#!/usr/bin/env python3
"""Synchronize Unity-generated .csproj Compile entries with local C# files."""

from __future__ import annotations

import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


DEFAULT_PROJECTS = ["Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Synchronize Compile Include entries for local Unity C# files."
    )
    parser.add_argument(
        "projects",
        nargs="*",
        default=DEFAULT_PROJECTS,
        help="Project files to repair. Defaults to Assembly-CSharp and Assembly-CSharp-Editor.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Only report mismatched entries. Exit 1 if any are found.",
    )
    parser.add_argument(
        "--build",
        action="store_true",
        help="Run dotnet build <project> --no-restore after repairing.",
    )
    return parser.parse_args()


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def is_editor_script(path: Path) -> bool:
    return "Editor" in path.parts


def find_asmdef_roots(assets_root: Path) -> set[Path]:
    return {path.parent.resolve() for path in assets_root.rglob("*.asmdef")}


def is_under_any(path: Path, roots: set[Path]) -> bool:
    resolved = path.resolve()
    return any(resolved == root or root in resolved.parents for root in roots)


def expected_sources(project_path: Path) -> set[str]:
    assembly_name = project_path.stem
    repo_root = project_path.parent
    assets_root = repo_root / "Assets"

    if assembly_name not in {"Assembly-CSharp", "Assembly-CSharp-Editor"}:
        return set()

    asmdef_roots = find_asmdef_roots(assets_root)
    sources: set[str] = set()

    for source_path in assets_root.rglob("*.cs"):
        if is_under_any(source_path, asmdef_roots):
            continue

        editor_script = is_editor_script(source_path.relative_to(assets_root))
        if assembly_name == "Assembly-CSharp" and editor_script:
            continue
        if assembly_name == "Assembly-CSharp-Editor" and not editor_script:
            continue

        sources.add(str(source_path.relative_to(repo_root)))

    return sources


def normalize_include(include: str) -> str:
    return str(Path(include))


def include_sort_key(include: str) -> tuple[str, str]:
    return (Path(include).parent.as_posix().lower(), Path(include).name.lower())


def repair_project(project_path: Path, check: bool) -> int:
    if not project_path.is_file():
        raise FileNotFoundError(f"Project file not found: {project_path}")

    tree = ET.parse(project_path)
    root = tree.getroot()
    stale_items: list[tuple[ET.Element, ET.Element, str]] = []
    compile_items: list[tuple[ET.Element, ET.Element, str]] = []
    compile_item_group: ET.Element | None = None

    for parent in root.iter():
        for child in list(parent):
            if local_name(child.tag) != "Compile":
                continue

            include = child.attrib.get("Include")
            if not include or "*" in include:
                continue

            compile_items.append((parent, child, include))
            compile_item_group = parent
            source_path = project_path.parent / include
            if not source_path.is_file():
                stale_items.append((parent, child, include))

    expected = expected_sources(project_path)
    existing = {normalize_include(include) for _, _, include in compile_items}
    missing_items = sorted(expected - existing, key=include_sort_key)
    mismatch_count = len(stale_items) + len(missing_items)

    if mismatch_count == 0:
        print(f"Compile Include entries are synchronized: {project_path.name}")
        return 0

    for _, _, include in stale_items:
        print(f"Stale Compile Include: {include}")
    for include in missing_items:
        print(f"Missing Compile Include: {include}")

    if check:
        return mismatch_count

    for parent, child, _ in stale_items:
        parent.remove(child)

    if missing_items:
        if compile_item_group is None:
            compile_item_group = ET.SubElement(root, "ItemGroup")

        for include in missing_items:
            item = ET.Element("Compile")
            item.set("Include", include)
            compile_item_group.append(item)

    tree.write(project_path, encoding="utf-8", xml_declaration=True)
    print(
        f"Synchronized {project_path.name}: "
        f"removed {len(stale_items)} stale, added {len(missing_items)} missing Compile Include entries"
    )
    return mismatch_count


def build_project(project_path: Path) -> int:
    print(f"Building {project_path.name}", flush=True)
    completed = subprocess.run(
        ["dotnet", "build", str(project_path), "--no-restore"],
        check=False,
    )
    return completed.returncode


def main() -> int:
    args = parse_args()
    repo_root = Path(__file__).resolve().parents[1]
    projects = [
        Path(project) if Path(project).is_absolute() else repo_root / project
        for project in args.projects
    ]

    total_stale = 0
    for project in projects:
        total_stale += repair_project(project, args.check)

    if args.check:
        return 1 if total_stale else 0

    if total_stale:
        print(f"Total synchronized Compile Include mismatches: {total_stale}")

    if args.build:
        for project in projects:
            exit_code = build_project(project)
            if exit_code != 0:
                return exit_code

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
