# 007. BattleUnitCombatState 비주얼 레이어 독립

> **선행 조건**: 이 플랜은 008(BattleSimulationManager 분해)의 선행 작업이다. `BattleFieldView`를 `BattleUnitCombatState` 기반으로 전환해야 008에서 각 System 클래스가 MonoBehaviour 없이 순수 데이터만 다룰 수 있다.

---

## 문제 상황

### 1. BattleFieldView가 BattleRuntimeUnit(MonoBehaviour)에 직접 결합

`BattleFieldView`는 전투 판정 로직(타겟 탐색, 거리 계산, 유효성 검사)을 담당하는 순수 전투 레이어다. 그런데 내부적으로 `IReadOnlyList<BattleRuntimeUnit>`을 보유하고 있어 Unity MonoBehaviour에 직접 의존한다.

```csharp
// BattleFieldView — 현재 구조
public sealed class BattleFieldView
{
    private readonly IReadOnlyList<BattleRuntimeUnit> _units;  // ← MonoBehaviour 목록

    public BattleRuntimeUnit FindNearestLivingEnemy(BattleRuntimeUnit requester)
    {
        // unit.Position        → transform.position (MonoBehaviour)
        // unit.IsCombatDisabled → State 위임 (순수 데이터)
        // unit.IsEnemy         → State 위임 (순수 데이터)
    }
}
```

`BattleFieldView`가 실제로 읽는 데이터를 추적하면 대부분이 `BattleUnitCombatState` 위임이고, MonoBehaviour가 직접 제공하는 것은 `Position`(transform.position)과 타겟 참조(`CurrentTarget`, `PlannedTargetEnemy`)뿐이다.

| BattleFieldView 접근 프로퍼티 | 실제 소유자 |
|---|---|
| `IsCombatDisabled`, `IsEnemy` | BattleUnitCombatState |
| `MaxHealth`, `CurrentHealth` | BattleUnitCombatState |
| `BodyRadius`, `AttackRange` | BattleUnitCombatState |
| `Position` | **transform.position — BattleRuntimeUnit** |
| `CurrentTarget`, `PlannedTargetEnemy` | **BattleRuntimeUnit 직접 보유** |

### 2. 시뮬레이션 레이어가 비주얼 레이어를 폴링해 데이터 레이어를 수정

`BattleSimulationManager.TickAllCooldowns()`에서 다음 코드가 매 틱 실행된다.

```csharp
// BattleSimulationManager.cs line 275
if (unit.IsAttacking && !unit.IsAttackAnimationPlaying())
    unit.State.SetAttackState(false);
```

**시뮬레이션 레이어**(`BattleSimulationManager`)가 **비주얼 레이어**(`BattleRuntimeUnit.IsAttackAnimationPlaying()` — Animator 직접 폴링)를 읽어서 **데이터 레이어**(`BattleUnitCombatState.IsAttacking`)를 수정한다. 레이어 방향이 역전된 구조다.

```
데이터 레이어         시뮬레이션 레이어      비주얼 레이어
BattleUnitCombatState ← BattleSimulationManager → BattleRuntimeUnit.Animator
                              ↑ 폴링                       ↑ 결과를 State에 반영
```

이 구조가 만드는 문제:
- `BattleUnitCombatState.IsAttacking`이 시뮬레이션 틱 사이에는 신뢰할 수 없다 (Animator 상태와 1틱 차이 발생 가능)
- `BattleFieldView`를 `BattleUnitCombatState` 기반으로 바꿔도 `IsAttacking`의 정확성이 Animator에 여전히 의존한다
- 훈련 환경(배속 ×8)에서 Animator 폴링 비용이 8배로 증폭된다

---

## 해결 방안

### 단계 1 — BattleUnitCombatState에 Position 추가 (sync 방식)

`BattleUnitCombatState`에 `Vector3 Position`을 추가하고, `BattleRuntimeUnit`이 Transform을 이동시킬 때마다 동기화한다. Transform이 이동하는 진입점은 `SetPosition()` 하나뿐이므로 동기화 누락 위험이 낮다.

```csharp
// BattleUnitCombatState 추가
public Vector3 Position { get; private set; }

public void SyncPosition(Vector3 pos) => Position = pos;
```

```csharp
// BattleRuntimeUnit — 수정
public void SetPosition(Vector3 newPosition)
{
    transform.position = newPosition;
    State.SyncPosition(newPosition);
}

// Initialize() 말미에 초기 위치 동기화
public void PlaceAt(Vector3 worldPos, Transform battlefield)
{
    // ... 기존 코드 ...
    State.SyncPosition(transform.position);  // 추가
}
```

### 단계 2 — CurrentTarget / PlannedTargetEnemy를 BattleUnitCombatState 참조로 전환

`BattleRuntimeUnit`이 직접 보유하던 타겟 참조를 `BattleUnitCombatState` 참조로 변경한다. 기존에 `BattleRuntimeUnit` 참조로 저장하던 이유는 `BattleUnitCombatState` 안에서 `BattleRuntimeUnit`을 참조하면 순환이 생기기 때문이었다. `BattleUnitCombatState`끼리 참조하면 순환이 발생하지 않는다.

```csharp
// BattleUnitCombatState 추가
public BattleUnitCombatState CurrentTarget       { get; private set; }
public BattleUnitCombatState PlannedTargetEnemy  { get; private set; }
public BattleUnitCombatState PlannedTargetAlly   { get; private set; }

public void SetCurrentTarget(BattleUnitCombatState target)      => CurrentTarget = target;
public void SetPlannedTargets(BattleUnitCombatState enemy, BattleUnitCombatState ally)
{
    PlannedTargetEnemy = enemy;
    PlannedTargetAlly  = ally;
    CurrentTarget      = enemy;
}
public void ClearTargets()
{
    CurrentTarget = PlannedTargetEnemy = PlannedTargetAlly = null;
}
```

`BattleRuntimeUnit`의 기존 타겟 프로퍼티는 `State`로 위임한다.

```csharp
// BattleRuntimeUnit — 기존 필드 제거 후 State 위임으로 교체
public BattleUnitCombatState PlannedTargetEnemy  => State.PlannedTargetEnemy;
public BattleUnitCombatState PlannedTargetAlly   => State.PlannedTargetAlly;
public BattleUnitCombatState CurrentTarget       => State.CurrentTarget;
```

### 단계 3 — BattleFieldView를 BattleUnitCombatState 기반으로 전환

```csharp
// 변경 전
public sealed class BattleFieldView
{
    private readonly IReadOnlyList<BattleRuntimeUnit> _units;
    public BattleFieldView(IReadOnlyList<BattleRuntimeUnit> units, ...) { ... }
}

// 변경 후
public sealed class BattleFieldView
{
    private readonly IReadOnlyList<BattleUnitCombatState> _units;
    public BattleFieldView(IReadOnlyList<BattleUnitCombatState> units, ...) { ... }
}
```

`BattleSimulationManager`는 초기화 시 `_runtimeUnits`에서 `State` 목록을 추출해 `BattleFieldView`에 넘긴다.

```csharp
// BattleSimulationManager.Initialize() 내부
var states = new List<BattleUnitCombatState>(_runtimeUnits.Count);
foreach (var u in _runtimeUnits) states.Add(u.State);
_fieldView = new BattleFieldView(states, BuildParameterRadii(), escapeTowardTeamBlend);
```

`BattleFieldView` 내부의 `BattleRuntimeUnit` 참조는 전부 `BattleUnitCombatState` 참조로 대체된다. `Position`은 이제 `State.Position`으로 읽는다.

### 단계 4 — 공격 애니메이션 락을 BattleUnitCombatState로 이전

Animator 폴링을 제거하고 duration 기반 카운트다운으로 교체한다.

```csharp
// BattleUnitCombatState 추가
private float _attackLockRemaining;
private float _skillLockRemaining;

public bool IsAttackLocked => _attackLockRemaining > 0f;
public bool IsSkillLocked  => _skillLockRemaining  > 0f;

public void StartAttackLock(float animationDuration)
{
    IsAttacking = true;
    _attackLockRemaining = Mathf.Max(0f, animationDuration);
}

public void StartSkillLock(float animationDuration)
{
    _skillLockRemaining = Mathf.Max(0f, animationDuration);
}

public void TickAttackLock(float deltaTime)
{
    if (_attackLockRemaining <= 0f) return;
    _attackLockRemaining = Mathf.Max(0f, _attackLockRemaining - deltaTime);
    if (_attackLockRemaining <= 0f)
        IsAttacking = false;
}

public void TickSkillLock(float deltaTime)
{
    _skillLockRemaining = Mathf.Max(0f, _skillLockRemaining - deltaTime);
}
```

`BattleRuntimeUnit`은 공격/스킬 트리거 시 Animator에서 클립 길이를 한 번만 읽어 State에 전달한다. 이후 Animator를 다시 참조하지 않는다.

```csharp
// BattleRuntimeUnit.HandleAttackTriggered() 수정
private void HandleAttackTriggered()
{
    _myAnimation?.SetTrigger("attack");

    float lockDuration = GetAttackAnimationDuration();
    State.StartAttackLock(lockDuration);  // Animator 지식을 State에 위임 후 종료

    if (State.PlannedTargetEnemy != null) FaceTarget(State.PlannedTargetEnemy.Position);
    else if (State.CurrentTarget  != null) FaceTarget(State.CurrentTarget.Position);
}

private float GetAttackAnimationDuration()
{
    if (_myAnimation == null) return 0.5f;
    RuntimeAnimatorController ctrl = _myAnimation.runtimeAnimatorController;
    AnimationClip[] clips = ctrl.animationClips;
    for (int i = 0; i < clips.Length; i++)
    {
        if (clips[i].name.Contains("attack"))
            return clips[i].length / Mathf.Max(0.01f, _myAnimation.speed);
    }
    return 0.5f;  // 폴백
}

// BattleRuntimeUnit.SetSkillState() 수정
public void SetSkillState(float animationDuration)
{
    _myAnimation?.SetTrigger("skill");
    State.StartSkillLock(animationDuration);
    if (State.PlannedTargetEnemy != null) FaceTarget(State.PlannedTargetEnemy.Position);
    else if (State.CurrentTarget  != null) FaceTarget(State.CurrentTarget.Position);
}
```

`BattleSimulationManager.TickAllCooldowns()`에서 Animator 폴링 코드를 제거하고 State 틱으로 교체한다.

```csharp
// 변경 전
if (unit.IsAttacking && !unit.IsAttackAnimationPlaying())
    unit.State.SetAttackState(false);

// 변경 후
unit.State.TickAttackLock(tickDeltaTime);
unit.State.TickSkillLock(tickDeltaTime);
```

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| BattleFieldView 의존 대상 | BattleRuntimeUnit (MonoBehaviour) | BattleUnitCombatState (순수 C# 클래스) |
| BattleFieldView 단위 테스트 | Unity 씬 + 12개 MonoBehaviour 필요 | BattleUnitCombatState 인스턴스만으로 테스트 가능 |
| IsAttacking 신뢰성 | 틱 사이에 Animator 상태와 1틱 오차 가능 | State가 duration 카운트다운으로 자체 관리 |
| 배속 ×8 훈련 시 Animator 폴링 | 매 틱 Animator GetCurrentAnimatorStateInfo 호출 | 폴링 없음, float 연산만 |
| 레이어 방향 | 시뮬레이션 → 비주얼 → 데이터 (역전) | 시뮬레이션 → 데이터, 비주얼은 데이터 이벤트 구독 |
| 008 플랜 선행 조건 충족 | 각 System이 MonoBehaviour에 의존 | 각 System이 BattleUnitCombatState만 다룸 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/BattleScene/BattleUnitCombatState.cs`  
  — `Vector3 Position` + `SyncPosition()` 추가  
  — `CurrentTarget`, `PlannedTargetEnemy`, `PlannedTargetAlly` (BattleUnitCombatState 타입) 이전  
  — `StartAttackLock()`, `TickAttackLock()`, `IsAttackLocked` 추가  
  — `StartSkillLock()`, `TickSkillLock()`, `IsSkillLocked` 추가

- `Assets/Scripts/BattleScene/BattleRuntimeUnit.cs`  
  — `SetPosition()`: `State.SyncPosition()` 호출 추가  
  — `PlaceAt()`: 말미에 `State.SyncPosition()` 호출 추가  
  — `PlannedTargetEnemy`, `PlannedTargetAlly`, `CurrentTarget`: State 위임으로 전환  
  — `HandleAttackTriggered()`: `GetAttackAnimationDuration()` 읽어 `State.StartAttackLock()` 호출  
  — `SetSkillState()`: `animationDuration` 파라미터 추가, `State.StartSkillLock()` 호출  
  — `IsAttackAnimationPlaying()`: 제거 (더 이상 사용하지 않음)

- `Assets/Scripts/BattleScene/BattleFieldView.cs`  
  — `IReadOnlyList<BattleRuntimeUnit>` → `IReadOnlyList<BattleUnitCombatState>`로 전환  
  — 내부 모든 `BattleRuntimeUnit` 참조를 `BattleUnitCombatState`로 교체

- `Assets/Scripts/BattleScene/BattleSimulationManager.cs`  
  — `TickAllCooldowns()`: Animator 폴링 제거, `TickAttackLock()` / `TickSkillLock()` 호출로 교체  
  — `Initialize()`: State 목록 추출 후 `BattleFieldView` 생성  
  — `SetSkillState()` 호출부: `animationDuration` 인자 전달

### 영향받는 호출 체인
- `BattleFieldView.FindBestPeelEnemy()` 내부에서 `enemy.CurrentTarget == protectedAlly` 비교 → `BattleUnitCombatState` 비교로 자동 전환
- `IBattleActionPlanner` 구현체들: `BattleRuntimeUnit`으로 타겟을 받지만 내부에서 `BattleFieldView`의 반환값(`BattleUnitCombatState`)을 사용하게 됨 → 플래너 반환 타입(`BattleActionExecutionPlan`) 검토 필요

### BattleActionExecutionPlan 타입 변경 검토
현재 `BattleActionExecutionPlan.TargetEnemy`가 `BattleRuntimeUnit` 타입이라면 `BattleUnitCombatState`로 교체해야 한다. `SetExecutionPlan(plan)` 호출부에서 타겟을 State로 전달하면 된다.

### 위험도
**중간.** 데이터 흐름 방향이 교정되는 리팩터링이지 로직 변경이 아니다. 가장 위험한 지점은 두 곳이다.

1. **Position 동기화 누락**: `SetPosition()` 외에 transform을 직접 이동하는 코드가 있으면 `State.Position`이 stale해진다. 구현 전 `transform.position =`을 전체 검색해 모든 진입점을 확인해야 한다.
2. **공격 락 duration 정확도**: `GetAttackAnimationDuration()`이 잘못된 클립을 반환하면 `IsAttacking`이 실제보다 일찍 또는 늦게 해제된다. 구현 후 플레이 모드에서 공격 애니메이션 도중 이동이 차단되는지, 애니메이션 종료 후 정상 해제되는지 직접 검증이 필요하다.

---

## 구현 단계

### Step 1 — transform.position 직접 할당 진입점 전수 조사
```
검색: transform.position =
대상: Assets/Scripts/BattleScene/ 전체
```
`SetPosition()` 외에 `transform.position`을 직접 쓰는 코드가 있으면 모두 `SetPosition()`으로 교체하거나 말미에 `State.SyncPosition()` 호출을 추가한다. 이 단계를 완료해야 Position 동기화 누락 위험이 사라진다.

### Step 2 — BattleUnitCombatState에 Position 추가
`Vector3 Position`과 `SyncPosition(Vector3)` 메서드를 추가한다. `BattleRuntimeUnit.SetPosition()`과 `PlaceAt()`에 `State.SyncPosition()` 호출을 추가한다.

컴파일 후 `BattleRuntimeUnit.Initialize()` 말미에 `Debug.Assert(State.Position == transform.position)`을 임시로 추가해 초기 동기화를 검증한다.

### Step 3 — 타겟 참조를 BattleUnitCombatState로 이전
`BattleUnitCombatState`에 `CurrentTarget`, `PlannedTargetEnemy`, `PlannedTargetAlly` 필드와 세터를 추가한다. `BattleRuntimeUnit`의 기존 필드를 State 위임으로 교체한다.

`SetExecutionPlan(plan)`과 `ClearExecutionPlan()`에서 타겟 대입 로직을 State 세터 호출로 전환한다.

### Step 4 — BattleFieldView 전환
`BattleFieldView` 생성자의 첫 번째 인자를 `IReadOnlyList<BattleUnitCombatState>`로 변경한다. 내부의 모든 `BattleRuntimeUnit` 참조를 `BattleUnitCombatState`로 교체한다. 컴파일 오류로 누락을 즉시 확인한다.

`BattleSimulationManager.Initialize()`에서 State 목록을 추출해 `BattleFieldView`를 생성한다.

```csharp
var states = new List<BattleUnitCombatState>(_runtimeUnits.Count);
foreach (var u in _runtimeUnits) if (u != null) states.Add(u.State);
_fieldView = new BattleFieldView(states, BuildParameterRadii(), escapeTowardTeamBlend);
```

### Step 5 — BattleActionExecutionPlan 타입 검토 및 수정
`BattleActionExecutionPlan.TargetEnemy`와 `TargetAlly`의 타입을 확인한다. `BattleRuntimeUnit`이면 `BattleUnitCombatState`로 변경하고, 플래너 구현체 7개의 반환 코드를 수정한다.

### Step 6 — 공격 락 duration 기반으로 전환
`BattleUnitCombatState`에 `StartAttackLock()`, `TickAttackLock()`, `IsAttackLocked`를 추가한다.

`BattleRuntimeUnit`에 `GetAttackAnimationDuration()` private 메서드를 추가하고, `HandleAttackTriggered()`에서 호출해 `State.StartAttackLock()`에 전달한다.

`BattleRuntimeUnit.IsAttackAnimationPlaying()`을 제거한다. `BattleSimulationManager.TickAllCooldowns()`의 Animator 폴링 코드를 `unit.State.TickAttackLock(tickDeltaTime)`으로 교체한다.

### Step 7 — 스킬 락 동일 패턴으로 확장
`StartSkillLock()`, `TickSkillLock()`, `IsSkillLocked`를 `BattleUnitCombatState`에 추가한다. `BattleRuntimeUnit.SetSkillState()`에 `animationDuration` 파라미터를 추가하고 `State.StartSkillLock()`을 호출한다. `BattleSimulationManager`의 스킬 실행부에서 `SetSkillState(duration)` 호출 시 클립 길이를 전달한다.

### Step 8 — 회귀 검증
플레이 모드에서 다음을 순서대로 확인한다.

1. 유닛 이동 시 `State.Position`이 `transform.position`과 일치하는지 `Debug.Log`로 확인
2. 공격 애니메이션 재생 중 이동이 차단되는지 확인 (`ExecuteMovementPhase`의 `IsAttacking` 분기)
3. 공격 애니메이션 종료 후 `IsAttacking`이 `false`로 전환되는지 확인
4. `BattleFieldView`의 타겟 탐색 결과가 리팩터링 전과 동일한지 5회 이상 전투로 확인
