# EditorConfig 적용 가이드

프로젝트 루트의 `.editorconfig` 파일은 들여쓰기, 줄바꿈, C# 네이밍 규칙 등을 정의합니다.
각 에디터/IDE에서 아래 설정을 따라 적용하세요.

---

## Visual Studio

별도 설치 없이 기본 지원합니다.

1. 솔루션 또는 프로젝트를 열면 `.editorconfig`를 자동으로 인식합니다.
2. `csharp_*`, `dotnet_*` 규칙은 Roslyn 분석기가 읽어 에디터에 경고/오류로 표시합니다.
3. 포맷 단축키: `Ctrl+K, Ctrl+D` (전체 문서), `Ctrl+K, Ctrl+F` (선택 영역)

**저장 시 자동 포맷 활성화:**

`Tools > Options > Text Editor > C# > Code Style > General`
→ **Format document on save** 체크

---

## Visual Studio Code

기본적으로 `.editorconfig`를 읽지 않으므로 확장 프로그램이 필요합니다.

### 1. 확장 설치

| 확장 | 용도 |
|------|------|
| **EditorConfig for VS Code** (`EditorConfig.EditorConfig`) | `.editorconfig` 파일 인식 |
| **C# Dev Kit** (`ms-dotnettools.csdevkit`) | `csharp_*`, `dotnet_*` 규칙 적용 |

```
ext install EditorConfig.EditorConfig
ext install ms-dotnettools.csdevkit
```

### 2. 워크스페이스 설정 확인

`.vscode/settings.json`에 아래 내용이 있어야 합니다 (이미 포함되어 있음):

```json
{
  "editor.tabSize": 4,
  "editor.insertSpaces": true,
  "editor.detectIndentation": false,
  "[csharp]": {
    "editor.tabSize": 4,
    "editor.insertSpaces": true,
    "editor.defaultFormatter": "ms-dotnettools.csharp"
  }
}
```

> `editor.detectIndentation`을 `false`로 설정하지 않으면 VS Code가 파일 내용을 분석해
> 들여쓰기를 자동 감지하고 `.editorconfig` 설정을 무시합니다.

### 3. 포맷 단축키

- `Shift+Alt+F` (Windows/Linux), `Shift+Option+F` (macOS): 전체 문서 포맷
- **저장 시 자동 포맷**: `Cmd+,` → `editor.formatOnSave` → `true`

---

## Rider

별도 설치 없이 기본 지원합니다. 단, 기본적으로 자체 코드 스타일 설정이 우선하므로
`.editorconfig`를 우선하도록 변경해야 합니다.

### 1. EditorConfig 우선순위 설정

`Settings > Editor > Code Style`
→ **Enable EditorConfig support** 체크 (기본 활성화)
→ **Use EditorConfig settings** 선택 시 `.editorconfig`가 Rider 자체 설정보다 우선 적용됨

### 2. 포맷 단축키

- `Ctrl+Alt+L` (Windows/Linux), `Cmd+Option+L` (macOS): 전체 문서 포맷
- **저장 시 자동 포맷**: `Settings > Tools > Actions on Save` → **Reformat code** 체크

### 3. 네이밍 규칙 적용 확인

`Settings > Editor > Inspections > C# > Naming`
→ `.editorconfig`의 `dotnet_naming_rule.*` 항목이 자동으로 반영됩니다.
→ 규칙 위반 시 에디터에 경고가 표시됩니다.

---

## 공통 주의사항

- `.editorconfig`는 파일 경로 기준 **가장 가까운 파일**이 우선 적용됩니다.
  하위 폴더에 별도 `.editorconfig`가 있으면 해당 폴더 규칙이 덮어씁니다.
- `root = true`가 설정되어 있으므로 상위 디렉토리의 `.editorconfig`는 무시됩니다.
- `csharp_*` / `dotnet_*` 네이밍 규칙은 EditorConfig 확장만으로는 적용되지 않으며,
  Roslyn 분석기(Visual Studio, C# Dev Kit, Rider)가 있어야 동작합니다.
