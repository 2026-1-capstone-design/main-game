## 개요

BootScene에서 바로 MainScene으로 진입하던 흐름을 TitleScene을 거치도록 변경하고, 타이틀 화면에서 New Game 버튼으로 MainScene을 시작하도록 연결했습니다. TitleScene에는 기본 UI 구조와 `TitleSceneUIManager`를 추가했고, Build Settings에 `TitleScene`을 포함해 실제 실행 경로가 일치하도록 정리했습니다.

## 해결 과제

### 1. Title Scene 구현
시작 시점에 바로 MainScene으로 이동하던 기존 흐름 때문에 타이틀의 진입 메뉴(새 게임/불러오기/설정/종료)를 둘 수 없었습니다.

### 2. Settings 진입점 부재
타이틀에 Settings 진입점이 없어 옵션 화면 확장 경로가 필요했습니다. 이번 변경에서 버튼과 처리 구조를 마련했습니다(실제 설정 화면은 후속 작업).

### 3. 씬 시작 경로 정리
`BootScene`, `TitleScene`, `MainScene` 사이의 부팅/빌드 설정이 일치하지 않으면 런타임 씬 로드 실패 가능성이 있어 Build Settings와 부팅 흐름을 정리해야 했습니다.

## 해결 방안

### 씬 흐름 변경
- **기존**: `BootScene → MainScene → BattleScene`
- **변경**: `BootScene → TitleScene → MainScene → BattleScene`

### TitleScene UI 구성
`TitleScene`에 Canvas, 배경 Panel, `New Game`, `Load Game`, `Settings`, `Quit` 버튼을 배치하고 `TitleSceneUIManager`가 각 버튼 동작을 담당하도록 구현했습니다.

- `New Game`: `MainScene`으로 이동
- `Load Game`: (향후 불러오기 기능 연결)
- `Settings`: (향후 설정 화면 연결)
- `Quit`: 에디터/빌드 환경에 맞춰 종료

### 부팅 흐름 정리
`AppFlowController`의 초기 진입 씬 이름과 `ProjectSettings/EditorBuildSettings.asset`을 함께 수정해 실제 실행 경로를 일치시켰습니다.

## 기대 효과

- 타이틀 화면이 생기면서 게임 시작 전 진입 흐름이 분리되어, 메뉴 확장(설정, 불러오기, 프로필 등)이 용이해집니다.
- 씬 전환 책임이 명확해져 유지보수성이 향상됩니다.
- `SceneLoader`와 `TitleSceneUIManager`를 통해 씬 이동 경로를 중앙집중식으로 관리할 수 있습니다.

## 영향 범위

| 파일 | 변경 유형 |
|------|---------|
| .vscode/settings.json | 수정 |
| Assets/Fonts/NanumGothic SDF.asset | 수정 |
| Assets/Scenes/BootScene.unity | 수정 |
| Assets/Scenes/TitleScene.unity | 신규 |
| Assets/Scenes/TitleScene.unity.meta | 신규 |
| Assets/Scripts/BootScripts/AppFlowController.cs | 수정 |
| Assets/Scripts/BootScripts/TitleSceneUIManager.cs | 신규 |
| Assets/Scripts/BootScripts/TitleSceneUIManager.cs.meta | 신규 |
| ProjectSettings/EditorBuildSettings.asset | 수정 |
