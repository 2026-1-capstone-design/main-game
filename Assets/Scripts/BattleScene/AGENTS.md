# BattleScene 작업 지침

## BattleRuntimeUnit과 BattleUnitCombatState 책임 분리

`BattleRuntimeUnit`은 전투 유닛의 **비주얼/씬 오브젝트 경계**다. `MonoBehaviour`이며 프리팹, `Transform`, `Animator`, HP bar, 상태 텍스트, 무기 모델 장착, 스킨 파츠 토글, 사망/공격/이동 애니메이션 트리거, 이펙트 또는 사운드 출력처럼 Unity 씬에 붙은 표현 계층을 담당한다.

전투 로직을 새로 추가할 때는 가능하다면 `BattleRuntimeUnit` 대신 `BattleUnitCombatState`를 우선 사용한다. `BattleUnitCombatState`는 `MonoBehaviour`가 아니며 HP, 전투불능 여부, 기본/실효 스탯, 버프, 쿨다운, 행동 타입, 타겟, 계획 위치, 넉백 누적값, 현재 파라미터/점수 같은 순수 전투 상태를 보유한다. `BattleFieldSnapshot`, 전투 파라미터 계산, 타겟 유효성 검사, AI 계획, 스킬/피해 판정처럼 시뮬레이션 의미를 다루는 코드는 이 상태 객체를 기준으로 작성하는 것이 기본 방침이다.

## 구현 기준

- 피해, 치유, 버프, 쿨다운, 행동 상태, 타겟 지정, 스탯 조회/계산은 `BattleUnitCombatState`를 사용한다.
- 거리, 타겟 유효성, 아군/적군 판정, 스냅샷 조회처럼 전투 판단에 필요한 값도 먼저 `BattleUnitCombatState.Position`, `TeamId`, `IsCombatDisabled`, `PlannedTargetEnemy` 등을 확인한다.
- `BattleRuntimeUnit.State`는 비주얼 객체에서 순수 상태로 들어가는 경계로만 사용한다. 로직이 `BattleRuntimeUnit`을 받더라도 실제 판단과 상태 변경은 가능한 한 `unit.State`에 위임한다.
- `BattleRuntimeUnit`을 직접 써야 하는 경우는 `Transform` 이동/회전, `SetPosition`, `FaceTarget`, 애니메이션 트리거, 무기/스킨/HP bar/상태 텍스트 갱신, 런타임 프리팹 참조, UI 바인딩처럼 씬 오브젝트가 필요한 작업으로 제한한다.
- 위치를 실제로 이동시키는 코드는 `BattleRuntimeUnit.SetPosition`을 사용한다. 이 메서드가 `transform.position`과 `BattleUnitCombatState.SyncPosition`을 함께 갱신하므로, `Transform`만 직접 바꾸면 시뮬레이션 상태와 비주얼 위치가 어긋난다.
- 새 시스템이 전투 결과를 계산하거나 테스트 가능해야 한다면 `BattleUnitCombatState` 또는 `BattleFieldSnapshot` 기반 API를 우선 설계한다. `BattleRuntimeUnit` 의존은 비주얼 출력이나 런타임 오브젝트 조회가 필요한 마지막 단계로 밀어낸다.

## 현재 코드에서의 패턴

- `BattleRuntimeUnit.Initialize`는 `BattleUnitSnapshot`으로 `BattleUnitCombatState`를 만들고, State 이벤트를 구독해 HP bar, 사망 표시, Animator, 상태 텍스트를 갱신한다.
- `BattleUnitCombatState.ApplyDamage`, `ApplyHeal`, `BuffApply`, `TickAttackCooldown`, `SetPlannedTargets`, `SetCurrentActionType` 등이 실제 전투 상태 변경 지점이다.
- `BattleFieldSnapshot`은 런타임 유닛 목록에서 State만 뽑아 `_allLivingStates`를 만들고, 대부분의 타겟 탐색 API를 `BattleUnitCombatState` 기준으로 제공한다.
- `BattlePhysicsSystem`처럼 실제 위치 이동이 필요한 시스템은 `BattleRuntimeUnit`을 받되, 판단에는 `unit.State`와 State의 타겟/스탯을 사용하고 최종 이동만 `unit.SetPosition`으로 반영한다.

요약하면, `BattleRuntimeUnit`은 "보이는 유닛"이고 `BattleUnitCombatState`는 "전투에서 계산되는 유닛"이다. 새 로직은 먼저 `BattleUnitCombatState`에 둘 수 있는지 검토하고, Unity 오브젝트가 필요한 경우에만 `BattleRuntimeUnit`에 의존한다.
