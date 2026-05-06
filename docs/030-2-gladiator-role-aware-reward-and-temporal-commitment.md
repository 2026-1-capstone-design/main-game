# 030. Gladiator 역할 기반 보상 및 시간적 목표 지속성 강화

## 개요

(선행: 030-1 완료 후 시작)  
관측과 액션 계약만 바뀌어서는 팀 뭉침 완화와 전략 분화가 자동으로 따라오지 않는다. 이 플랜은 `030-1`에서 추가한 역할/anchor 상태를 활용해 reward와 metrics를 역할 단위로 정렬하고, “조금 시도하고 바꾸는” 정책보다 “끝까지 수행하는” 정책이 이득이 되도록 shaping을 재설계하는 것을 목표로 한다.

## 문제 상황

### 현재 tactical reward의 범위가 좁다

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorTacticalRewardShaper.cs
if (approachDelta > 0f)
{
    reward += approachDelta * AnchorApproachReward;
}

if (features.AnchorVisibility > 0.5f)
{
    reward += AnchorVisibilityReward;
}

if (
    (
        action.PathMode == GladiatorActionSchema.PathModeFlankLeft
        || action.PathMode == GladiatorActionSchema.PathModeFlankRight
    )
    && Mathf.Abs(action.RelativeMove.x) > 0.1f
)
{
    reward += FlankReward * Mathf.Clamp01(1f - features.EnemyClusterPressure);
}
```

**문제 1 — 접근/재포착/우회/peel은 있으나 역할별 성공 정의가 없다**  
현재 tactical reward는 anchor 접근과 flank, peel의 약한 shaping만 갖고 있다. `engage`, `peel`, `assassinate`, `regroup` 각각에 대해 어떤 상태가 성공인지 분리되어 있지 않다.

**문제 2 — 추격 성공이 킬 전환까지 이어지지 않는다**  
도주 적을 쫓는 동안 거리가 조금씩 줄면 보상을 받지만, 끝내 때리지 못해도 중간 보상만 쌓일 수 있다. 이는 “쫓는 척만 하는” 정책을 허용한다.

### 현재 penalty 구조는 유지 비용만 있고 완료 보상이 약하다

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorRewardEvaluator.cs
private float EvaluateTargetSwitch(GladiatorAgentTacticalContext context)
{
    if (
        !context.HasValidTarget
        || context.PreviousTargetSlot < 0
        || context.TargetSlot == context.PreviousTargetSlot
    )
    {
        return 0f;
    }

    return _config.targetSwitchPenalty;
}

private float EvaluateStanceSwitch(GladiatorAgentTacticalContext context)
{
    if (context.PreviousStance < 0 || context.Stance == context.PreviousStance)
    {
        return 0f;
    }

    return _config.stanceSwitchPenalty;
}
```

**문제 3 — 바꾸는 비용은 있지만 유지하는 보상은 거의 없다**  
현재는 switch penalty가 있어도, 같은 목표를 유지하며 유의미하게 진행하는 것에 대한 time-consistent reward가 부족하다. 결과적으로 “덜 자주 바꾸는 정책”은 생길 수 있어도 “끝까지 수행하는 정책”은 약하다.

### 현재 reward config는 역할별 파라미터를 담지 못한다

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorRewardConfig.cs
public float targetSwitchPenalty = -0.01f;
public float stanceSwitchPenalty = -0.01f;
public float damageDealtRatio = 1f;
public float damageTakenRatio = -1f;
public float attackLanded = 0.05f;
public float kill = 3f;
```

**문제 4 — 모든 역할이 거의 같은 보상 축을 공유한다**  
추격, 보조, 암살, 재정렬은 서로 다른 실패/성공 구조를 갖는데, 현재 config는 역할 중립적이다. 역할 분화를 원한다면 config도 역할-aware해야 한다.

## 해결 방안

### 단계 1 — 역할별 shaping 인터페이스 도입

```csharp
public interface IGladiatorRoleRewardRule
{
    float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features);
}
```

`GladiatorTacticalRewardShaper` 내부를 역할별 rule 집합으로 재구성한다. 하나의 메서드에서 모든 역할을 처리하지 않고, 역할별 성공 조건을 분리한다.

### 단계 2 — commitment reward와 abort penalty를 컨텍스트에 추가

```csharp
public readonly struct GladiatorAgentTacticalContext
{
    public readonly int AnchorCommitmentSteps;
    public readonly int RoleCommitmentSteps;
    public readonly bool BrokeCommitmentEarly;
    public readonly bool CompletedRoleWindow;
}
```

anchor/role를 충분한 시간 유지했는지, 중간에 조기 이탈했는지, 유지 후 성과를 냈는지를 reward 계산 시 직접 사용할 수 있게 한다.

### 단계 3 — 역할별 성공 정의를 명시

```csharp
private float EvaluateAssassinate(...)
{
    float reward = 0f;
    reward += lowHpTargetDistanceDelta * _config.assassinateApproachReward;
    reward += targetEliminated ? _config.assassinateFinishReward : 0f;
    reward += brokeOffTooEarly ? _config.assassinateAbortPenalty : 0f;
    return reward;
}
```

각 역할마다 중간 목표와 완료 목표를 분리한다.

- `Engage`: 유효 target 유지, 공격 기회 활용, 피해 교환 우위
- `Peel`: 보호 대상 아군에 대한 enemy focus 감소, ally 생존 유지
- `Assassinate`: 고립/저체력 적 접근, escape 차단, 빠른 마무리
- `Regroup`: 짧은 시간 내 안전 재정렬 후 교전 재진입

### 단계 4 — regroup를 짧은 회복 역할로 제한

```csharp
if (action.Role == GladiatorActionSchema.RoleRegroup && context.RoleCommitmentSteps > _config.regroupWindowSteps)
{
    reward += _config.regroupOverstayPenalty;
}
```

현재 뭉침 문제의 핵심은 `Regroup`나 team-center 지향 행동이 장기 안전전략으로 굳는 데 있다. regroup는 회복 목적의 짧은 역할로 제한하고, 오래 머물면 패널티를 받게 한다.

### 단계 5 — 역할 단위 메트릭 확장

```csharp
public sealed class GladiatorAgentEpisodeMetrics
{
    public void RecordRoleSample(int role);
    public void RecordRoleCompletion(int role);
    public void RecordRoleAbort(int role);
}
```

보상 조정 효과를 보려면 역할별 선택 횟수, 완료율, 조기 포기율이 필요하다. 기존 전투 지표 외에 역할 지표를 추가한다.

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| 목표 지속성 | switch penalty 위주 | 유지 보상과 조기 이탈 패널티 동시 적용 |
| 추격 학습 | 거리 감소 중심 | 마무리 성공까지 이어지는 shaping |
| 아군 보조 학습 | 약한 peel bonus만 존재 | focus 해소와 생존 유지 기준으로 명시 |
| regroup 의미 | 안전하게 뭉치는 선택으로 악용 가능 | 짧은 회복 역할로 제한 |
| 보상 디버깅 | 전투 공통 reward 위주 | 역할별 선택/완료/중단 메트릭으로 세분화 |

## 영향 범위

### 수정 대상 파일

- `Assets/Scripts/BattleScene/Agent/GladiatorAgentTacticalContext.cs`
  — commitment 관련 컨텍스트 필드 추가
- `Assets/Scripts/BattleScene/Agent/GladiatorRewardConfig.cs`
  — 역할별 shaping 계수와 regroup window 파라미터 추가
- `Assets/Scripts/BattleScene/Agent/GladiatorTacticalRewardShaper.cs`
  — 역할별 rule 기반 구조로 재구성
- `Assets/Scripts/BattleScene/Agent/GladiatorRewardEvaluator.cs`
  — commitment reward, abort penalty, 역할별 shaper 호출 정리
- `Assets/Scripts/BattleScene/Agent/GladiatorAgent.cs`
  — commitment 상태 추적 및 context 구성
- `Assets/Scripts/BattleScene/Agent/GladiatorAgentEpisodeMetrics.cs`
  — 역할별 선택/완료/중단 메트릭 추가

### 신규 생성 파일

```
Assets/Scripts/BattleScene/Agent/RewardRules/IGladiatorRoleRewardRule.cs
Assets/Scripts/BattleScene/Agent/RewardRules/GladiatorEngageRewardRule.cs
Assets/Scripts/BattleScene/Agent/RewardRules/GladiatorPeelRewardRule.cs
Assets/Scripts/BattleScene/Agent/RewardRules/GladiatorAssassinateRewardRule.cs
Assets/Scripts/BattleScene/Agent/RewardRules/GladiatorRegroupRewardRule.cs
```

### 위험도
**중간.** 보상 구조가 크게 바뀌어 기존 학습 곡선과 직접 비교가 어려워지지만, 입출력 계약과 물리 실행기를 다시 바꾸지는 않는다.

## 구현 단계

### Step 1 — tactical context에 commitment 상태 추가

`GladiatorAgentTacticalContext.cs`를 확장해 역할 유지 step, anchor 유지 step, 조기 이탈 여부를 담는다.

```csharp
public readonly int AnchorCommitmentSteps;
public readonly int RoleCommitmentSteps;
public readonly bool BrokeCommitmentEarly;
```

### Step 2 — reward config에 역할별 파라미터 추가

`GladiatorRewardConfig.cs`에 역할별 접근/완료/중단 계수를 추가한다.

```csharp
public float engageOpportunityReward = 0.01f;
public float peelFocusBreakReward = 0.03f;
public float assassinateFinishReward = 0.08f;
public float regroupOverstayPenalty = -0.02f;
public int regroupWindowSteps = 8;
```

### Step 3 — 역할별 reward rule 파일 생성

`RewardRules/` 아래에 역할별 클래스와 인터페이스를 만든다.

```csharp
public sealed class GladiatorPeelRewardRule : IGladiatorRoleRewardRule
{
    public float Evaluate(...)
    {
        // focus 해소 및 ally 생존 기준
    }
}
```

### Step 4 — tactical reward shaper를 역할 분기 구조로 교체

`GladiatorTacticalRewardShaper.cs`에서 현재 상수 기반 reward를 제거하고 역할별 rule로 위임한다.

```csharp
return _roleRules[action.Role].Evaluate(context, action, features);
```

### Step 5 — agent와 reward evaluator에서 commitment 추적 반영

`GladiatorAgent.cs`, `GladiatorRewardEvaluator.cs`를 수정해 role/anchor 변화 이력을 누적하고 context로 전달한다.

```csharp
if (action.AnchorSlot == _previousTargetSlot)
{
    _anchorCommitmentSteps++;
}
else
{
    _anchorCommitmentSteps = 0;
}
```

### Step 6 — episode metrics에 역할별 지표 추가

`GladiatorAgentEpisodeMetrics.cs`에 역할 선택률, 완료율, abort 비율을 기록한다.

```csharp
statsRecorder.Add("Combat/RoleAssassinateCompletionRate", completionRate);
```

### Step 7 — 검증

- 동일 seed로 짧은 self-play를 돌려 `TargetSwitch`, `Combat/Role*AbortRate` 추이를 비교한다.
- regroup 사용률이 높지만 완료율이 낮게 나오는지 확인한다.
- low HP 적 도주 상황에서 `AssassinateCompletionRate`가 증가하는지 본다.
- 아군 focus 상황에서 `PeelCompletionRate`와 ally survival 관련 지표가 개선되는지 확인한다.
