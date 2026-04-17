# Contributing Guide

## Pull Requests

### 범위 및 초안 활용

- **PR은 최대한 작게 쪼개서 올린다.** 큰 기능이나 리팩터링은 논리적인 단위로 분리하여 각각 PR을 올린다. 리뷰어의 부담을 줄이고 리뷰 품질을 높이기 위함이다.
- **Draft PR을 적극적으로 활용한다.** 작업이 완료되지 않았더라도 Draft 상태로 PR을 열어 팀원들이 현재 누가 어떤 작업을 진행 중인지 파악할 수 있게 한다. 작업이 완료되면 Ready for Review로 전환한다.
- **선행 PR이 존재하면 반드시 Draft로 올린다.** PR 본문 개요 하단에 `Blocked By #<PR번호>` 형식으로 선행 PR을 명시한다. 선행 PR이 Merge된 후 Ready for Review로 전환한다.

### 코드 리뷰

- **팀원 모두에게 코드 리뷰 의무가 있다.** 리뷰 요청을 받은 팀원은 적극적으로 리뷰에 참여한다.
- 리뷰 의무가 지켜지지 않은 경우, PR 작성자는 **자율적으로 Merge**할 수 있다.
- **완료된 리뷰 코멘트는 반드시 Resolve한다.** Resolve되지 않은 코멘트가 남아 있으면 Merge할 수 없다.
- 리뷰어가 없거나 **마지막 업데이트로부터 48시간이 경과**하면 자동 Approve된 것으로 간주하고 Merge할 수 있다.

### Merge 전 준비

- Approve를 받은 후 Merge 전에, 가능하다면 **최신 `main` 브랜치를 기준으로 rebase**한다.
  ```bash
  git fetch origin
  git rebase origin/main
  ```
- Merge commit 대신 rebase를 사용하는 이유는 git graph를 선형으로 유지하기 위함이다. Merge commit이 쌓이면 히스토리가 복잡해진다.

---

## Branch Naming

```
<type>/<problem-description>
```

`<type>`은 아래 중 하나를 사용한다.

| Type | 설명 |
|------|------|
| `feat` | 새로운 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 기능 변경 없는 코드 개선 |
| `docs` | 문서 작성 및 수정 |
| `test` | 테스트 코드 추가/수정 |
| `chore` | 빌드 설정, 패키지 관리 등 |
| `perf` | 성능 개선 |

`<problem-description>`은 소문자와 하이픈(`-`)으로 작성한다.

**예시**

```
feat/player-dash-ability
fix/battle-unit-death-event
refactor/separate-state-visual
```

---

## Commit Messages

```
<type>: <solve the problem>
```

- `<type>`은 브랜치 네이밍과 동일한 목록을 사용한다.
- 설명은 **영어**, **현재형 동사**로 시작한다. (e.g. `add`, `fix`, `move`, `remove`)
- 첫 글자는 소문자로 시작한다.

**예시**

```
feat: add player dash ability
fix: resolve battle unit death event not firing
refactor: move HP logic to BattleUnitCombatState
chore: update Unity meta files
```
