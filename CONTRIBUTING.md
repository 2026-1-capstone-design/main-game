# Contributing Guide

## Branching Strategy
- **main**: 항상 배포 가능한 상태를 유지한다. PR은 main 브랜치로 머지한다.
- **feat/**: 새로운 기능 개발을 위한 브랜치. 예: `feat/player-dash-ability`
- **bugfix/**: 버그 수정을 위한 브랜치. 예: `bugfix/battle-unit-death-event`
- **refactor/**: 기능 변경 없이 코드 개선을 위한 브랜치. 예: `refactor/separate-state-visual`
- **docs/**: 문서 작성 및 수정 브랜치. 예: `docs/update-readme`
- **chore/**: 빌드 설정, 패키지 관리 등 기타 작업을 위한 브랜치. 예: `chore/update-unity-meta`

#### Branch Naming
- `<type>/<problem-description>` 형식으로 작성한다. `<type>`은 위의 목록에서 선택하고, `<problem-description>`은 소문자와 하이픈(`-`)으로 작성한다.
- 브랜치 이름은 영어로 작성한다.

## Commit

- 커밋 단위는 논리적인 단위로 작성한다. 하나의 커밋에는 하나의 변경 사항이 포함되어야 한다.
- 커밋 메시지는 영어 또는 한국어로, 명확한 내용이 드러나도록 작성한다.
- "wip" 같은 커밋 메시지는 피한다. 작업이 완료되지 않았더라도 커밋 메시지는 명확하게 작성한다.

## Pull Requests

### 범위 및 Draft 활용

- **PR은 최대한 작게 쪼개서 올린다.** 큰 기능이나 리팩터링은 논리적인 단위로 분리하여 각각 PR을 올린다. 리뷰어의 부담을 줄이고 리뷰 품질을 높이기 위함이다.
- **PR 템플릿을 활용한다.** PR 본문에 작업 개요, 문제 설명, 해결 방안, 기대 효과, 영향 범위을 명확하게 작성한다.
- **Draft PR을 적극적으로 활용한다.** 작업이 완료되지 않았더라도 Draft 상태로 PR을 열어 팀원들이 현재 누가 어떤 작업을 진행 중인지 파악할 수 있게 한다. 작업이 완료되면 Ready for Review로 전환한다.
- **선행 PR이 존재하면 반드시 Draft로 올린다.** PR 본문 개요 하단에 `Blocked By #<PR번호>` 형식으로 선행 PR을 명시한다. 선행 PR이 Merge된 후 Ready for Review로 전환한다.

### 코드 리뷰

- PR 게시 후 **셀프 리뷰**를 적극 권장한다. PR 본문 외에 리뷰어에게 알려야 하는 정보가 있다면 셀프 리뷰로 작성한다.
- **팀원 모두에게 코드 리뷰 의무가 있다.** 리뷰 요청을 받은 팀원은 적극적으로 리뷰에 참여한다.
- 리뷰 의무가 지켜지지 않아 **마지막 업데이트로부터 48시간이 경과**하면 자동 Approve된 것으로 간주하고 Merge할 수 있다.
- **수정 완료된 리뷰 코멘트는 반드시 Resolve한다.** Resolve되지 않은 코멘트가 남아 있으면 Merge할 수 없다. 만약 수정 필요가 없는 리뷰라면, 그 이유를 코멘트로 남기고 Resolve한다. 
- PR을 올릴 때, Github Application으로 설치된 *Gemini Code Assistant*가 자동으로 리뷰를 작성한다. Draft에서도 자동으로 호출되므로, 리뷰 가능한 PR로 전환하면 `/gemini review` 명령어를 코멘트로 입력하여 리뷰를 재요청한다.

### Merge 전 준비

- Approve를 받은 후 Merge 전에, 가능하다면 **최신 `main` 브랜치를 기준으로 rebase**한다.
  ```bash
  git fetch origin
  git rebase origin/main
  ```
  Merge commit 대신 rebase를 사용하는 이유는 git graph를 선형으로 유지하기 위함이다. `main` 브랜치뿐만 아니라 PR 브랜치 간에 merge commit이 쌓이기 시작하면 git graph가 매우 복잡해진다. 이렇게 꼬인 git graph를 보는데 오는 인지적 부하가 rebase를 사용할 때 오는 인지적 부하보다 크다고 판단하여 rebase를 규칙으로 정한다.
- PR target branch와 conflict가 발생한 경우 merge commit이 아닌 rebase로 해결한다.

## Code Style

### Csharpier
C# 코드 스타일은 [Csharpier](https://csharpier.com/)를 따른다.

**설치 방법**
1. dotnet tool을 사용하여 Csharpier를 설치한다.
  ```bash
    dotnet tool install csharpier
  ```
  만약 NuGet 피드에서 찾을 수 없다는 오류가 발생하면, `%appdata%\NuGet\NuGet.Config` 파일을 열어 `<packageSources>` 섹션에 다음 항목을 추가한다.
  ```xml
    <packageSources>
      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    </packageSources>
  ```
2. 사용하는 에디터에 따라 [에디터 설정](https://csharpier.com/docs/Editors)을 참고하여 Csharpier를 설정한다. Visual Studio Code에서는 `.vscode/settings.json`에 저장 시 자동 포맷팅 설정이 이미 적용되어 있다.

## Ai Driven Development
Claude Code, Codex, Gemini CLI 등 AI 코딩 어시스턴스를 적극 활용할 수 있다. 특히 PR을 작성할 때 `write-pr` skill의 사용을 권장한다.
