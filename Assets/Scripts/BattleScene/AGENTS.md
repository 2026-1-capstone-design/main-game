# BattleScene 작업 지침

## BattleRuntimeUnit과 BattleUnitCombatState 책임 분리

`BattleRuntimeUnit`은 Unity 씬 오브젝트와 비주얼 경계, `BattleUnitCombatState`는 HP, 스탯, 타겟, 쿨다운, 위치 같은 순수 전투 상태를 담당한다. 전투 판단, 파라미터 계산, AI 계획, 스킬/피해 판정은 가능한 한 `BattleUnitCombatState` 또는 `BattleFieldSnapshot` 기준으로 작성한다.

`BattleRuntimeUnit` 의존은 이동/회전, 애니메이션, UI, 프리팹 참조처럼 씬 오브젝트가 필요한 작업으로 제한한다. 로직이 `BattleRuntimeUnit`을 받더라도 실제 판단과 상태 변경은 `unit.State`에 위임한다.

위치를 실제로 이동시키는 코드는 `BattleRuntimeUnit.SetPosition`을 사용한다. `Transform`만 직접 바꾸면 시뮬레이션 상태와 비주얼 위치가 어긋난다.

## 틱 단위 계산 캐싱

매 틱마다 여러 유닛, 플래너, 스킬에서 반복 조회되는 값은 가능한 한 한 번 계산해 캐싱한다. 같은 틱 안에서 변하지 않는 아군/적군 목록, 팀 중심, 타겟 후보, 거리/위치 기반 값은 스냅샷이나 시스템 내부 버퍼에 두고 재사용한다.

캐시는 반드시 틱 경계를 명확히 가져야 하며, 이전 틱의 타겟, 거리, 팀 중심, 생존 여부가 남지 않도록 재빌드 시 비우거나 `battleTick`으로 검증한다. 이동처럼 틱 중 위치가 바뀌는 단계에서는 stale 위치 값을 쓰지 않도록 해당 단계 전용으로 다시 계산한다.

틱 루프에서는 새 `List`, `Dictionary`, LINQ 결과, 배열 생성을 피하고 기존 버퍼를 `Clear`해서 재사용한다. 거리 비교는 가능한 경우 `Vector3.Distance` 반복 호출 대신 `delta.sqrMagnitude` 또는 미리 계산한 거리 값을 사용한다.
