# PR#14 — 모범 PR 예시

> **원문**: https://github.com/2026-1-capstone-design/main-game/pull/14  
> **제목**: refactor: BattleSimulationManager 파이프라인 분리

---

## 개요

전투 AI 의사결정 파이프라인(파라미터 계산 → 스코어 평가 → 플랜 빌드 → 스킬 실행)이
`BattleSimulationManager` 1793줄 안에 모두 private 메서드로 얽혀 있던 구조를 분리한다.
각 단계를 입력/출력이 명확한 순수 클래스로 추출하고, 스킬 로직을 `IBattleSkill` 인터페이스 기반으로 데이터 드리븐화한다.

---

## 해결 과제

### 1. 파라미터 계산 로직을 단독으로 검증할 수 없음

`ComputeParametersForUnit()`은 9개의 수식을 순차 계산하는데, 그 수식 자체는 단순하다:

```csharp
// line 976~990 — 입력·출력이 명확한 순수 수식
float isolation = nearestSupportDistance / isolationRadius;
float hpLow = 1f - (enemy.CurrentHealth / enemy.MaxHealth);
float reachFactor = 0.35f + 0.65f * LinearFalloff(distance, assassinReachRadius);
return isolation * (0.6f + 0.4f * hpLow) * reachFactor;
```

그러나 이 수식 하나를 검증하려면:

- `BattleSimulationManager` MonoBehaviour를 Play Mode에서 초기화
- 12개 `BattleRuntimeUnit`을 모두 스폰하고 Initialize
- 한 틱을 실행한 뒤 `unit.CurrentRawParameters.IsolatedEnemyVulnerability` 값 확인

파라미터 수식 하나를 검증하는데 씬 전체가 필요했다.
또한 각 계산 메서드가 내부에서 `_runtimeUnits` 전체 리스트를 순회하므로,
시그니처만 봐서는 어떤 유닛 데이터에 의존하는지 알 수 없었다.

### 2. 스코어 평가와 행동 선택이 분리되지 않음

"어떤 행동을 할 것인가"라는 같은 개념을 4개의 private 메서드가 나눠 처리하고 있었다:

| 메서드                                    | 위치           | 역할                            |
| ----------------------------------------- | -------------- | ------------------------------- |
| `EvaluateActionScores()`                  | line 685~703   | 파라미터 → 점수 계산            |
| `ApplyEscapeReengageBias()`               | line 1297~1314 | 현재 상태에 따른 점수 사후 보정 |
| `CommitOrSwitchActions()`                 | line 300~337   | 점수 기반 행동 전환             |
| `EnsureCurrentActionIsUsableOrFallback()` | line 423~439   | 선택된 행동 실행 가능성 검증    |

중간 상태가 `unit`에 직접 기록되어 "스코어 계산 후 행동 선택" 경계를 독립적으로 테스트할 수 없었다.

### 3. 7개 플랜 빌더가 중복 패턴으로 나열됨

`BuildExecutionPlan()`은 switch로 7개 빌더 메서드를 호출하며, 각 빌더는 동일한 패턴을 반복했다:

```csharp
// BuildAssassinatePlan, BuildDiveBacklinePlan, BuildPeelPlan ... 모두 같은 패턴
BattleRuntimeUnit target = FindBest_____(unit);
return new BattleActionExecutionPlan {
    Action = BattleActionType.___,
    TargetEnemy = target,
    DesiredPosition = target != null ? target.Position : unit.Position,
    HasDesiredPosition = target != null
};
```

새 액션 타입을 추가하려면 SimManager 내 **8군데**를 수정해야 했고,
하나라도 빠지면 런타임 오류 없이 조용히 동작이 틀려졌다.

### 4. 스킬 로직이 이중 switch로 하드코딩됨

스킬 실행 경로가 두 단계 switch로 구성되어 있었다:

```csharp
// 1차 switch: skillType으로 실행 조건 분기 (line 543~574)
switch (Caster.getSkillType())
{
    case skillType.support:
        FindNearestLivingAlly(Caster);   // 반환값을 UseSkill에 전달하지 않음 (버그)
        UseSkill(Caster, null);          // 항상 null로 호출
        break;
}

// 2차 switch: WeaponSkillId로 실제 효과 분기 (line 575~611)
private void UseSkill(BattleRuntimeUnit Caster, BattleRuntimeUnit target)
{
    switch (Caster.getSkill())
    {
        case WeaponSkillId.HeartAttack:
            target.ApplyDamage(20f);
            target.AddKnockback(pushDirection, 50f);
            break;
        case WeaponSkillId.Madness:
            Caster.BuffApply(BuffType.AttackSpeed, 2, 20);
            break;
        default:
        case WeaponSkillId.None:
            Caster.ApplyHeal(10);   // 스킬 없음 = 힐로 기본값 처리
            break;
    }
}
```

새 스킬을 추가할 때마다 SimManager를 직접 수정해야 하고,
스킬 로직을 단독으로 테스트할 방법이 없었다.
또한 `support` 케이스에서 `FindNearestLivingAlly()` 반환값을 버리는 잠재 버그가 내재되어 있었다.

### 5. AI 튜닝 데이터가 코드에 하드코딩됨

반경 값(surroundRadius 등), 행동 전환 관성(commitmentDecayPerSecond), 그리고 7가지 행동에 대한 상세 가중치 데이터가 `BattleSimulationManager` 클래스 내부에 수백 줄의 코드로 하드코딩되어 있었다.
이로 인해 에디터에서 직관적으로 데이터를 변경하기 어려웠다.

---

## 해결 방안

### 핵심 구조 변경

```
[기존]
BattleSimulationManager (1793줄)
  ├── ComputeAllParameters()      // 파라미터 계산 — private, 테스트 불가
  ├── EvaluateAllActionScores()   // 스코어 평가 — private, 테스트 불가
  ├── BuildAllExecutionPlans()    // switch → 7개 빌더 — 수정 시 SimManager 열어야 함
  └── ExecuteSkillPhase()         // switch → 스킬 로직 — 수정 시 SimManager 열어야 함

[변경 후]
BattleSimulationManager (오케스트레이터)
  ├── BattleParameterComputer     // 순수 static 클래스, Edit Mode 테스트 가능
  ├── BattleActionScorer          // 순수 static 클래스, Edit Mode 테스트 가능
  ├── BattleFieldView             // 공간 헬퍼 집약, IBattleActionPlanner에 컨텍스트 제공
  ├── IBattleActionPlanner ×7     // 액션별 플랜 빌더 — SimManager 무관하게 추가
  ├── IBattleSkill (BattleSkillRegistry) // 스킬별 구현체 — SimManager 무관하게 추가
  └── BattleAITuningSO            // 신규: AI 튜닝 데이터 에셋 (ScriptableObject)
```

### Phase A — BattleParameterComputer 분리

`BattleUnitView` (readonly struct)로 RuntimeUnit 스냅샷을 캡처하고,
`BattleParameterRadii`로 반경 상수를 묶어 순수 정적 클래스에 전달한다.

```csharp
public static class BattleParameterComputer
{
    public static BattleRawParameterSet Compute(
        BattleUnitView self,
        IReadOnlyList<BattleUnitView> allies,
        IReadOnlyList<BattleUnitView> enemies,
        BattleParameterRadii radii)
    { ... }
}
```

### Phase B — BattleActionScorer 분리

```csharp
public static class BattleActionScorer
{
    public static BattleActionScoreSet Evaluate(
        BattleRawParameterSet raw,
        BattleModifiedParameterSet modified,
        IReadOnlyList<BattleActionTuning> tunings,
        WeaponType weaponType)
    { ... }
}
```

### Phase C — IBattleActionPlanner

```csharp
public interface IBattleActionPlanner
{
    BattleActionType ActionType { get; }
    BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field);
    bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field);
}
```

SimManager는 `Dictionary<BattleActionType, IBattleActionPlanner>`로 플랜너를 보유하고 위임만 수행한다.
새 액션 타입 추가 시 구현 클래스 1개 추가 + 등록이 전부다.

### Phase D — IBattleSkill 스킬 시스템

```csharp
public interface IBattleSkill
{
    WeaponSkillId SkillId { get; }
    skillType SkillCategory { get; }
    bool CanActivate(BattleUnitCombatState caster, BattleContext context);
    void Apply(BattleUnitCombatState caster, BattleContext context, ISkillEffectApplier applier);
}

// ExecuteSkillPhase — switch 제거, 3-line 루프
private void ExecuteSkillPhase()
{
    foreach (var unit in ActiveUnits())
    {
        if (unit.State.SkillCooldownRemaining > 0f) continue;
        IBattleSkill skill = _skillRegistry.Get(unit.State.SkillId);
        if (skill == null || !skill.CanActivate(unit.State, _context)) continue;
        skill.Apply(unit.State, _context, _effectApplier);
        unit.SetSkillState();
        unit.State.ResetSkillCooldown();
    }
}
```

`ISkillEffectApplier`가 데미지·넉백·힐·버프 적용을 추상화하므로 SimManager 없이 스킬 로직만 테스트할 수 있다.

**버그 수정**: `support` 케이스에서 `FindNearestLivingAlly(Caster)` 반환값을 버리던 코드가 구조적으로 제거된다.
각 스킬이 `Apply()`에서 타겟을 직접 결정하므로 반환값 미사용 버그가 재현 불가 상태가 된다.

### Phase E — BattleAITuningSO 분리 (데이터 중심 AI 관리)

`BattleSimulationManager` 내부에 하드코딩되어 있던 각종 가중치와 반경 값을 `ScriptableObject`로 분리했다.
- **데이터 구조화**: 행동 전환 관성, 8종의 파라미터 반경, 7종의 행동별 상세 가중치를 에셋으로 관리한다.
- **실시간 조정**: 에디터의 Play Mode에서도 SO 에셋을 수정하여 AI 성향을 즉시 테스트할 수 있다.
- **초기화 로직**: `Initialize` 시점에 SO 에셋이 할당되었는지 확인하고, 누락된 경우에만 기본 튜닝을 생성하도록 보강했다.

---

## 기대 효과

### 단위 테스트 가능

```csharp
// BattleParameterComputer — Unity, 씬, 프리팹 모두 필요 없음
[Test]
public void IsolatedEnemyVulnerability_SingleEnemyNoSupport_MaxScore()
{
    var self = new BattleUnitView { Position = Vector3.zero, IsEnemy = false };
    var enemy = new BattleUnitView { Position = new Vector3(100f, 0, 0), IsEnemy = true, CurrentHealth = 50f, MaxHealth = 100f };
    var radii = new BattleParameterRadii { isolationRadius = 450f, assassinReachRadius = 600f };

    var result = BattleParameterComputer.Compute(self, allies: Array.Empty<BattleUnitView>(), new[] { enemy }, radii);

    Assert.Greater(result.IsolatedEnemyVulnerability, 0.5f);
}

// HeartAttackSkill — Play Mode 없이 실행 가능
[Test]
public void HeartAttack_AppliesDamageAndKnockback()
{
    var caster = MakeUnit(position: Vector3.zero);
    var target = MakeUnit(position: new Vector3(100f, 0, 0), health: 100f);
    var applier = new TestSkillEffectApplier();

    new HeartAttackSkill().Apply(caster.State, MakeContext(caster, target), applier);

    Assert.AreEqual(20f, applier.DamageDealt);
    Assert.Greater(applier.KnockbackForce, 0f);
}
```

### 확장 지점 명확화

| 추가 대상          | 기존                              | 변경 후                                               |
| ------------------ | --------------------------------- | ----------------------------------------------------- |
| 새 스킬            | SimManager `UseSkill` switch 수정 | `IBattleSkill` 구현 클래스 1개 추가 + 레지스트리 등록 |
| 새 액션 타입       | SimManager 내 8군데 수정          | `IBattleActionPlanner` 구현 클래스 1개 추가 + 등록    |
| 파라미터 수식 변경 | SimManager 내 private 메서드 수정 | `BattleParameterComputer` 수정, 단독 테스트로 검증    |
| AI 튜닝 파라미터   | SimManager 내 하드코딩된 값 수정  | `BattleAITuningSO` 에셋에서 실시간 조정 및 저장       |

### SimManager 역할 축소

약 1793줄의 단일 MonoBehaviour에서 파이프라인 각 단계를 외부 컴포넌트에 위임하는 오케스트레이터로 역할이 축소된다.

### #13 이후 시너지

분리된 클래스들은 `BattleRuntimeUnit` 대신 순수 데이터(`BattleUnitView`, `BattleUnitCombatState`)를 입력으로 받는다.
#13 (RuntimeUnit 상태/비주얼 분리)과 함께 진행할 때 입력 타입이 자연스럽게 맞물린다.

---

## 영향 범위

| 파일                               | 변경 유형                                                                              |
| ---------------------------------- | -------------------------------------------------------------------------------------- |
| `BattleSimulationManager.cs`       | 파이프라인 각 단계를 외부 컴포넌트에 위임, `UseSkill` 삭제, `ISkillEffectApplier` 구현 |
| `BattleAIDecisionContracts.cs`     | `BattleParameterRadii`, `BattleUnitView` struct 추가                                   |
| 신규: `BattleParameterComputer.cs` | 순수 static 클래스, 파라미터 9개 계산                                                  |
| 신규: `BattleActionScorer.cs`      | 순수 static 클래스, 스코어 계산                                                        |
| 신규: `BattleFieldView.cs`         | 공간 헬퍼 집약, 플랜너 컨텍스트 역할                                                   |
| 신규: `IBattleActionPlanner.cs`    | 플랜 빌더 인터페이스                                                                   |
| 신규: `BattlePlanners/*.cs`        | 액션별 플랜 빌더 구현 7개                                                              |
| 신규: `IBattleSkill.cs`            | 스킬 인터페이스 + `ISkillEffectApplier`                                                |
| 신규: `BattleSkills/*.cs`          | 스킬 구현체 (HeartAttack, Madness, DefaultHeal)                                        |
| 신규: `BattleSkillRegistry.cs`     | `WeaponSkillId` → `IBattleSkill` 매핑                                                  |
| 신규: `BattleAITuningSO.cs`        | AI 튜닝 데이터 에셋 정의                                                               |
