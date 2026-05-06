# 030. Gladiator 목표 지속성 관측 및 행동 기반 정비

## 개요

(선행 플랜 없음)  
현재 `GladiatorAgent`는 `anchor` 중심의 전술 인터페이스를 이미 도입했지만, 목표 지속성 및 역할 분화에 필요한 상태 표현이 아직 약하다. 이 플랜은 observation과 action 계약을 먼저 정비해 “특정 적을 끝까지 추격한다”, “특정 아군을 일정 시간 보호한다” 같은 중기 의도를 정책이 안정적으로 유지할 수 있는 기반을 만드는 것을 목표로 한다.

## 문제 상황

### 현재 목표 지속성 관측의 문제

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorObservationBuilder.cs
sensor.AddObservation(context.BattleTimeoutRemainingRatio);
Vector2 canonicalSmoothedMove = BattleCanonicalFrame.ToCanonical(self.TeamId, context.AgentSmoothedWorldMove);
Vector2 canonicalPreviousMove = BattleCanonicalFrame.ToCanonical(
    self.TeamId,
    context.AgentPreviousRawWorldMove
);
sensor.AddObservation(canonicalSmoothedMove.x);
sensor.AddObservation(canonicalSmoothedMove.y);
sensor.AddObservation(canonicalPreviousMove.x);
sensor.AddObservation(canonicalPreviousMove.y);
AddAnchorKindOneHot(sensor, context.AnchorKind);
AddPathModeOneHot(sensor, context.PathMode);
```

**문제 1 — 이전 선택의 의미는 남지만 선택 지속 시간은 남지 않는다**  
현재 observation에는 현재 `AnchorKind`, `PathMode`, 이전 이동 입력은 들어가지만, 동일 anchor를 몇 step째 유지 중인지 정보가 없다. 이 구조에서는 정책이 “조금 해보다가 바꾸는” 습관을 버리기 어렵다.

**문제 2 — 역할 상태가 anchor/path 조합에 암묵적으로만 존재한다**  
`Enemy + FlankLeft`와 `Ally + Direct`를 통해 역할을 추론할 수는 있지만, 정책이 스스로 현재 역할을 안정적으로 유지하는 데 필요한 self-state가 부족하다.

### 현재 관계 기반 관측의 문제

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorObservationBuilder.cs
private static float ComputeAllyUnderFocusRatio(
    IReadOnlyList<BattleUnitCombatState> teammates,
    IReadOnlyList<BattleUnitCombatState> opponents
)
{
    int allyCount = 0;
    int focusedCount = 0;
    for (int i = 0; i < teammates.Count; i++)
    {
        BattleUnitCombatState teammate = teammates[i];
        if (teammate == null || teammate.IsCombatDisabled)
        {
            continue;
        }

        allyCount++;
        for (int j = 0; j < opponents.Count; j++)
        {
            BattleUnitCombatState opponent = opponents[j];
            if (opponent != null && opponent.PlannedTargetEnemy == teammate)
            {
                focusedCount++;
                break;
            }
        }
    }
```

**문제 3 — 팀 단위 focus 비율은 있지만 anchor 단위 혼잡도는 없다**  
현재는 “아군 중 누군가 포커싱되고 있다”는 팀 레벨 요약만 있다. 하지만 암살, peel, chase를 분리하려면 “내가 고른 이 적을 이미 몇 명이 압박 중인지”, “내가 붙으려는 아군을 몇 명이 노리고 있는지”가 필요하다.

**문제 4 — 도주 대상과 고립 대상을 분리할 근거가 부족하다**  
현재 적 슬롯에는 상대 위치와 기본 스탯, `IsTargetingMeAggressively`만 있다. 저체력 도주 적, 고립된 backline 적, 이미 포화된 frontline 적을 분리하는 feature가 없다.

### 현재 액션 의미의 문제

```csharp
// Assets/Scripts/BattleScene/Agent/GladiatorActionSchema.cs
public const int CommandBranch = 0;
public const int StanceBranch = 1;
public const int PathModeBranch = 2;
public const int AnchorKindBranch = 3;
public const int AnchorSlotBranch = 4;
public const int DiscreteBranchCount = 5;
```

**문제 5 — 전술 조합은 가능하지만 상위 의도는 명시되지 않는다**  
현재 액션은 `stance/path/anchor`를 조합하면 다양한 전술을 만들 수 있다. 하지만 “이번 step은 추격, 이번 step은 peel” 같은 상위 목표가 드러나지 않아 학습이 여전히 조합 탐색 문제로 남는다.

**문제 6 — action mask가 전술별 유효 후보를 충분히 줄여주지 않는다**  
현재 mask는 사망한 적, tactic mode에 따른 `PathMode`/`AnchorKind` 제한 수준에 머문다. 역할별 유효 anchor 후보를 줄이지 않으면 팀 뭉침 같은 쉬운 안전전략으로 수렴하기 쉽다.

## 해결 방안

### 단계 1 — observation에 목표 지속성 상태를 추가

```csharp
public readonly struct GladiatorObservationContext
{
    public readonly int AnchorKind;
    public readonly int PathMode;
    public readonly int CurrentRole;
    public readonly int AnchorCommitmentSteps;
    public readonly int RoleCommitmentSteps;
    public readonly BattleUnitCombatState CurrentAnchor;
}
```

`GladiatorObservationContext`에 현재 anchor 유지 시간과 역할 유지 시간을 추가한다. 정책이 “바로 바꾸는 것”과 “의도를 유지하는 것”의 차이를 self-state로 직접 볼 수 있게 한다.

### 단계 2 — 역할 one-hot과 관계 feature를 self observation에 확장

```csharp
public enum GladiatorRoleObservationIndex
{
    RoleEngage = 31,
    RolePeel = 32,
    RoleAssassinate = 33,
    RoleRegroup = 34,
    AnchorCommitmentRatio = 35,
    RoleCommitmentRatio = 36,
    AnchorAllySupportPressure = 37,
    AnchorEnemyFocusPressure = 38,
    AnchorEnemyIsolation = 39,
    AnchorEnemyRetreatSignal = 40,
}
```

현재 `SelfSize = 31` 계약을 확장해 역할 one-hot과 anchor 관련 관계 요약치를 넣는다. 여기서 `AnchorEnemyFocusPressure`는 “이 적을 이미 아군 몇 명이 보고 있나”, `AnchorEnemyIsolation`은 “주변 적 지원이 얼마나 적나”, `AnchorEnemyRetreatSignal`은 “최근 멀어지는 중인가”를 뜻한다.

### 단계 3 — anchor 후보별 관계 값 계산 모듈을 분리

```csharp
public readonly struct GladiatorAnchorRelationFeatures
{
    public readonly float AllySupportPressure;
    public readonly float EnemyFocusPressure;
    public readonly float EnemyIsolation;
    public readonly float EnemyRetreatSignal;
}

public static GladiatorAnchorRelationFeatures BuildAnchorRelationFeatures(
    BattleUnitCombatState self,
    BattleUnitCombatState anchor,
    IReadOnlyList<BattleUnitCombatState> teammates,
    IReadOnlyList<BattleUnitCombatState> opponents,
    float arenaRadius)
```

`GladiatorObservationBuilder` 내부에 흩어진 계산을 모듈화해, 이후 reward와 metrics에서도 같은 정의를 재사용할 수 있게 한다.

### 단계 4 — 상위 역할 액션 브랜치를 추가

```csharp
public static class GladiatorActionSchema
{
    public const int RoleBranch = 1;
    public const int StanceBranch = 2;
    public const int PathModeBranch = 3;
    public const int AnchorKindBranch = 4;
    public const int AnchorSlotBranch = 5;

    public const int RoleEngage = 0;
    public const int RolePeel = 1;
    public const int RoleAssassinate = 2;
    public const int RoleRegroup = 3;
}
```

현재 조합형 액션 위에 `RoleBranch`를 추가해 정책이 먼저 상위 의도를 고르게 만든다. 이후 `stance/path/anchor`는 해당 역할의 구체화로 해석한다.

### 단계 5 — 역할별 action mask를 도입

```csharp
private void ApplyRoleAwareAnchorMask(
    IDiscreteActionMask actionMask,
    GladiatorRole role,
    int slotBranchSize)
{
    if (role == GladiatorRole.Peel)
    {
        // focus 받는 아군 슬롯만 허용
    }
    else if (role == GladiatorRole.Assassinate)
    {
        // 고립되었거나 저체력인 적 슬롯만 허용
    }
}
```

기존 death/tactic mode mask 외에 역할별 후보 축소를 추가한다. 이는 탐색 공간을 줄이고 “모든 상황에서 같은 적만 치는” 정책을 깨는 데 직접 도움이 된다.

## 기대 효과

| 항목 | 현재 | 개선 후 |
|---|---|---|
| 목표 유지 표현 | 현재 선택 종류만 있음 | 선택 지속 시간과 역할 상태까지 관측 |
| 역할 분화 | `anchor/path/stance` 조합에 암묵적으로만 존재 | `RoleBranch`로 명시적 선택 |
| 적 선택 근거 | 거리와 기본 스탯 위주 | 집중도, 고립도, 도주 신호까지 포함 |
| 아군 보조 판단 | 팀 단위 `AllyUnderFocusRatio` 중심 | anchor 아군 단위 지원 가치 판단 가능 |
| action 탐색 공간 | 광범위한 조합 탐색 | 역할별 mask로 유효 후보 축소 |

## 영향 범위

### 수정 대상 파일

- `Assets/Scripts/BattleScene/Agent/GladiatorObservationSchema.cs`
  — self observation 인덱스와 총 길이 확장
- `Assets/Scripts/BattleScene/Agent/GladiatorObservationBuilder.cs`
  — 역할 상태, 관계 feature, commitment feature 추가
- `Assets/Scripts/BattleScene/Agent/GladiatorActionSchema.cs`
  — `RoleBranch` 추가와 branch 순서 재정의
- `Assets/Scripts/BattleScene/Agent/GladiatorAgentActionParser.cs`
  — 역할 브랜치를 읽어 `GladiatorPolicyAction`에 반영
- `Assets/Scripts/BattleScene/Agent/GladiatorPolicyAction.cs`
  — role 필드 추가
- `Assets/Scripts/BattleScene/Agent/GladiatorAgent.cs`
  — commitment state 추적, 역할별 mask 적용
- `Assets/Scripts/BattleScene/Agent/GladiatorAgentTacticalContext.cs`
  — 역할/지속성 관련 컨텍스트 필드 확장

### 신규 생성 파일

```
Assets/Scripts/BattleScene/Agent/GladiatorAnchorRelationFeatures.cs
```

### 위험도
**중간.** observation/action contract가 바뀌므로 재학습과 demo 재생성은 필요하지만, 물리 실행기와 trainer 운영 변경은 아직 건드리지 않는다.

## 구현 단계

### Step 1 — 역할 및 지속성 상태 모델 추가

`GladiatorPolicyAction.cs`, `GladiatorAgentTacticalContext.cs`에 역할과 commitment 상태를 담을 필드를 추가한다.

```csharp
public readonly struct GladiatorPolicyAction
{
    public readonly int Role;
    public readonly Vector2 RelativeMove;
    public readonly int AnchorKind;
    public readonly int AnchorSlot;
}
```

### Step 2 — observation schema 확장

`GladiatorObservationSchema.cs`에서 self observation 길이와 enum을 늘리고, 역할 one-hot 및 commitment index를 확정한다.

```csharp
public const int SelfSize = 41;
```

### Step 3 — 관계 feature 계산기 분리

`GladiatorAnchorRelationFeatures.cs`를 만들고, anchor 기준 집중도/고립도/도주 신호 계산을 한 곳에 모은다.

```csharp
public static GladiatorAnchorRelationFeatures Build(...)
{
    return new GladiatorAnchorRelationFeatures(...);
}
```

### Step 4 — observation builder에 역할 및 commitment 관측 추가

`GladiatorObservationBuilder.cs`에서 새 feature를 self observation에 기록한다.

```csharp
sensor.AddObservation(context.CurrentRole == GladiatorActionSchema.RoleEngage ? 1f : 0f);
sensor.AddObservation(context.CurrentRole == GladiatorActionSchema.RolePeel ? 1f : 0f);
sensor.AddObservation(context.AnchorCommitmentRatio);
sensor.AddObservation(anchorRelations.EnemyIsolation);
```

### Step 5 — action schema와 parser에 RoleBranch 추가

`GladiatorActionSchema.cs`, `GladiatorAgentActionParser.cs`를 수정해 role을 별도 discrete branch로 파싱한다.

```csharp
int role = ReadDiscrete(
    actions.DiscreteActions,
    GladiatorActionSchema.RoleBranch,
    GladiatorActionSchema.RoleEngage);
```

### Step 6 — 역할별 anchor/path mask 적용

`GladiatorAgent.cs`의 `WriteDiscreteActionMask`에서 현재 role 기준 유효 anchor/path 후보를 제한한다.

```csharp
ApplyRoleAwareAnchorMask(actionMask, currentRole, slotBranchSize);
```

### Step 7 — 검증

- Unity Editor에서 `Behavior Parameters`의 branch 수와 observation size가 새 계약과 일치하는지 확인한다.
- `Heuristic()` 경로가 새 branch 구조에서도 예외 없이 동작하는지 확인한다.
- inference 상태에서 role/anchor 변경 로그를 찍어 commitment step이 누적되는지 확인한다.
- 1v1과 2v2 짧은 학습에서 `TargetSwitch`가 과도하게 증가하지 않는지 확인한다.
