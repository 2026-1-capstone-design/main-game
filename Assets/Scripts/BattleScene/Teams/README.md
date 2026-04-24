# Teams

이 디렉토리는 전투 팀 구조를 아래 4가지 관심사로 나눠 다룹니다.

- **팀 식별**: 어떤 유닛이 어느 팀인가
- **팀 결성**: 어떤 유닛 묶음이 하나의 팀인가
- **로스터 정책**: 그 팀을 슬롯/번호 체계에서 어떻게 다루는가
- **UI/오더 해석**: 런타임 유닛을 player/hostile 관점으로 어떻게 읽는가

이 구조의 목적은 팀 내용, 슬롯 정책, 표시용 해석을 서로 분리해 각 계층이 자기 책임만 알도록 만드는 것입니다.

## 한 줄 흐름

```
`BattleUnitSnapshot` 생성
-> `BattleTeamEntry`로 팀 결성
-> `BattleRosterLayout`으로 슬롯/번호 정책 결정
-> `BattleStartPayload`에 담아 Battle Scene으로 전달
-> `BattleSceneFlowManager`가 팀별 스폰 좌표 계산
-> `BattleBootstrapper`가 실제 `BattleRuntimeUnit` 생성
-> `BattleRosterProjection`이 UI/오더용 읽기 모델 제공
```

## 핵심 타입

### `BattleTeamId`
- 전투 내부에서 팀 정체성을 표현하는 값 객체입니다.
- `bool`이 아니라 명시적인 팀 ID를 사용해 팀 수와 표현을 일반화합니다.

### `BattleTeamIds`
- `Player`, `Enemy` 같은 기본 팀 ID 상수 모음입니다.
- 빠르게 읽히는 코드 작성을 돕는 편의 타입입니다.

### `BattleTeamEntry`
- 하나의 전투 팀에 속한 실제 유닛 묶음입니다.
- 질문으로 바꾸면 "누가 이 팀에 속해 있나?"에 답하는 타입입니다.
- 포함 내용:
  - `TeamId`
  - `IsPlayerOwned`
  - `Units`

### `BattleTeamLayout`
- 특정 팀이 로스터에서 차지하는 슬롯 구간입니다.
- 질문으로 바꾸면 "이 팀을 슬롯/번호 체계에서 어떻게 배치하나?"에 답하는 타입입니다.
- 포함 내용:
  - `TeamId`
  - `MaxUnitCount`
  - `GlobalSlotStart`

### `BattleRosterLayout`
- 전체 팀에 대한 슬롯/번호 정책입니다.
- 담당 책임:
  - 팀별 최대 슬롯 수 조회
  - 팀별 전역 번호 시작점 관리
  - 로컬 슬롯 -> 전역 `UnitNumber` 변환
  - 전역 `UnitNumber` -> 로컬 슬롯 역변환

### `BattleRosterProjection`
- `BattleStartPayload + runtimeUnits`를 UI/오더 관점으로 다시 해석하는 읽기 모델입니다.
- 담당 책임:
  - 플레이어 슬롯 조회
  - hostile 슬롯 조회
  - `A_01`, `E_01` 같은 표시용 유닛 ID 생성
  - 상태 UI와 오더가 같은 슬롯 규칙을 공유하도록 보장

## 타입 간 관계

## Slot의 의미

이 디렉토리에서 `slot`은 "팀을 UI/번호 체계에서 배치하는 자리"를 뜻합니다.

중요한 점은 `slot`이 항상 "실제로 현재 유닛이 들어 있는 칸"만 의미하지는 않는다는 것입니다.
`slot`은 비어 있을 수도 있고, 실제 팀 멤버 수보다 더 크게 잡힐 수도 있습니다.

예를 들어:

- 플레이어 팀 실제 유닛 수 = 4
- 플레이어 팀 슬롯 수 = 6

이라면, 유닛은 4명만 있어도 로스터는 6칸으로 표현될 수 있습니다.

이 문맥에서 slot은 두 단계로 나뉩니다.

### 로컬 슬롯

- 특정 팀 내부에서의 몇 번째 칸인지 나타냅니다.
- 예: 플레이어 팀의 0번 슬롯, 1번 슬롯, 2번 슬롯

### 전역 번호

- 전체 전투 기준에서 유닛을 식별하는 번호입니다.
- `BattleRosterLayout`이 로컬 슬롯을 전역 `UnitNumber`로 변환합니다.

즉 관계는 아래와 같습니다.

- `BattleTeamEntry` = 실제로 어떤 유닛이 팀에 속하는가
- `BattleTeamLayout` = 그 팀이 슬롯을 몇 칸 가지는가
- `BattleRosterLayout` = 로컬 슬롯을 전역 번호로 어떻게 바꾸는가
- `BattleRosterProjection` = 그 슬롯을 다시 UI/오더 관점으로 어떻게 읽는가

## Slot이 쓰이는 곳

`slot`은 이 디렉토리 안에서만 정의되는 개념이 아니라, 실제 전투 흐름 여러 지점에서 함께 쓰입니다.

### 1. 유닛 번호 부여

- `BattleBootstrapper`가 팀 유닛을 생성할 때 `BattleRosterLayout.AllocateUnitNumber(...)`를 호출합니다.
- 이때 "팀 내부 몇 번째 슬롯인가"가 전역 `UnitNumber`로 변환됩니다.

즉 slot은 런타임 유닛 생성 시 번호 정책의 입력으로 쓰입니다.

### 2. 상태 UI 배치

- `BattleStatusGridUIManager`는 `BattleRosterProjection`을 통해
  - 플레이어 팀 몇 번째 슬롯인지
  - 적대 측 몇 번째 슬롯인지
  를 조회합니다.
- 그 결과로 각 상태 셀에 어떤 유닛을 표시할지 결정합니다.

즉 slot은 상태 패널의 셀 위치를 정하는 데 쓰입니다.

### 3. 오더 UI 해석

- `BattleOrdersManager`도 `BattleRosterProjection`을 통해 player/hostile 슬롯을 해석합니다.
- 어떤 유닛이 플레이어 쪽 명령 대상인지, 어떤 hostile 순번으로 보여야 하는지 여기서 정합니다.

즉 slot은 오더 시스템의 대상 해석 기준으로 쓰입니다.

### 4. 표시용 유닛 ID 생성

- `BattleRosterProjection.GetDisplayUnitId(...)`는 slot을 바탕으로 `A_01`, `E_01` 같은 표시용 ID를 만듭니다.
- 여기서 player slot과 hostile slot이 그대로 표시 규칙에 반영됩니다.

즉 slot은 UI/LLM 공통 식별자 생성에도 쓰입니다.

### 5. 빈 칸 유지

- 팀의 실제 유닛 수보다 슬롯 수가 더 클 수 있기 때문에, UI는 빈 칸을 포함한 로스터 구조를 유지할 수 있습니다.
- 예를 들어 실제 유닛이 4명이어도 슬롯이 6칸이면 남은 2칸은 빈 슬롯으로 남습니다.

즉 slot은 "현재 있는 유닛"만이 아니라 "로스터 구조 자체"를 표현하는 데 쓰입니다.

### `BattleTeamEntry` vs `BattleTeamLayout`

이 둘은 이름이 비슷하지만 관심사가 다릅니다.

#### `BattleTeamEntry`
- 팀의 실제 멤버를 담습니다.
- 즉, "누가 이 팀에 속해 있나?"를 표현합니다.

#### `BattleTeamLayout`
- 팀의 슬롯/번호 정책을 담습니다.
- 즉, "이 팀을 몇 칸으로 보고 번호를 어디서부터 줄까?"를 표현합니다.

예시:

- `BattleTeamEntry(Player)` = 플레이어 팀에 실제 유닛 4명이 들어 있음
- `BattleTeamLayout(Player)` = 플레이어 팀은 슬롯 6칸을 사용하고 번호는 1번부터 시작함

정리하면:

- `BattleTeamEntry`는 내용 모델
- `BattleTeamLayout`은 정책 모델

## 실제 코드 흐름

### 1. 팀 설정

`MainFlowManager` 또는 `BattleSceneTester`가 `BattleUnitSnapshot` 목록을 만듭니다.

### 2. 팀 결성

스냅샷 목록을 `BattleTeamEntry`로 묶습니다.

예:

- 플레이어 팀 엔트리
- hostile 팀 엔트리

### 3. 로스터 정책 결정

`BattleRosterLayout.CreateSequential(...)`로 팀별 슬롯 구간과 전역 번호 정책을 만듭니다.

### 4. payload 구성

`BattleStartPayload`에 아래를 담습니다.

- `Teams`
- `PlayerTeamId`
- `RosterLayout`

### 5. Battle Scene 진입

`BattleSceneFlowManager`가 payload를 받아 팀별 스폰 좌표를 계산합니다.

이 단계의 관심사는 "어떤 팀이 어디에 등장하는가"이며, 팀 정체성이나 슬롯 번호 정책과는 분리됩니다.

### 6. 실제 스폰

`BattleBootstrapper`가 각 `BattleTeamEntry`를 순회하며:

- flow manager가 계산한 팀별 좌표를 받고
- `BattleRosterLayout`으로 `UnitNumber`를 만들고
- `BattleRuntimeUnit.Initialize(...)`를 호출합니다

### 7. UI / 오더 해석

`BattleRosterProjection`이 런타임 유닛을 다시 player/hostile 슬롯으로 해석합니다.

이렇게 해서:

- 상태 UI
- 오더 매니저
- 표시용 유닛 ID

가 같은 규칙을 공유합니다.

## 읽는 순서 추천

처음 읽을 때는 아래 순서가 가장 쉽습니다.

1. `BattleTeamId`
2. `BattleTeamEntry`
3. `BattleTeamLayout`
4. `BattleRosterLayout`
5. `BattleRosterProjection`

그 다음 실제 흐름은 아래 파일에서 보면 됩니다.

1. `MainFlowManager` 또는 `BattleSceneTester`
2. `BattleStartPayload`
3. `BattleSceneFlowManager`
4. `BattleBootstrapper`

이 순서로 보면:

- 팀이 무엇인지
- 팀을 어떻게 묶는지
- 팀을 로스터에서 어떻게 다루는지
- 런타임 유닛을 UI가 어떻게 읽는지

가 자연스럽게 이어집니다.
