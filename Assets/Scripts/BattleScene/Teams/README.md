# Teams

이 디렉토리는 현재 전투의 팀 구조를 "팀 정체성", "팀 멤버", "표시용 인덱스 해석" 세 층으로 나눠 다룹니다.

현재 전투는 **2팀 체계**입니다.

- 플레이어 팀 1개
- 적 팀 1개

팀 내부 순서는 각 팀의 `Units` 인덱스로 표현합니다.

## 한 줄 흐름

```text
`BattleUnitSnapshot` 생성
-> `BattleTeamEntry`로 player team / hostile team 구성
-> `BattleStartPayload`가 팀 순서 기준 `UnitNumber` 규칙 계산
-> `BattleSceneFlowManager`가 팀별 스폰 좌표 계산
-> `BattleBootstrapper`가 실제 `BattleRuntimeUnit` 생성
-> `BattleRosterProjection`이 player index / hostile index / 표시 ID 제공
```

## 핵심 타입

### `BattleTeamId`

- 전투 안에서 팀을 식별하는 값 객체입니다.
- `bool` 대신 명시적 ID를 사용해 팀 판정 기준을 분명히 합니다.

### `BattleTeamIds`

- `Player`, `Enemy` 기본 상수 모음입니다.
- 현재 2팀 체계에서 읽기 쉬운 코드를 위한 편의 타입입니다.

### `BattleTeamEntry`

- 하나의 전투 팀에 속한 실제 유닛 묶음입니다.
- 질문으로 바꾸면 "누가 이 팀에 속해 있나?"에 답합니다.
- 핵심 데이터:
  - `TeamId`
  - `IsPlayerOwned`
  - `Units`

여기서 중요한 점은 `Units`가 이미 **순서 있는 컬렉션**이라는 것입니다.
즉, 팀 내부 몇 번째 유닛인가는 `Units[0]`, `Units[1]` 같은 인덱스로 표현합니다.

### `BattleStartPayload`

`Teams` 디렉토리 밖에 있는 타입이지만, 현재 팀 구조의 중심 책임을 함께 가집니다.

- player team / hostile team 검증
- 팀 순서 기준 `UnitNumber` 시작점 계산
- `AllocateUnitNumber(teamId, localUnitIndex)` 제공
- `TryGetTeamLocalUnitIndex(teamId, unitNumber, out index)` 제공

즉, 별도 layout 객체 없이도 "팀 내부 인덱스 <-> 전역 번호" 변환을 담당합니다.

### `BattleRosterProjection`

- 런타임 유닛을 UI/오더 관점으로 다시 읽는 projection입니다.
- 핵심 질문은 아래와 같습니다.
  - 이 유닛이 플레이어 팀인가
  - 플레이어 팀 내부 몇 번째인가
  - 적 팀 내부 몇 번째인가
  - 화면/LLM용 표시 ID는 무엇인가

## Index와 Slot

이 구조의 핵심 데이터 개념은 **team-local index**입니다.

### Team-local index

- 특정 팀 내부에서 몇 번째 유닛인가를 뜻합니다.
- 예:
  - 플레이어 팀 첫 번째 유닛 = player index 0
  - 적 팀 세 번째 유닛 = hostile index 2

이 값은 별도 구조에 저장하지 않고 `Units` 순서와 `UnitNumber` 계산으로부터 얻습니다.

### UI slot

`slot`이라는 말은 **UI 칸**을 설명할 때 사용합니다.

- 상태 패널의 ally 칸
- 상태 패널의 enemy 칸
- 오더 버튼이 붙은 셀

즉:

- 도메인/데이터 계층: `index`
- UI 계층: `slot`

으로 읽으면 됩니다.

## 실제 코드 흐름

### 1. 팀 구성

`MainFlowManager` 또는 `BattleSceneTester`가 아군/적군 `BattleUnitSnapshot` 목록을 만든 뒤 `BattleTeamEntry` 두 개로 묶습니다.

### 2. payload 구성

`BattleStartPayload`가:

- 팀이 정확히 두 개인지
- player team과 hostile team이 모두 있는지
- 각 팀에 유닛이 최소 1명 이상 있는지

를 확인하고, 팀 순서 기준으로 `UnitNumber` 규칙을 준비합니다.

### 3. 전장 배치

`BattleSceneFlowManager`가 플레이어 팀과 적 팀의 스폰 좌표를 계산합니다.

이 단계는 "어디에 등장하는가"를 다루며, 팀 내부 인덱스 계산과는 분리됩니다.

### 4. 실제 스폰

`BattleBootstrapper`가 각 팀의 `Units[i]`를 순회하며:

- `payload.AllocateUnitNumber(teamId, i)`로 `UnitNumber`를 만들고
- 해당 좌표에 `BattleRuntimeUnit`을 생성합니다.

### 5. UI / 오더 해석

`BattleRosterProjection`이 런타임 유닛을 다시 읽어:

- `TryGetPlayerIndex(...)`
- `TryGetHostileIndex(...)`
- `GetDisplayUnitId(...)`

를 제공합니다.

`BattleStatusGridUIManager`와 `BattleOrdersManager`는 이 projection만 사용해 같은 기준으로 유닛을 해석합니다.

## 읽는 순서 추천

처음 볼 때는 아래 순서가 가장 직접적입니다.

1. `BattleTeamId`
2. `BattleTeamEntry`
3. `BattleStartPayload`
4. `BattleRosterProjection`
5. `BattleSceneFlowManager`
6. `BattleBootstrapper`

이 순서로 보면:

- 팀이 무엇인지
- 팀이 어떻게 묶이는지
- 번호가 어떻게 정해지는지
- UI가 팀 내부 순서를 어떻게 읽는지

가 짧게 이어집니다.
