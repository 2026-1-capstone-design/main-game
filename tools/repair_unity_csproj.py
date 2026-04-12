#!/usr/bin/env python3
"""Remove stale Compile Include entries from Unity-generated .csproj files."""

from __future__ import annotations

import argparse
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Remove Compile Include entries that point to deleted .cs files."
    )
    parser.add_argument(
        "projects",
        nargs="*",
        default=["Assembly-CSharp.csproj"],
        help="Project files to repair. Defaults to Assembly-CSharp.csproj.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Only report stale entries. Exit 1 if any are found.",
    )
    parser.add_argument(
        "--build",
        action="store_true",
        help="Run dotnet build <project> --no-restore after repairing.",
    )
    return parser.parse_args()


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def repair_project(project_path: Path, check: bool) -> int:
    if not project_path.is_file():
        raise FileNotFoundError(f"Project file not found: {project_path}")

    tree = ET.parse(project_path)
    root = tree.getroot()
    stale_items: list[tuple[ET.Element, ET.Element, str]] = []

    for parent in root.iter():
        for child in list(parent):
            if local_name(child.tag) != "Compile":
                continue

            include = child.attrib.get("Include")
            if not include or "*" in include:
                continue

            source_path = project_path.parent / include
            if not source_path.is_file():
                stale_items.append((parent, child, include))

    if not stale_items:
        print(f"No stale Compile Include entries: {project_path.name}")
        return 0

    for _, _, include in stale_items:
        print(f"Stale Compile Include: {include}")

    if check:
        return len(stale_items)

    for parent, child, _ in stale_items:
        parent.remove(child)

    tree.write(project_path, encoding="utf-8", xml_declaration=True)
    print(f"Removed {len(stale_items)} stale Compile Include entries from {project_path.name}")
    return len(stale_items)


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
        print(f"Total removed stale Compile Include entries: {total_stale}")

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
