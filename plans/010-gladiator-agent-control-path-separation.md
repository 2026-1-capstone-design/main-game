# 010. GladiatorAgent ↔ BattleSimulationManager 제어 경로 분리

## 문제 상황

ML-Agents 제어 경로와 내장 AI 경로는 `BattleRuntimeUnit.IsExternallyControlled` 플래그 하나로 분기된다. 이 구조가 만드는 문제는 세 가지다.

### 1. 관찰값(Observation)이 Unit 내부 상태에 직접 결합

```csharp
// GladiatorAgent.cs — CollectObservations 내부
sensor.AddObservation(_selfUnit.AttackRange);        // 비정규화 raw 값
sensor.AddObservation(_selfUnit.CurrentHealth);
sensor.AddObservation(relativePos.x);               // 월드 좌표 델타
```

`CollectObservations()`는 `BattleRuntimeUnit`을 직접 참조하여 85개 float을 조립한다. 관찰 공간을 단위 테스트하려면 실제 `BattleRuntimeUnit` MonoBehaviour 인스턴스가 필요하다. 관찰 슬롯의 의미(인덱스 0 = arena 상대좌표 x, 인덱스 7 = 공격 쿨타임…)는 코드를 처음 보는 사람이 읽기 어렵고, 슬롯이 바뀌면 신경망을 처음부터 다시 학습해야 한다.

### 2. 보상(Reward)이 이벤트 핸들러에 하드코딩

```csharp
private void HandleDamageTaken(float damage) { AddReward(-0.1f); }
private void HandleAttackLanded(BattleRuntimeUnit target, bool wasKill)
{
    AddReward(10f);
    if (wasKill) { AddReward(100f); EndEpisode(); }
}
```

보상 스케일을 변경하려면 코드를 수정하고 재컴파일해야 한다. 팀킬 패널티, 생존 시간 보상, 거리 shaping 계수 등이 분산되어 있어 어떤 보상 항목이 존재하는지 한눈에 파악하기 어렵다.

### 3. `IsExternallyControlled` 플래그로 인한 암묵적 분기

`BattleSimulationManager`는 각 유닛을 순회할 때 `IsExternallyControlled`를 확인해 AI 결정을 건너뛴다. 이 검사가 빠진 신규 파이프라인 단계가 추가되면, ML-Agents 제어 유닛도 내장 AI의 영향을 받는 조용한 버그가 생긴다.

---

## 해결 방안

### 단계 1 — ObservationBuilder 추출

```csharp
// 신규: BattleObservationBuilder.cs
public static class BattleObservationBuilder
{
    public readonly struct Snapshot
    {
        // 자신 (8 floats)
        public readonly Vector2 ArenaCenterOffset;
        public readonly float Health, MaxHealth, Attack, AttackRange, MoveSpeed, AttackCooldown;

        // 슬롯 배열 (5 teammate + 6 opponent = 11 × 7 floats)
        public readonly UnitSlotObservation[] TeammateSlots;  // length 5
        public readonly UnitSlotObservation[] OpponentSlots;  // length 6
    }

    public static Snapshot Build(
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> allUnits,
        Vector3 arenaCenter);
}
```

`GladiatorAgent.CollectObservations()`는 `Snapshot`을 받아 순서대로 `sensor.AddObservation()`만 호출한다. `Snapshot`은 순수 데이터 구조이므로 MonoBehaviour 없이 단위 테스트 가능하다.

```csharp
// 리팩터링 후 CollectObservations
public override void CollectObservations(VectorSensor sensor)
{
    var snap = BattleObservationBuilder.Build(_selfUnit, _allUnits, _arenaCenter);
    snap.WriteTo(sensor);   // Snapshot이 순서대로 AddObservation 처리
}
```

### 단계 2 — RewardConfig ScriptableObject 도입

```csharp
// 신규: GladiatorRewardConfig.cs (ScriptableObject)
[CreateAssetMenu]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    public float damageTakenScale  = -0.1f;
    public float attackLandedBase  =  10f;
    public float killBonus         = 100f;
    public float deathPenalty      =  -5f;
    public float distanceShaperMin =  -0.01f;
    public float distanceShaperMax =   0.01f;
    public float boundaryPenalty   =  -0.5f;
}
```

`GladiatorAgent`는 `GladiatorRewardConfig`를 Inspector에서 주입받는다. 보상 계수 변경 시 ScriptableObject Asset만 수정하면 되고, 서로 다른 보상 구성을 여러 에이전트에 적용해 A/B 비교가 가능해진다.

### 단계 3 — IUnitController 인터페이스로 플래그 제거

```csharp
public interface IUnitController
{
    void OnDecisionRequested(BattleRuntimeUnit unit, BattleFieldView field);
}

public sealed class BuiltInAIController : IUnitController { ... }
public sealed class MLAgentController   : IUnitController { ... }
```

`BattleRuntimeUnit`은 `IUnitController` 하나를 가지며, `IsExternallyControlled` 플래그를 제거한다. `BattleSimulationManager`는 `unit.Controller.OnDecisionRequested()`를 호출하면 분기 없이 처리된다.

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| Observation 슬롯 단위 테스트 | `BattleRuntimeUnit` MonoBehaviour 필요 | `BattleObservationBuilder.Build()`에 stub 주입으로 테스트 |
| 보상 계수 변경 | 코드 수정 + 재컴파일 | ScriptableObject Inspector 수정만으로 적용 |
| 새 파이프라인 단계에서 플래그 누락 버그 | 발생 가능 | `IUnitController` 패턴으로 분기 자체를 제거 |
| 보상 항목 목록 파악 | 이벤트 핸들러 전체 검색 | `GladiatorRewardConfig` 파일 하나에서 확인 |
| 내장 AI ↔ ML-Agents 혼합 전투 테스트 | 플래그 수동 설정 | Controller 교체만으로 유닛별 제어 방식 지정 |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/TrainingScripts/GladiatorAgent.cs`  
  — `CollectObservations` 간소화, `AddReward` 호출을 Config 참조로 교체, Controller 패턴 연동
- `Assets/Scripts/BattleScene/BattleRuntimeUnit.cs`  
  — `IsExternallyControlled` 플래그 → `IUnitController Controller` 프로퍼티로 교체
- `Assets/Scripts/BattleScene/BattleSimulationManager.cs`  
  — 플래그 분기 제거, `unit.Controller.OnDecisionRequested()` 호출로 교체

### 신규 생성 파일
```
Assets/Scripts/TrainingScripts/
  BattleObservationBuilder.cs
  GladiatorRewardConfig.cs
Assets/Scripts/BattleScene/
  IUnitController.cs
  BuiltInAIController.cs
  MLAgentController.cs
```

### 영향받는 씬 / 에셋
- `TrainingScene.unity` — `GladiatorAgent` Inspector에 `GladiatorRewardConfig` Asset 연결 필요
- `BattleTestPreset.asset` — 변경 없음

### 위험도
**높음.** `BattleRuntimeUnit`의 `IsExternallyControlled` 제거는 이를 참조하는 모든 코드를 수정해야 한다. `TrainingBootstrapper`도 에이전트-유닛 연결 방식을 Controller 주입으로 바꿔야 한다. 단계적으로 진행한다면 `IsExternallyControlled`를 deprecated 래퍼로 남기고 내부에서 Controller null 체크로 위임하는 방식으로 점진 마이그레이션이 가능하다.

---

## 구현 단계

### Step 1 — GladiatorRewardConfig ScriptableObject 생성
`Assets/Scripts/TrainingScripts/GladiatorRewardConfig.cs`를 생성한다. 해결 방안의 필드 목록을 그대로 구현하고 `[CreateAssetMenu]`를 붙인다. `Assets/Configs/` 아래에 `GladiatorRewardConfig.asset`을 생성하여 기존 하드코딩 값(`-0.1f`, `10f`, `100f`, `-5f`)을 기본값으로 설정한다.

### Step 2 — GladiatorAgent 보상 로직을 Config 참조로 교체
`GladiatorAgent`에 `[SerializeField] GladiatorRewardConfig rewardConfig;` 필드를 추가한다. `HandleDamageTaken`, `HandleAttackLanded`, `HandleSelfDied` 이벤트 핸들러 안의 리터럴 숫자를 `rewardConfig.damageTakenScale` 등으로 교체한다. Inspector에서 생성한 Asset을 연결하고 플레이 모드에서 기존과 동일하게 동작하는지 확인한다.

### Step 3 — IUnitController 인터페이스 정의
`Assets/Scripts/BattleScene/IUnitController.cs`를 생성한다. 시그니처는 `void OnDecisionRequested(BattleRuntimeUnit unit, BattleFieldView field)` 하나로 최소화한다.

### Step 4 — BuiltInAIController 구현
`BattleSimulationManager`에서 유닛당 행동 결정 로직(`IsExternallyControlled` 분기 내부의 AI 경로)을 `BuiltInAIController.OnDecisionRequested()`로 이동한다. 이 클래스는 `BattleAITuningSO` 참조가 필요하므로 생성자에서 주입받는다.

### Step 5 — MLAgentController 구현
`MLAgentController`는 `OnDecisionRequested()`에서 아무것도 하지 않는다(ML-Agents가 `OnActionReceived()`를 통해 직접 유닛을 제어하기 때문). 빈 구현이지만 분기를 제거하는 역할을 한다.

### Step 6 — BattleRuntimeUnit에 Controller 프로퍼티 추가 (점진 방식)
`BattleRuntimeUnit`에 `public IUnitController Controller { get; private set; }` 프로퍼티를 추가한다. `IsExternallyControlled`를 즉시 제거하지 않고, 내부를 `Controller is MLAgentController`로 위임하는 deprecated 래퍼로 바꾼다. 이렇게 하면 참조 코드를 점진적으로 교체할 수 있다.

```csharp
[System.Obsolete("Use Controller instead")]
public bool IsExternallyControlled => Controller is MLAgentController;

public void SetController(IUnitController controller) => Controller = controller;
```

### Step 7 — BattleSimulationManager 플래그 분기 제거
`IsExternallyControlled` 검사를 `unit.Controller.OnDecisionRequested(unit, _fieldView)` 호출로 교체한다. 플래너 딕셔너리와 AI 결정 로직은 `BuiltInAIController`로 이미 이동되어 있으므로, Orchestrator에서는 Controller 호출만 남는다.

### Step 8 — TrainingBootstrapper 업데이트
`_selfUnit.SetExternallyControlled(true)` 호출을 `_selfUnit.SetController(new MLAgentController())` 로 교체한다. 내장 AI 유닛에는 `SetController(new BuiltInAIController(aiTuning))` 을 설정한다.

### Step 9 — IsExternallyControlled 완전 제거
모든 참조가 Controller 기반으로 전환됐는지 컴파일러 경고로 확인한 뒤, deprecated 래퍼와 `IsExternallyControlled`를 삭제한다.

### Step 10 — TrainingScene Inspector 연결 및 검증
TrainingScene을 열고 `GladiatorAgent` Inspector에 `GladiatorRewardConfig.asset`을 연결한다. ML-Agents `mlagents-learn`으로 짧은 훈련 세션을 실행해 보상이 정상적으로 기록되는지 TensorBoard에서 확인한다.
