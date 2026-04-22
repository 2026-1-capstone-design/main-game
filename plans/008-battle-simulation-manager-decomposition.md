# 008. BattleSimulationManager God Object 분해

## 문제 상황

`BattleSimulationManager.cs`는 약 900줄 단일 클래스 안에 전투 시뮬레이션의 전 단계를 담당한다.

```
BattleSimulationManager
  ├─ TickAllCooldowns          — 쿨타임 틱 감소
  ├─ ComputeAllParameters      — RAW 파라미터 9개 계산
  ├─ EvaluateAllActionScores   — 행동 점수 계산 (무기 타입 배율 포함)
  ├─ CommitOrSwitchActions     — commitment decay · 행동 유지/전환
  ├─ BuildAllExecutionPlans    — 플래너별 타겟 · 이동 목표 선택
  ├─ ExecuteSpecialEffect      — 넉백 · 버프 틱
  ├─ ExecuteMovementPhase      — 이동 처리 (경로 추적)
  ├─ ResolveUnitSeparation     — 충돌 반경 밀어내기
  ├─ ExecuteAttackPhase        — 사거리 · 쿨타임 확인 후 대미지 적용
  ├─ ExecuteSkillPhase         — 스킬 쿨타임 확인 후 스킬 발동
  └─ TryFinishBattle           — 승패 판정 · 보상 계산
```

### 핵심 문제점

1. **테스트 불가능한 commitment 로직**  
   `CommitOrSwitchActions`는 exponential decay를 적용한 뒤 임계값 비교로 행동 전환 여부를 결정한다. 이 로직 하나를 검증하려면 12개 `BattleRuntimeUnit`, `BattleFieldView`, tuning ScriptableObject 전부를 준비해야 한다. 파라미터 하나가 잘못되어도 어느 단계에서 문제가 생겼는지 추적하기 어렵다.

2. **파라미터 modifier 오버플로 무음 버그**  
   `ApplyCurrentActionParameterModifiers()`(line 316)는 파라미터를 수정한 뒤 호출부에서 `Clamp01`이 적용된다. modifier 비율이 200%이면 clamp가 숫자를 덮어써서 실제 overflow가 발생했는지 알 수 없다.

3. **UI·시뮬레이션 결합**  
   `BattleStatusGridUIManager`와 `BattleSceneUIManager`가 `Initialize()` 파라미터로 직접 주입되어 시뮬레이션 루프(`StepSimulation`) 안에서 UI 갱신이 호출된다. UI 없이 시뮬레이션만 실행하는 게 구조적으로 불가능하다.

4. **ISkillEffectApplier 책임 혼재**  
   `BattleSimulationManager`가 `ISkillEffectApplier`를 직접 구현(lines 670-681)하여 스킬 이펙트 적용 로직이 시뮬레이션 루프 안에 묻혀 있다.

---

## 해결 방안

### 단계 1 — 파이프라인 단계별 클래스 추출

| 새 클래스 | 책임 | 주요 입출력 |
|---|---|---|
| `BattleCooldownSystem` | 쿨타임 틱 감소 | `IReadOnlyList<BattleRuntimeUnit>` → (side-effect only) |
| `BattleParameterSystem` | RAW 파라미터 + modifier 보정 | units, field, radii → `BattleRawParameters[]` |
| `BattleDecisionSystem` | 점수 계산 + commitment decay + 행동 전환 | parameters, tuning → `BattleActionType[]` |
| `BattlePlanningSystem` | 플래너별 ExecutionPlan 빌드 | decision results, field → `BattleActionExecutionPlan[]` |
| `BattlePhysicsSystem` | 이동 · separation · 넉백 | plans, units, collider → (side-effect only) |
| `BattleCombatSystem` | 공격 · 스킬 · 대미지 적용 | plans, units → `BattleCombatResult[]` |
| `BattleVictorySystem` | 승패 판정 · 보상 계산 | units, result → `BattleOutcome?` |

`BattleSimulationManager`는 이 시스템들을 순서대로 호출하는 **Orchestrator**로 줄어든다.

```csharp
// 리팩터링 후 StepSimulation 의사 코드
void StepSimulation()
{
    _cooldownSystem.Tick(_runtimeUnits, deltaTime);
    var rawParams = _parameterSystem.Compute(_runtimeUnits, _fieldView);
    var decisions = _decisionSystem.Decide(rawParams, _runtimeUnits, _fieldView);
    var plans     = _planningSystem.Build(decisions, _runtimeUnits, _fieldView);
    _physicsSystem.Execute(plans, _runtimeUnits, _battlefieldCollider, deltaTime);
    var combatResults = _combatSystem.Execute(plans, _runtimeUnits);
    var outcome = _victorySystem.Evaluate(_runtimeUnits, combatResults);

    OnSimulationTicked?.Invoke(new SimulationTickData(rawParams, decisions, combatResults));
    if (outcome.HasValue) OnBattleFinished?.Invoke(outcome.Value);
}
```

### 단계 2 — UI 의존성 이벤트 기반으로 교체

```csharp
// 현재 (직접 참조)
_statusGridUIManager.Refresh();

// 리팩터링 후 (이벤트)
OnSimulationTicked?.Invoke(tickData);
// BattleStatusGridUIManager가 이 이벤트를 구독하여 자체 Refresh()
```

### 단계 3 — ISkillEffectApplier 분리

`SkillEffectApplier` 독립 클래스를 만들고 `BattleCombatSystem`이 이를 주입받는다.

---

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| `CommitOrSwitchActions` 단위 테스트 | 12유닛 + FieldView 풀 셋업 필요 | `BattleDecisionSystem`에 mock parameters 주입만으로 테스트 |
| 파라미터 modifier 오버플로 추적 | clamp로 무음 처리 | `BattleParameterSystem` 출력값에 assert 삽입 가능 |
| ML-Agents 훈련 시 UI 비활성화 | 구조적으로 불가 | Orchestrator에서 UI 이벤트 구독 생략으로 해결 |
| 새 파이프라인 단계 추가 | ~900줄 파일 수정 | 새 System 클래스 추가 후 Orchestrator에 삽입 |
| 코드 가독성 | 11단계가 한 파일에 산재 | 파일명 = 책임 (BattleDecisionSystem.cs 등) |

---

## 영향 범위

### 수정 대상 파일
- `Assets/Scripts/BattleScene/BattleSimulationManager.cs` — 대폭 축소 (Orchestrator만 남음)
- `Assets/Scripts/TrainingScripts/TrainingBootstrapper.cs` — `IsBattleFinished` 폴링 유지, `ForceFinishBattle()` 유지 (public API 보존)
- `Assets/Scripts/TrainingScripts/GladiatorAgent.cs` — 변경 없음 (Unit 이벤트 구독만 사용)

### 신규 생성 파일
```
Assets/Scripts/BattleScene/Simulation/
  BattleCooldownSystem.cs
  BattleParameterSystem.cs
  BattleDecisionSystem.cs
  BattlePlanningSystem.cs
  BattlePhysicsSystem.cs
  BattleCombatSystem.cs
  BattleVictorySystem.cs
  SimulationTickData.cs      ← 이벤트 페이로드 DTO
  BattleOutcome.cs           ← 승패 결과 DTO
```

### 영향받는 외부 구독자
- `BattleSceneUIManager` — `OnBattleFinished` 이벤트 구독으로 전환
- `BattleStatusGridUIManager` — `OnSimulationTicked` 이벤트 구독으로 전환
- `BattleSceneFlowManager` — `Initialize()` 시그니처에서 UI 파라미터 제거 가능

### 위험도
**중간.** public API(`Initialize`, `ForceFinishBattle`, `IsBattleFinished`, `RuntimeUnits`)는 보존된다. 내부 로직만 분리되므로 씬(Scene) 설정 변경은 최소화된다. Unity Inspector 직렬화 필드(`simulationTickRate`, `aiTuning` 등)는 Orchestrator에 그대로 유지된다.

---

## 구현 단계

### Step 1 — DTO 파일 생성
`Assets/Scripts/BattleScene/Simulation/` 디렉토리를 생성하고 데이터 구조를 먼저 정의한다. 이후 단계에서 모든 System이 이 타입을 참조하므로 가장 먼저 작성해야 한다.

- `BattleCombatResult.cs` — 한 틱에서 발생한 공격 이벤트 목록 (공격자, 피격자, 대미지, 킬 여부)
- `SimulationTickData.cs` — `OnSimulationTicked` 이벤트 페이로드 (rawParams 배열, decisions 배열, combatResults)
- `BattleOutcome.cs` — 승패 결과 (승자 팀, 종료 틱, 생존 유닛 목록)

### Step 2 — BattleCooldownSystem 추출
`BattleSimulationManager.TickAllCooldowns()`를 그대로 잘라내어 `BattleCooldownSystem.Tick(IReadOnlyList<BattleRuntimeUnit> units, float deltaTime)`로 이동한다. `BattleSimulationManager`에서는 `_cooldownSystem.Tick(_runtimeUnits, deltaTime)` 한 줄로 대체한다. 컴파일 통과 확인 후 다음 단계로 진행한다.

### Step 3 — BattleParameterSystem 추출
`ComputeAllParameters()`와 `ApplyCurrentActionParameterModifiers()`를 `BattleParameterSystem.Compute()`로 이동한다. 반환형은 `BattleRawParameters[]`(또는 기존 타입)로 하고, modifier 적용 결과를 별도 배열로 반환하여 overflow 여부를 호출부에서 assert할 수 있게 한다.

### Step 4 — BattleDecisionSystem 추출
`EvaluateAllActionScores()`와 `CommitOrSwitchActions()`를 `BattleDecisionSystem.Decide()`로 이동한다. commitment 상태(`_commitmentValues` 등 private 필드)를 DecisionSystem이 소유하도록 옮긴다. 이 단계가 가장 많은 내부 상태 이동을 수반하므로 시간을 충분히 확보한다.

### Step 5 — BattlePlanningSystem 추출
`BuildAllExecutionPlans()`를 `BattlePlanningSystem.Build()`로 이동한다. 플래너 딕셔너리(`_planners`)를 PlanningSystem이 소유한다. `BattleSimulationManager`의 `Initialize()`에서 플래너 등록 코드도 PlanningSystem 생성자로 이동한다.

### Step 6 — BattlePhysicsSystem 추출
`ExecuteMovementPhase()`, `ResolveUnitSeparation()`, `ExecuteSpecialEffect()`(넉백 부분)를 `BattlePhysicsSystem.Execute()`로 이동한다. `_battlefieldCollider` 참조를 PhysicsSystem 생성자에서 주입받는다.

### Step 7 — BattleCombatSystem 및 SkillEffectApplier 분리
`ExecuteAttackPhase()`와 `ExecuteSkillPhase()`를 `BattleCombatSystem.Execute()`로 이동한다. `ISkillEffectApplier` 구현을 `SkillEffectApplier` 독립 클래스로 추출하고, CombatSystem 생성자에서 주입한다. `BattleSimulationManager`에서 `ISkillEffectApplier` 구현 코드를 제거한다.

### Step 8 — BattleVictorySystem 추출
`TryFinishBattle()`을 `BattleVictorySystem.Evaluate()`로 이동한다. 반환형을 `BattleOutcome?`으로 하여 전투 미종료 시 null을 반환한다.

### Step 9 — UI 이벤트 기반으로 전환
`BattleSimulationManager`에 `OnSimulationTicked` 및 `OnBattleFinished` 이벤트를 추가한다. `StepSimulation()` 말미에 이벤트를 발행한다. `BattleStatusGridUIManager`와 `BattleSceneUIManager`가 각각 이 이벤트를 구독하도록 수정한다. `Initialize()` 파라미터에서 두 UI 매니저를 제거한다.

### Step 10 — Orchestrator 최종 정리 및 회귀 검증
`BattleSimulationManager.StepSimulation()`이 해결 방안의 의사 코드와 일치하는지 확인한다. 파일 길이가 250줄 이하로 줄었는지 확인한다. Unity Editor에서 BattleScene과 TrainingScene을 각각 실행해 전투가 정상 진행되는지 확인한다.
