# Gladiator Agent Behavior Policy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply `002-gladiator-agent-behavior-policy.md` so `GladiatorAgent` uses enemy-only anchor targeting, a single Strategy branch, policy-aligned rewards, personality reward mixing, and lower-overhead metrics.

**Architecture:** Treat `GladiatorActionSchema` and `GladiatorObservationSchema` as the contracts. First migrate action and observation data models away from Role/Ally/TeamCenter, then update reward evaluation and metrics to the new Strategy semantics, and finally adjust assets/trainer config and validation tests. Keep simulation-facing behavior in `BattleAgentControlBuffer` and `MlAgentControlPlanner` compatible by carrying only the data the simulator still needs.

**Tech Stack:** Unity, C#, Unity ML-Agents, MA-POCA, EditMode tests, `unity-scanner`, `rtk`, `tools/repair_unity_csproj.py`.

---

## File Structure

- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorActionSchema.cs`  
  Owns the ML-Agents action contract. Rename FightMode to Strategy, remove Role and AnchorKind branches, set discrete branch count to 3, and make anchor action values `0..5` enemy slots.

- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorAction.cs`  
  Remove `Role` and `AnchorKind`; store `Strategy`, `AnchorSlot`, `Command`, and normalized local move only.

- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorAgentActionParser.cs`  
  Parse `Command`, `Strategy`, `Anchor` from three discrete branches. Clamp enemy anchor slot to `0..5`.

- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorAgentActionSink.cs`  
  Pass enemy-only anchor information to `BattleAgentControlBuffer`.

- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgent/BattleAgentControlInput.cs`  
  Remove `Role` and `AnchorKind`; keep `Strategy`, enemy `AnchorSlot`, `AnchorTarget`, and command.

- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgent/BattleAgentControlBuffer.cs`  
  Store enemy-only control input and call `SetAgentFightMode(strategy)` until the underlying `BattleUnitCombatState` API is renamed.

- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgentControlPlanner.cs`  
  Always emit `BattleAnchorKind.Enemy` for agent plans and use the resolved enemy target as both anchor and target.

- Modify: `Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgent.cs`  
  Update masks, previous-action state, heuristic, fallback handling, observation context, and comments to the 2 continuous + 3 branch contract.

- Modify: `Assets/Scripts/BattleScene/Agent/Agent/BattleSceneGladiatorAgentBinder.cs`  
  Update expected branch sizes and contract validation for inference mode.

- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingAgentBinder.cs`  
  Update expected branch sizes and contract setup for training mode. Keep MA-POCA reward code but clamp team outcome reward to `[-100, 100]`.

- Modify: `Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgentInferenceConfig.cs`  
  Keep inference contract metadata aligned with the new contract version and observation size.

- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservationSchema.cs`  
  Make comments and indices the single source of truth. Remove anchor kind and role observation fields, add command/strategy/continuous fields required by the spec, and include collectivism/passiveness.

- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservationBuilder.cs`  
  Write anchor-axis coordinates, current command/strategy/continuous inputs, commitment ratios, tactical pressure features, and personality bias.

- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservation.cs`  
  Adjust the self-observation constructor and `WriteTo` order to match the schema.

- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorCombatSignalFeatures.cs`  
  Remove TeamCenter/Ally branches and compute features relative to enemy anchor only.

- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Combat/GladiatorTacticalContext.cs`  
  Remove Role and AnchorKind state; track Command, Strategy, AnchorSlot, previous target distance, valid enemy target, and commitment windows.

- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorRewardConfig.cs`  
  Remove step, invalid action, role rewards, and role commitment fields. Add optional terminal survival bonus and personality clamp fields.

- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorRewardEvaluator.cs`  
  Remove per-step positive/negative step reward. Evaluate switch penalties for Command/Strategy/Anchor only, smoothness only for Move-to-Move, and apply personality mixing to reward categories.

- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorTacticalRewardShaper.cs`  
  Delete role rule wiring and keep Strategy reward rules only.

- Delete: `Assets/Scripts/BattleScene/Agent/Reward/Rules/IGladiatorRoleRewardRule.cs`  
  Role rewards are outside the new policy.

- Delete: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorEngageRewardRule.cs`
- Delete: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorAssassinateRewardRule.cs`
- Delete: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorRegroupRewardRule.cs`
- Delete: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorPeelRewardRule.cs`  
  These encode the removed Role layer.

- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorPressureRewardRule.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorKeepRangeRewardRule.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorRetreatRewardRule.cs`  
  Keep these as Strategy rules and align formulas with the policy.

- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Logging/GladiatorAgentEpisodeMetrics.cs`  
  Precompute metric keys, remove per-step `StatsRecorder.Add` calls for share metrics, flush local averages every 10,000 decision steps, and rename FightMode metrics to Strategy metrics.

- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Environment/IGladiatorCurriculumSource.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Environment/GladiatorAnchorCurriculum.cs`
- Delete or stop using: `Assets/Scripts/BattleScene/Agent/Shared/Environment/GladiatorRoleCurriculum.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/Curriculum/TrainingCurriculumParameterNames.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingBootstrapper.cs`  
  Remove Role curriculum and force enemy-slot-only anchors.

- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/LegacyAI/BuiltInAiHeuristicTranslator.cs`  
  Translate built-in AI to the new 3-branch action contract for demo recording.

- Modify: `Assets/ML-Agents/GladiatorBehavior.yaml`  
  Set `summary_freq: 50000` and remove `role_curriculum`.

- Modify Unity assets through Inspector or YAML-aware tooling:
  - `Assets/ML-Agents/GladiatorRewardConfig.asset`
  - `Assets/ML-Agents/BattleMlAgentInferenceConfig.asset`
  Set contract version, branch-related serialized values, reward defaults, and trainer metadata to match the code.

- Create: `Assets/Tests/EditMode/GladiatorActionSchemaTests.cs`  
  EditMode tests for action branch sizes, anchor encode/decode, parser clamping, and enemy-only mask assumptions.

- Create: `Assets/Tests/EditMode/GladiatorRewardPolicyTests.cs`  
  EditMode tests for no step reward, Move-only smoothness, switch penalties, and Strategy reward behavior.

- Create: `Assets/Tests/EditMode/GladiatorMetricsTests.cs`  
  EditMode tests for metric key caching and 10,000-step local flush semantics where possible without a live trainer.

---

### Task 1: Lock The New Action Contract

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorActionSchema.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorAction.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Action/GladiatorAgentActionParser.cs`
- Create: `Assets/Tests/EditMode/GladiatorActionSchemaTests.cs`

- [ ] **Step 1: Write failing action contract tests**

Create `Assets/Tests/EditMode/GladiatorActionSchemaTests.cs` with:

```csharp
using NUnit.Framework;
using Unity.MLAgents.Actuators;
using UnityEngine;

public sealed class GladiatorActionSchemaTests
{
    [Test]
    public void Schema_UsesTwoContinuousAndThreeDiscreteBranches()
    {
        Assert.AreEqual(14, GladiatorActionSchema.ContractVersion);
        Assert.AreEqual(2, GladiatorActionSchema.ContinuousSize);
        Assert.AreEqual(3, GladiatorActionSchema.DiscreteBranchCount);
        Assert.AreEqual(0, GladiatorActionSchema.CommandBranch);
        Assert.AreEqual(1, GladiatorActionSchema.StrategyBranch);
        Assert.AreEqual(2, GladiatorActionSchema.AnchorBranch);
        CollectionAssert.AreEqual(
            new[] { 2, 4, BattleTeamConstants.MaxUnitsPerTeam },
            GladiatorActionSchema.DiscreteBranchSizes
        );
    }

    [Test]
    public void AnchorEncoding_IsEnemySlotOnly()
    {
        for (int slot = 0; slot < BattleTeamConstants.MaxUnitsPerTeam; slot++)
        {
            Assert.AreEqual(slot, GladiatorActionSchema.EncodeEnemyAnchorAction(slot));
            Assert.IsTrue(GladiatorActionSchema.TryDecodeEnemyAnchorAction(slot, out int decodedSlot));
            Assert.AreEqual(slot, decodedSlot);
        }
    }

    [Test]
    public void Parser_ClampsContinuousAndDiscreteValues()
    {
        ActionBuffers buffers = BuildActionBuffers(
            new[] { 2.5f, -3f },
            new[] { 99, 99, 99 }
        );

        GladiatorAction action = GladiatorAgentActionParser.Parse(buffers);

        Assert.AreEqual(Vector2.one.normalized.x, action.RelativeMove.x, 0.0001f);
        Assert.AreEqual(-Vector2.one.normalized.y, action.RelativeMove.y, 0.0001f);
        Assert.AreEqual(GladiatorCommand.Attack, action.Command);
        Assert.AreEqual(GladiatorStrategy.Retreat, action.Strategy);
        Assert.AreEqual(BattleTeamConstants.MaxUnitsPerTeam - 1, action.AnchorSlot);
    }

    private static ActionBuffers BuildActionBuffers(float[] continuousValues, int[] discreteValues)
    {
        ActionSegment<float> continuous = new ActionSegment<float>(continuousValues);
        ActionSegment<int> discrete = new ActionSegment<int>(discreteValues);
        return new ActionBuffers(continuous, discrete);
    }
}
```

- [ ] **Step 2: Run the new test and verify it fails**

Run:

```bash
rtk proxy powershell -NoProfile -Command "Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults Temp/EditModeResults.xml"
```

Expected: FAIL because `ContractVersion` is still 13, `StrategyBranch` and `DiscreteBranchSizes` do not exist, and Role/AnchorKind are still part of the schema.

- [ ] **Step 3: Replace Role/FightMode/AnchorKind schema with Strategy + enemy anchor**

Change `GladiatorActionSchema.cs` to this shape:

```csharp
// ML-Agents discrete action branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 명령 종류다.
public enum GladiatorCommand
{
    Move = 0,
    Attack = 1,
}

// ML-Agents strategy branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 단기 교전 태세다.
public enum GladiatorStrategy
{
    Neutral = 0,
    Pressure = 1,
    KeepRange = 2,
    Retreat = 3,
}

public static class GladiatorActionSchema
{
    public const int ContractVersion = 14;

    public const int ContinuousAnchorStrafe = 0;
    public const int ContinuousAnchorForward = 1;
    public const int ContinuousSize = 2;

    public const int CommandBranch = 0;
    public const int StrategyBranch = 1;
    public const int AnchorBranch = 2;
    public const int DiscreteBranchCount = 3;

    public const int CommandBranchSize = 2;
    public const int StrategyBranchSize = 4;
    public const int AnchorActionBranchSize = BattleTeamConstants.MaxUnitsPerTeam;

    public static readonly int[] DiscreteBranchSizes =
    {
        CommandBranchSize,
        StrategyBranchSize,
        AnchorActionBranchSize,
    };

    public static int EncodeEnemyAnchorAction(int anchorSlot) =>
        Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);

    public static bool TryDecodeEnemyAnchorAction(int anchorAction, out int anchorSlot)
    {
        if (anchorAction >= 0 && anchorAction < AnchorActionBranchSize)
        {
            anchorSlot = anchorAction;
            return true;
        }

        anchorSlot = 0;
        return false;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
```

- [ ] **Step 4: Simplify `GladiatorAction`**

Change the struct fields and helpers to:

```csharp
using UnityEngine;

// GladiatorAgent의 정책 출력 action을 시스템 내부에서 공유하는 값 타입이다.
// ActionBuffers의 raw branch 값은 이 contract로 정규화된 뒤 reward, tactical context, sink, metrics가 함께 해석한다.
public readonly struct GladiatorAction
{
    public readonly Vector2 RelativeMove;
    public readonly GladiatorStrategy Strategy;
    public readonly int AnchorSlot;
    public readonly GladiatorCommand Command;

    public GladiatorAction(
        Vector2 relativeMove,
        GladiatorStrategy strategy,
        int anchorSlot,
        GladiatorCommand command
    )
    {
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        Strategy = strategy;
        AnchorSlot = Mathf.Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);
        Command = command;
    }

    public GladiatorAction WithCommand(GladiatorCommand command) =>
        new GladiatorAction(RelativeMove, Strategy, AnchorSlot, command);

    public GladiatorAction WithAnchorSlot(int anchorSlot) =>
        new GladiatorAction(RelativeMove, Strategy, anchorSlot, Command);
}
```

- [ ] **Step 5: Update parser to three branches**

`GladiatorAgentActionParser.Parse` should return:

```csharp
public static GladiatorAction Parse(ActionBuffers actions)
{
    int command = actions.DiscreteActions.Length > GladiatorActionSchema.CommandBranch
        ? actions.DiscreteActions[GladiatorActionSchema.CommandBranch]
        : (int)GladiatorCommand.Move;
    command = Mathf.Clamp(command, 0, GladiatorActionSchema.CommandBranchSize - 1);

    int strategy = actions.DiscreteActions.Length > GladiatorActionSchema.StrategyBranch
        ? actions.DiscreteActions[GladiatorActionSchema.StrategyBranch]
        : (int)GladiatorStrategy.Neutral;
    strategy = Mathf.Clamp(strategy, 0, GladiatorActionSchema.StrategyBranchSize - 1);

    int anchorSlot = actions.DiscreteActions.Length > GladiatorActionSchema.AnchorBranch
        ? actions.DiscreteActions[GladiatorActionSchema.AnchorBranch]
        : 0;
    if (!GladiatorActionSchema.TryDecodeEnemyAnchorAction(anchorSlot, out anchorSlot))
    {
        anchorSlot = 0;
    }

    return new GladiatorAction(
        ReadRelativeMove(actions.ContinuousActions),
        (GladiatorStrategy)strategy,
        anchorSlot,
        (GladiatorCommand)command
    );
}
```

- [ ] **Step 6: Run action tests**

Run:

```bash
rtk proxy powershell -NoProfile -Command "Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults Temp/EditModeResults.xml"
```

Expected: the new action schema tests pass or fail only on downstream compile errors from files still using Role/FightMode/AnchorKind.

- [ ] **Step 7: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Action Assets/Tests/EditMode/GladiatorActionSchemaTests.cs
rtk git commit -m "feat: update gladiator action contract"
```

### Task 2: Update Agent Binding, Masks, And Simulation Input

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgent.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Agent/BattleSceneGladiatorAgentBinder.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingAgentBinder.cs`
- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgent/BattleAgentControlInput.cs`
- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgent/BattleAgentControlBuffer.cs`
- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/MlAgentControlPlanner.cs`
- Modify: `Assets/Scripts/BattleScene/Simulation/Planners/LegacyAI/BuiltInAiHeuristicTranslator.cs`

- [ ] **Step 1: Replace expected branch arrays**

In both binders, replace local branch arrays with:

```csharp
private static readonly int[] ExpectedDiscreteBranches =
{
    GladiatorActionSchema.CommandBranchSize,
    GladiatorActionSchema.StrategyBranchSize,
    GladiatorActionSchema.AnchorActionBranchSize,
};
```

When constructing `ActionSpec`, prefer:

```csharp
behaviorParameters.BrainParameters.ActionSpec = new ActionSpec(
    GladiatorActionSchema.ContinuousSize,
    (int[])GladiatorActionSchema.DiscreteBranchSizes.Clone()
);
```

- [ ] **Step 2: Remove Role and AnchorKind state from `GladiatorAgent`**

Replace previous-state fields with:

```csharp
private GladiatorCommand? _previousCommand;
private int _previousTargetSlot = -1;
private GladiatorStrategy? _previousStrategy;
private int _commandCommitmentSteps;
private int _anchorCommitmentSteps;
private int _strategyCommitmentSteps;
```

Reset them in `Initialize` and `OnEpisodeBegin`:

```csharp
_previousCommand = null;
_previousTargetSlot = -1;
_previousStrategy = null;
_commandCommitmentSteps = 0;
_anchorCommitmentSteps = 0;
_strategyCommitmentSteps = 0;
```

- [ ] **Step 3: Simplify action masks**

Replace `WriteDiscreteActionMask` branch logic with:

```csharp
ApplyAnchorActionMask(actionMask, branchSizes);
ApplyStrategyMask(actionMask, branchSizes);
ApplyCommandMask(actionMask, branchSizes);
```

Implement enemy-only anchor mask:

```csharp
private void ApplyAnchorActionMask(IDiscreteActionMask actionMask, int[] branchSizes)
{
    if (branchSizes.Length <= GladiatorActionSchema.AnchorBranch)
    {
        return;
    }

    int branchSize = branchSizes[GladiatorActionSchema.AnchorBranch];
    for (int i = 0; i < branchSize; i++)
    {
        if (!IsValidEnemySlot(i))
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.AnchorBranch, i, false);
        }
    }
}
```

Rename `ApplyFightModeMask` to `ApplyStrategyMask` and switch to `GladiatorStrategy.KeepRange`.

- [ ] **Step 4: Simplify target resolution and fallback**

Replace anchor target resolution with:

```csharp
private BattleUnitCombatState ResolveAnchorTarget(GladiatorAction action) =>
    ResolveOpponentSlot(action.AnchorSlot);
```

Replace fallback with:

```csharp
private bool TryApplyNearestEnemyAnchorFallback(ref GladiatorAction action, ref BattleUnitCombatState target)
{
    if (IsValidAnchorTarget(target))
    {
        return false;
    }

    if (!TryResolveNearestOpponentAnchor(out BattleUnitCombatState fallbackTarget, out int fallbackSlot))
    {
        return false;
    }

    target = fallbackTarget;
    action = action.WithAnchorSlot(fallbackSlot);
    return true;
}
```

Delete `NormalizeAllyAnchorAction`.

- [ ] **Step 5: Update simulation buffer input**

Change `BattleAgentControlInput` fields to:

```csharp
public Vector2 RawLocalMove;
public Vector2 PreviousRawLocalMove;
public GladiatorStrategy Strategy;
public int AnchorSlot;
public BattleCombatCommand Command;
public BattleUnitCombatState AnchorTarget;
public BattleUnitCombatState Target;
```

Change `BattleAgentControlBuffer.SetRawInput` signature to:

```csharp
public void SetRawInput(
    BattleUnitCombatState self,
    Vector2 rawRelativeMove,
    GladiatorStrategy strategy,
    int anchorSlot,
    GladiatorCommand command,
    BattleUnitCombatState target
)
```

Inside it:

```csharp
input.Strategy = strategy;
input.AnchorSlot = Mathf.Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);
input.Command = ToCommand(command);
input.AnchorTarget = target;
input.Target = BattleFieldSnapshot.IsValidEnemyTarget(self, target) ? target : null;
self.SetAgentFightMode((GladiatorFightMode)strategy);
```

Keep the cast only if `BattleUnitCombatState.SetAgentFightMode` has not been renamed yet; otherwise update that API in the same task.

- [ ] **Step 6: Update planner bridge**

In `MlAgentControlPlanner`, emit enemy anchor plans:

```csharp
BattleAnchor anchor = new BattleAnchor(BattleAnchorKind.Enemy, input.AnchorTarget);
return new BattleControlPlan(
    combatIntent,
    input.Target,
    anchor,
    input.RawLocalMove
);
```

- [ ] **Step 7: Update heuristic translator**

Write the discrete action branches as:

```csharp
discrete[GladiatorActionSchema.CommandBranch] = ResolveCommand(plan, selfState);
discrete[GladiatorActionSchema.StrategyBranch] = ResolveStrategy(currentAction);
discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(enemySlot);
```

Map old action types to strategy:

```csharp
private static int ResolveStrategy(BattleActionType actionType) =>
    actionType switch
    {
        BattleActionType.EscapeFromPressure => (int)GladiatorStrategy.Retreat,
        BattleActionType.PeelForWeakAlly => (int)GladiatorStrategy.KeepRange,
        BattleActionType.RegroupToAllies => (int)GladiatorStrategy.KeepRange,
        BattleActionType.AssassinateIsolatedEnemy => (int)GladiatorStrategy.Pressure,
        BattleActionType.DiveEnemyBackline => (int)GladiatorStrategy.Pressure,
        BattleActionType.CollapseOnCluster => (int)GladiatorStrategy.Pressure,
        _ => (int)GladiatorStrategy.Neutral,
    };
```

- [ ] **Step 8: Compile check**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
```

Expected: compile errors now only come from observation/reward/metrics references still using Role/FightMode/AnchorKind.

- [ ] **Step 9: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Agent Assets/Scripts/BattleScene/Agent/TrainingSetup Assets/Scripts/BattleScene/Simulation/Planners
rtk git commit -m "feat: bind enemy-only gladiator agent actions"
```

### Task 3: Align Observation Schema With The Policy

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservationSchema.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservation.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservationBuilder.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorCombatSignalFeatures.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgent.cs`

- [ ] **Step 1: Make `GladiatorObservationSchema` the explicit SSoT**

Update self indices to remove anchor kind and role fields and include the spec fields:

```csharp
public static class GladiatorObservationSchema
{
    public const int SelfSize = 43;
    public const int TeammateSlotSize = 9;
    public const int OpponentSlotSize = 10;
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1;
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam;
    public const int TotalSize = SelfSize + (TeammateSlots * TeammateSlotSize) + (OpponentSlots * OpponentSlotSize);
}

public enum GladiatorSelfObservationIndex
{
    ArenaCenterAnchorRelativeX = 0,
    ArenaCenterAnchorRelativeZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,
    AnchorThreatToSelfRatio = 8,
    SelfThreatToAnchorRatio = 9,
    AnchorInSelfRange = 10,
    SelfInAnchorRange = 11,
    LeftLaneFreeRatio = 12,
    RightLaneFreeRatio = 13,
    EnemyClusterPressure = 14,
    BoundaryPressure = 15,
    BattleTimeoutRemainingRatio = 16,
    CurrentCommandMove = 17,
    CurrentCommandAttack = 18,
    CurrentStrategyNeutral = 19,
    CurrentStrategyPressure = 20,
    CurrentStrategyKeepRange = 21,
    CurrentStrategyRetreat = 22,
    CurrentAnchorSlot0 = 23,
    CurrentAnchorSlot1 = 24,
    CurrentAnchorSlot2 = 25,
    CurrentAnchorSlot3 = 26,
    CurrentAnchorSlot4 = 27,
    CurrentAnchorSlot5 = 28,
    CurrentAnchorStrafe = 29,
    CurrentAnchorForward = 30,
    PreviousAnchorStrafe = 31,
    PreviousAnchorForward = 32,
    CommandCommitmentRatio = 33,
    StrategyCommitmentRatio = 34,
    AnchorCommitmentRatio = 35,
    AnchorAllySupportPressure = 36,
    AnchorEnemyFocusPressure = 37,
    AnchorEnemyIsolation = 38,
    AnchorEnemyRetreatSignal = 39,
    CollectivismBias = 40,
    PassivenessBias = 41,
    ReservedPolicyFeature = 42,
}
```

- [ ] **Step 2: Update observation context**

Change context fields to:

```csharp
public readonly GladiatorCommand CurrentCommand;
public readonly GladiatorStrategy CurrentStrategy;
public readonly int AnchorSlot;
public readonly int CommandCommitmentSteps;
public readonly int StrategyCommitmentSteps;
public readonly int AnchorCommitmentSteps;
public readonly float CollectivismBias;
public readonly float PassivenessBias;
```

Keep `WorldToObservationAxes` unchanged because it already projects onto anchor axes.

- [ ] **Step 3: Build self observation with policy fields**

In `GladiatorObservationBuilder.Build`, pass:

```csharp
context.CurrentCommand,
context.CurrentStrategy,
context.AnchorSlot,
context.AgentSmoothedWorldMove.x,
context.AgentSmoothedWorldMove.y,
context.AgentPreviousRawWorldMove.x,
context.AgentPreviousRawWorldMove.y,
NormalizeCommitment(context.CommandCommitmentSteps),
NormalizeCommitment(context.StrategyCommitmentSteps),
NormalizeCommitment(context.AnchorCommitmentSteps),
features.AnchorAllySupportPressure,
features.AnchorEnemyFocusPressure,
features.AnchorEnemyIsolation,
features.AnchorEnemyRetreatSignal,
Mathf.Clamp01(context.CollectivismBias),
Mathf.Clamp01(context.PassivenessBias),
0f
```

- [ ] **Step 4: Update `GladiatorObservation` serialization order**

Adjust `GladiatorSelfObservation.WriteTo` so values are written in exactly the `GladiatorSelfObservationIndex` order. Add an assertion in the constructor:

```csharp
Debug.Assert(Values.Length == GladiatorObservationSchema.SelfSize);
```

- [ ] **Step 5: Update `GladiatorAgent.CreateObservationContext`**

Read the buffer snapshot and pass:

```csharp
controlInput.Command == BattleCombatCommand.BasicAttack ? GladiatorCommand.Attack : GladiatorCommand.Move,
controlInput.Strategy,
controlInput.AnchorSlot,
_commandCommitmentSteps,
_strategyCommitmentSteps,
_anchorCommitmentSteps,
ResolveCollectivismBias(),
ResolvePassivenessBias(),
currentAnchor
```

Add local bias methods returning neutral defaults until personality data is wired in Task 6:

```csharp
private float ResolveCollectivismBias() => 0.5f;
private float ResolvePassivenessBias() => 0.5f;
```

- [ ] **Step 6: Compile check**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
```

Expected: observation-related compile errors are gone; remaining errors are in reward/metrics if not yet migrated.

- [ ] **Step 7: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Observation Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgent.cs
rtk git commit -m "feat: align gladiator observations with behavior policy"
```

### Task 4: Replace Role Rewards With Strategy Rewards

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Combat/GladiatorTacticalContext.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorTacticalRewardShaper.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorPressureRewardRule.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorKeepRangeRewardRule.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorRetreatRewardRule.cs`
- Delete: role reward rule files listed in File Structure
- Create: `Assets/Tests/EditMode/GladiatorRewardPolicyTests.cs`

- [ ] **Step 1: Write reward policy tests**

Create tests for step reward and smoothness:

```csharp
using NUnit.Framework;
using UnityEngine;

public sealed class GladiatorRewardPolicyTests
{
    [Test]
    public void EvaluateActionStep_DoesNotApplyStepReward()
    {
        GladiatorRewardConfig config = ScriptableObject.CreateInstance<GladiatorRewardConfig>();
        config.actionDelta = 0f;
        config.commandSwitchPenalty = 0f;
        config.strategySwitchPenalty = 0f;
        config.anchorSwitchPenalty = 0f;

        float reward = 0f;
        var evaluator = new GladiatorRewardEvaluator(config, value => reward += value);
        GladiatorAction action = new GladiatorAction(Vector2.zero, GladiatorStrategy.Neutral, 0, GladiatorCommand.Move);
        GladiatorTacticalContext context = GladiatorTacticalContext.ForTests(action);

        evaluator.EvaluateActionStep(action, context, default);

        Assert.AreEqual(0f, reward, 0.0001f);
    }

    [Test]
    public void SmoothnessReward_AppliesOnlyWhenBothCommandsAreMove()
    {
        GladiatorRewardConfig config = ScriptableObject.CreateInstance<GladiatorRewardConfig>();
        config.actionDelta = -0.01f;

        float reward = 0f;
        var evaluator = new GladiatorRewardEvaluator(config, value => reward += value);

        GladiatorAction first = new GladiatorAction(Vector2.right, GladiatorStrategy.Neutral, 0, GladiatorCommand.Move);
        evaluator.EvaluateActionStep(first, GladiatorTacticalContext.ForTests(first), default);

        GladiatorAction second = new GladiatorAction(Vector2.left, GladiatorStrategy.Neutral, 0, GladiatorCommand.Attack);
        GladiatorRewardEvaluation evaluation = evaluator.EvaluateActionStep(
            second,
            GladiatorTacticalContext.ForTests(second, previousCommand: GladiatorCommand.Move),
            default
        );

        Assert.AreEqual(0f, evaluation.SmoothnessReward, 0.0001f);
    }
}
```

Add `GladiatorTacticalContext.ForTests` as an internal static helper under `#if UNITY_INCLUDE_TESTS` so production construction stays explicit.

- [ ] **Step 2: Migrate tactical context**

Remove `PreviousRole`, `Role`, `PreviousAnchorKind`, `AnchorKind`, role commitment fields, and completed role window. Add:

```csharp
public readonly GladiatorStrategy? PreviousStrategy;
public readonly GladiatorStrategy Strategy;
public readonly int StrategyCommitmentSteps;
public readonly bool CompletedStrategyWindow;
```

Builder input should accept `GladiatorStrategy? previousStrategy` and `int strategyCommitmentSteps`, then compute:

```csharp
int nextStrategyCommitmentSteps =
    previousStrategy == action.Strategy ? strategyCommitmentSteps + 1 : 0;
bool completedStrategyWindow =
    previousStrategy == action.Strategy && strategyCommitmentSteps + 1 >= commitmentWindowSteps;
```

- [ ] **Step 3: Remove role rule wiring**

Change `GladiatorTacticalRewardShaper` to:

```csharp
public sealed class GladiatorTacticalRewardShaper
{
    private readonly IGladiatorFightModeRewardRule[] _strategyRules;

    public GladiatorTacticalRewardShaper(GladiatorRewardConfig config)
    {
        _strategyRules = new IGladiatorFightModeRewardRule[GladiatorActionSchema.StrategyBranchSize];
        _strategyRules[(int)GladiatorStrategy.Pressure] = new GladiatorPressureRewardRule(config);
        _strategyRules[(int)GladiatorStrategy.KeepRange] = new GladiatorKeepRangeRewardRule(config);
        _strategyRules[(int)GladiatorStrategy.Retreat] = new GladiatorRetreatRewardRule(config);
    }

    public float Evaluate(GladiatorTacticalContext context, GladiatorAction action, GladiatorCombatSignalFeatures features)
    {
        int strategyIndex = (int)action.Strategy;
        if (strategyIndex < 0 || strategyIndex >= _strategyRules.Length)
        {
            return 0f;
        }

        IGladiatorFightModeRewardRule rule = _strategyRules[strategyIndex];
        return rule != null ? rule.Evaluate(context, action, features) : 0f;
    }
}
```

Rename `IGladiatorFightModeRewardRule` to `IGladiatorStrategyRewardRule` if doing so is a small mechanical change; otherwise keep the old interface name for a separate cleanup commit.

- [ ] **Step 4: Align Strategy formulas**

Pressure rule:

```csharp
float reward = 0f;
float approachDelta = context.PreviousTargetDistance - context.TargetDistance;
if (approachDelta > 0f)
{
    reward += approachDelta * _config.pressureApproachReward;
}

if (action.Command == GladiatorCommand.Attack && context.HasValidTarget && !context.IsTargetOutOfAttackRange)
{
    reward += _config.pressureFavorableRangeReward;
}

return reward;
```

KeepRange rule:

```csharp
float effectiveRange = Mathf.Max(_config.minimumEffectiveRange, context.TargetEffectiveRange);
float distanceRatio = context.TargetDistance / effectiveRange;
float reward = 0f;
if (distanceRatio >= _config.keepRangeBandMin && distanceRatio <= _config.keepRangeBandMax)
{
    reward += _config.keepRangeBandReward;
}

float separationDelta = context.TargetDistance - context.PreviousTargetDistance;
if (distanceRatio < _config.keepRangeBandMin && separationDelta > 0f)
{
    reward += separationDelta * _config.keepRangeRecoverReward;
}

return reward;
```

Retreat rule:

```csharp
float reward = 0f;
float separationDelta = context.TargetDistance - context.PreviousTargetDistance;
if (separationDelta > 0f)
{
    reward += separationDelta * _config.retreatSeparationReward;
}

if (action.Command == GladiatorCommand.Move && action.RelativeMove.y < 0f)
{
    reward += Mathf.Abs(action.RelativeMove.y) * _config.retreatSeparationReward;
}

if (context.PreviousTargetDistance <= context.TargetEffectiveRange && context.TargetDistance > context.TargetEffectiveRange)
{
    reward += _config.retreatEscapeRangeReward;
}

return reward;
```

- [ ] **Step 5: Delete role rule files and meta files through Unity or filesystem**

Remove:

```bash
rtk proxy powershell -NoProfile -Command "Remove-Item -LiteralPath 'Assets/Scripts/BattleScene/Agent/Reward/Rules/IGladiatorRoleRewardRule.cs','Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorEngageRewardRule.cs','Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorAssassinateRewardRule.cs','Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorRegroupRewardRule.cs','Assets/Scripts/BattleScene/Agent/Reward/Rules/GladiatorPeelRewardRule.cs' -Force"
```

Let Unity regenerate or remove corresponding `.meta` files in the same change.

- [ ] **Step 6: Run tests**

Run:

```bash
rtk proxy powershell -NoProfile -Command "Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults Temp/EditModeResults.xml"
```

Expected: reward policy tests pass after compile fixes.

- [ ] **Step 7: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Shared/Combat Assets/Scripts/BattleScene/Agent/Reward Assets/Tests/EditMode/GladiatorRewardPolicyTests.cs
rtk git commit -m "feat: replace gladiator role rewards with strategy rewards"
```

### Task 5: Align Core Rewards And Team Outcome Rewards

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorRewardConfig.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorRewardEvaluator.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingAgentBinder.cs`
- Modify: `Assets/ML-Agents/GladiatorRewardConfig.asset`

- [ ] **Step 1: Change reward config fields**

Remove `step`, `invalidAction`, role switch/commitment/reward fields. Add:

```csharp
[Header("Terminal Survival")]
[FieldDescription("전투 종료 시 살아남은 agent에게만 지급할 수 있는 작은 선택적 생존 보상.")]
public float terminalSurvivalBonus = 0f;

[Header("Personality Mixing")]
[FieldDescription("성격 bias가 개별 reward category를 증폭할 때 적용되는 최대 절대 배율.")]
public float personalityCategoryWeightMin = 0.5f;

[FieldDescription("성격 bias가 개별 reward category를 증폭할 때 적용되는 최대 절대 배율.")]
public float personalityCategoryWeightMax = 1.5f;
```

Rename fields:

```csharp
public float strategySwitchPenalty = -0.02f;
public float strategyCommitmentReward = 0f;
```

- [ ] **Step 2: Remove step and role logic from evaluator**

Start action-step reward at zero:

```csharp
float reward = 0f;
float smoothnessReward = EvaluateSmoothness(action, context);
reward += smoothnessReward;
reward += EvaluateCommandSwitch(context);
reward += EvaluateStrategySwitch(context);
reward += EvaluateAnchorSwitch(context);
reward += EvaluateCommitment(context);
reward += _tacticalRewardShaper.Evaluate(context, action, features);
```

Replace fight-mode switch with Strategy:

```csharp
private float EvaluateStrategySwitch(GladiatorTacticalContext context)
{
    if (!context.PreviousStrategy.HasValue || context.Strategy == context.PreviousStrategy)
    {
        return 0f;
    }

    return _config.strategySwitchPenalty;
}
```

Commitment:

```csharp
if (context.CompletedStrategyWindow)
{
    reward += _config.strategyCommitmentReward;
}
```

- [ ] **Step 3: Keep invalid Attack canonicalization but no invalid penalty**

In `GladiatorAgent.ResolveExecutableAction`, keep:

```csharp
if (action.Command != GladiatorCommand.Move && !context.HasValidTarget)
{
    return action.WithCommand(GladiatorCommand.Move);
}
```

Do not add reward or penalty there. Invalid attack frequency should be handled by action masks first.

- [ ] **Step 4: Clamp MA-POCA group rewards**

In `TrainingAgentBinder.EndTrainingGroups`, clamp calculated values:

```csharp
private static float NormalizeTeamOutcomeReward(float reward) => Mathf.Clamp(reward, -100f, 100f);
```

Apply it to all `AddGroupReward` calls:

```csharp
_allyGroup.AddGroupReward(NormalizeTeamOutcomeReward(
    (allyWon ? _settings.GroupWinReward : _settings.GroupLossReward) * combinedMultiplier
));
```

Timeout:

```csharp
float interruptionReward =
    reason == TrainingEpisodeEndReason.Timeout ? NormalizeTeamOutcomeReward(timeoutReward) : _settings.GroupInterruptedReward;
```

- [ ] **Step 5: Update reward asset defaults**

Using Unity Inspector or a YAML-aware edit, set `GladiatorRewardConfig.asset` values to policy defaults:

```text
damageDealtRatio: 1
damageTakenRatio: -1
attackLanded: 0.05
kill: 3
death: -3
actionDelta: -0.001
commandSwitchPenalty: -0.02
strategySwitchPenalty: -0.02
anchorSwitchPenalty: -0.05
commandCommitmentReward: 0
strategyCommitmentReward: 0
anchorCommitmentReward: 0
groupWin: 10
groupLoss: -10
winSpeedBonus: 1.5
winHpBonus: 1.5
timeoutMultiplier: 1.2
timeoutHpRatioMultiplierMax: 1.5
```

- [ ] **Step 6: Run tests and build**

Run:

```bash
rtk proxy powershell -NoProfile -Command "Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults Temp/EditModeResults.xml"
rtk python tools/repair_unity_csproj.py --build
```

Expected: all EditMode tests pass; C# build succeeds.

- [ ] **Step 7: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Reward Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingAgentBinder.cs Assets/ML-Agents/GladiatorRewardConfig.asset
rtk git commit -m "feat: align gladiator reward policy"
```

### Task 6: Add Personality Bias As Reward Category Mixing

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Reward/GladiatorRewardEvaluator.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Agent/GladiatorAgent.cs`
- Modify: personality data source files under `Assets/Scripts/SOScripts` or `Assets/Scripts/BattleScene/Agent/TrainingSetup/Gladiators` after locating the actual field owner
- Modify: `Assets/Scripts/BattleScene/Agent/Observation/GladiatorObservationBuilder.cs`

- [ ] **Step 1: Locate personality source**

Run:

```bash
rtk rg -n "Personality|Collectiv|Passive|Aggressive|Individual" Assets/Scripts Assets/Content/Personalities
```

Expected: identify the ScriptableObject or runtime snapshot that owns personality values for each unit. Use that source in the following steps.

- [ ] **Step 2: Add neutral bias value object**

Create or add near reward evaluator:

```csharp
public readonly struct GladiatorPersonalityBias
{
    public readonly float Collectivism;
    public readonly float Passiveness;

    public GladiatorPersonalityBias(float collectivism, float passiveness)
    {
        Collectivism = Mathf.Clamp01(collectivism);
        Passiveness = Mathf.Clamp01(passiveness);
    }

    public static GladiatorPersonalityBias Neutral => new GladiatorPersonalityBias(0.5f, 0.5f);
}
```

- [ ] **Step 3: Add individual-only personality weight methods**

In evaluator:

```csharp
private const float PersonalityWeightEpsilon = 0.0001f;

private float CollectivismIndividualScale(GladiatorPersonalityBias bias)
{
    float teamWeight = Mathf.Lerp(0.8f, 1.2f, bias.Collectivism);
    float individualWeight = Mathf.Lerp(1.2f, 0.8f, bias.Collectivism);
    float scale = individualWeight / Mathf.Max(PersonalityWeightEpsilon, teamWeight);
    return Mathf.Clamp(scale, _config.personalityCategoryWeightMin, _config.personalityCategoryWeightMax);
}

private float DamageCategoryWeight(GladiatorPersonalityBias bias)
{
    float damageWeight = Mathf.Lerp(1.2f, 0.8f, bias.Passiveness);
    return CollectivismIndividualScale(bias) * damageWeight;
}

private float SurvivalCategoryWeight(GladiatorPersonalityBias bias)
{
    float survivalWeight = Mathf.Lerp(0.8f, 1.2f, bias.Passiveness);
    return CollectivismIndividualScale(bias) * survivalWeight;
}

private float StrategyCategoryWeight(GladiatorStrategy strategy)
{
    switch (strategy)
    {
        case GladiatorStrategy.Pressure:
            return DamageCategoryWeight(_bias);
        case GladiatorStrategy.KeepRange:
        case GladiatorStrategy.Retreat:
            return SurvivalCategoryWeight(_bias);
        default:
            return 1f;
    }
}
```

Apply:

```csharp
public GladiatorRewardEvaluation EvaluateActionStep(
    GladiatorAction action,
    GladiatorTacticalContext context,
    GladiatorCombatSignalFeatures features
)
{
    float reward = 0f;
    float smoothnessReward = EvaluateSmoothness(action, context);
    reward += smoothnessReward;
    reward += EvaluateCommandSwitch(context);
    reward += EvaluateStrategySwitch(context);
    reward += EvaluateAnchorSwitch(context);
    reward += EvaluateCommitment(context);

    float strategyReward = _tacticalRewardShaper.Evaluate(context, action, features);
    reward += strategyReward * StrategyCategoryWeight(action.Strategy);

    RecordLastActionContext(action, context);
    ApplyReward(reward);

    return new GladiatorRewardEvaluation(reward, smoothnessReward);
}

public void RewardDamageTaken(float damage, BattleUnitCombatState selfState)
{
    float ratio = selfState != null && selfState.MaxHealth > 0f ? Mathf.Max(0f, damage) / selfState.MaxHealth : 0f;
    ApplyReward(
        (ratio * _config.damageTakenRatio + ratio * EvaluateConditionalDamageTakenReward())
            * SurvivalCategoryWeight(_bias)
    );
}

public void RewardDeath()
{
    ApplyReward(_config.death * SurvivalCategoryWeight(_bias));
}

public void RewardAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
{
    float ratio = target != null && target.State != null && target.State.MaxHealth > 0f
        ? Mathf.Max(0f, actualDamage) / target.State.MaxHealth
        : 0f;

    float reward = (_config.attackLanded + ratio * _config.damageDealtRatio) * DamageCategoryWeight(_bias);
    if (wasKill)
    {
        reward += _config.kill * DamageCategoryWeight(_bias);
    }

    ApplyReward(reward);
}
```

Do not apply `teamWeight` to MA-POCA group rewards in `TrainingAgentBinder`. Team outcome reward must remain identical across personalities; `teamWeight` is only the denominator in the individual reward scale:

```text
modified individual reward := original individual reward * (weighted individual reward / weighted team reward)
weighted individual reward := individualWeight * categoryWeight
weighted team reward := teamWeight
```

Strategy shaping is part of individual reward. Apply the same individual-only personality scale to it:

```text
Pressure strategy reward -> DamageCategoryWeight
KeepRange strategy reward -> SurvivalCategoryWeight
Retreat strategy reward -> SurvivalCategoryWeight
Neutral strategy reward -> no shaping reward
```

- [ ] **Step 4: Wire bias from agent initialization**

In `GladiatorAgent.Initialize`, resolve:

```csharp
GladiatorPersonalityBias bias = ResolvePersonalityBias(unit);
_rewardEvaluator = new GladiatorRewardEvaluator(rewardConfig, AddReward, bias);
_personalityBias = bias;
```

Implement neutral fallback:

```csharp
private static GladiatorPersonalityBias ResolvePersonalityBias(BattleRuntimeUnit unit)
{
    if (unit == null || unit.Snapshot == null)
    {
        return GladiatorPersonalityBias.Neutral;
    }

    return GladiatorPersonalityBias.Neutral;
}
```

Replace the neutral return with actual personality fields found in Step 1 in the same task.

- [ ] **Step 5: Put bias into observation**

Return from `ResolveCollectivismBias` and `ResolvePassivenessBias`:

```csharp
private float ResolveCollectivismBias() => _personalityBias.Collectivism;
private float ResolvePassivenessBias() => _personalityBias.Passiveness;
```

- [ ] **Step 6: Run build**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
```

Expected: build succeeds.

- [ ] **Step 7: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent Assets/Scripts/SOScripts Assets/Scripts/BattleScene/Agent/TrainingSetup/Gladiators
rtk git commit -m "feat: apply gladiator personality reward bias"
```

### Task 7: Optimize And Rename Metrics

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Logging/GladiatorAgentEpisodeMetrics.cs`
- Create: `Assets/Tests/EditMode/GladiatorMetricsTests.cs`

- [ ] **Step 1: Replace per-step StatsRecorder share writes with local accumulators**

Add fields:

```csharp
private const int MetricFlushDecisionSteps = 10000;

private readonly int[] _commandShareCounts = new int[GladiatorActionSchema.CommandBranchSize];
private readonly int[] _strategyShareCounts = new int[GladiatorActionSchema.StrategyBranchSize];
private readonly int[] _anchorSlotShareCounts = new int[GladiatorActionSchema.AnchorActionBranchSize];
private int _localMetricStepCount;
```

In `RecordAction`, replace `RecordActionShares(Academy.Instance.StatsRecorder, action)` with:

```csharp
_commandShareCounts[(int)action.Command]++;
_strategyShareCounts[(int)action.Strategy]++;
_anchorSlotShareCounts[action.AnchorSlot]++;
_localMetricStepCount++;

if (_localMetricStepCount >= MetricFlushDecisionSteps)
{
    FlushLocalAverages(Academy.Instance.StatsRecorder);
}
```

- [ ] **Step 2: Precompute metric keys**

Add static key arrays:

```csharp
private static readonly string[] CommandShareKeys =
{
    "Combat/CommandShare/Move",
    "Combat/CommandShare/Attack",
};

private static readonly string[] StrategyShareKeys =
{
    "Combat/StrategyShare/Neutral",
    "Combat/StrategyShare/Pressure",
    "Combat/StrategyShare/KeepRange",
    "Combat/StrategyShare/Retreat",
};

private static readonly string[] StrategyAnchorRangeOffsetKeys =
{
    "Combat/StrategyAnchorRangeOffset/Neutral",
    "Combat/StrategyAnchorRangeOffset/Pressure",
    "Combat/StrategyAnchorRangeOffset/KeepRange",
    "Combat/StrategyAnchorRangeOffset/Retreat",
};
```

Do not use string interpolation in hot paths.

- [ ] **Step 3: Flush conditional averages**

Implement:

```csharp
private void FlushLocalAverages(StatsRecorder recorder)
{
    if (_localMetricStepCount <= 0)
    {
        return;
    }

    float inverse = 1f / _localMetricStepCount;
    for (int i = 0; i < _commandShareCounts.Length; i++)
    {
        recorder.Add(CommandShareKeys[i], _commandShareCounts[i] * inverse, StatAggregationMethod.Average);
    }

    for (int i = 0; i < _strategyShareCounts.Length; i++)
    {
        recorder.Add(StrategyShareKeys[i], _strategyShareCounts[i] * inverse, StatAggregationMethod.Average);
    }

    Array.Clear(_commandShareCounts, 0, _commandShareCounts.Length);
    Array.Clear(_strategyShareCounts, 0, _strategyShareCounts.Length);
    Array.Clear(_anchorSlotShareCounts, 0, _anchorSlotShareCounts.Length);
    _localMetricStepCount = 0;
}
```

Call `FlushLocalAverages(recorder)` at the start of `Flush()` before final episode metrics.

- [ ] **Step 4: Rename metrics**

Replace:

```text
Combat/FightModeSwitch -> Combat/StrategySwitch
Combat/FightModeMaintenance -> Combat/StrategyMaintenance
Combat/FightModeShare/* -> Combat/StrategyShare/*
Combat/FightModeAnchorRangeOffset/* -> Combat/StrategyAnchorRangeOffset/*
```

Remove Role metrics:

```text
Combat/RoleSwitch
Combat/RoleMaintenance
Combat/RoleShare/*
```

- [ ] **Step 5: Compile and smoke test**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
```

Expected: build succeeds. If a testable wrapper is added for key arrays, `GladiatorMetricsTests` verifies there is no `$` interpolation path and key counts equal enum counts.

- [ ] **Step 6: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Shared/Logging Assets/Tests/EditMode/GladiatorMetricsTests.cs
rtk git commit -m "perf: batch gladiator agent metrics"
```

### Task 8: Remove Curriculum And Update Training Config

**Files:**
- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Environment/IGladiatorCurriculumSource.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/Shared/Environment/GladiatorAnchorCurriculum.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/Curriculum/TrainingCurriculumParameterNames.cs`
- Modify: `Assets/Scripts/BattleScene/Agent/TrainingSetup/TrainingBootstrapper.cs`
- Modify: `Assets/ML-Agents/GladiatorBehavior.yaml`

- [ ] **Step 1: Remove Role curriculum API**

Change interface to:

```csharp
public interface IGladiatorCurriculumSource
{
    float BattleTimeoutRemainingRatio { get; }
}
```

Remove `CurrentRoleCurriculum` and `CurrentAnchorCurriculum` consumers in `GladiatorAgent`.

- [ ] **Step 2: Remove role curriculum parameter**

In `TrainingCurriculumParameterNames`, keep:

```csharp
public const string OpponentMode = "opponent_mode";
public const string TeamSize = "team_size";
```

Remove `role_curriculum` and `anchor_curriculum` uses from `TrainingBootstrapper`.

- [ ] **Step 3: Update trainer YAML**

Change:

```yaml
behaviors:
  GladiatorBehavior:
    summary_freq: 50000
environment_parameters:
  opponent_mode: 1.0
  team_size: 6.0
```

Keep existing hyperparameters unless training evidence says otherwise.

- [ ] **Step 4: Compile check**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
```

Expected: no references to role/anchor curriculum remain.

- [ ] **Step 5: Search verification**

Run:

```bash
rtk rg -n "RoleCurriculum|role_curriculum|AnchorCurriculum|anchor_curriculum|GladiatorActionRole|GladiatorAnchorKind|FightModeBranch|RoleBranch" Assets/Scripts Assets/ML-Agents docs
```

Expected: only historical docs or migration notes mention removed names. Runtime C# should not.

- [ ] **Step 6: Commit**

```bash
rtk git add Assets/Scripts/BattleScene/Agent/Shared/Environment Assets/Scripts/BattleScene/Agent/TrainingSetup Assets/ML-Agents/GladiatorBehavior.yaml
rtk git commit -m "chore: remove gladiator role curriculum"
```

### Task 9: Update Assets, Inference Contract, And Documentation

**Files:**
- Modify: `Assets/ML-Agents/BattleMlAgentInferenceConfig.asset`
- Modify: `Assets/ML-Agents/GladiatorRewardConfig.asset`
- Modify: `docs/ML-Agent/usage.md`
- Modify: `docs/ML-Agent/academy.md`

- [ ] **Step 1: Update inference asset**

Set:

```text
contractVersion: 14
expectedContinuousActions: 2
expectedObservationSize: GladiatorObservationSchema.TotalSize value after Task 3
```

If the asset stores only scalar values, use the computed integer value from `GladiatorObservationSchema.TotalSize`.

- [ ] **Step 2: Update behavior comments and docs**

In docs, describe:

```text
Continuous Actions:
- 0: anchor strafe
- 1: anchor forward

Discrete Branches:
- Branch 0: Command, size 2
- Branch 1: Strategy, size 4
- Branch 2: Anchor enemy slot, size 6
```

Remove references to Role, Ally Anchor, TeamCenter Anchor, and `FightMode` metric names from user-facing ML-Agent usage docs.

- [ ] **Step 3: Build and Unity test**

Run:

```bash
rtk python tools/repair_unity_csproj.py --build
rtk proxy powershell -NoProfile -Command "Unity -batchmode -quit -projectPath . -runTests -testPlatform EditMode -testResults Temp/EditModeResults.xml"
```

Expected: build succeeds and EditMode tests pass.

- [ ] **Step 4: Final runtime smoke checks**

Run a short training or inference scene from Unity and verify these conditions manually:

```text
BehaviorParameters shows 2 continuous actions and 3 discrete branches.
Branch sizes are 2 / 4 / 6.
Agents only select living enemy slots as anchors.
Attack without a valid target is executed as Move.
TensorBoard metrics include Combat/StrategyShare/* and do not include Combat/RoleShare/*.
summary_freq is 50000.
```

- [ ] **Step 5: Commit**

```bash
rtk git add Assets/ML-Agents docs/ML-Agent
rtk git commit -m "docs: update gladiator ml-agent policy contract"
```

---

## Self-Review

- Spec coverage: Action contract is covered by Tasks 1-2. Observation contract is covered by Task 3. Reward policy and Strategy rewards are covered by Tasks 4-5. Personality bias is covered by Task 6. Curriculum removal is covered by Task 8. Metrics and `summary_freq` are covered by Tasks 7-8. Assets/docs are covered by Task 9.
- Known plan constraint: the codebase currently uses `GladiatorFightMode` in some simulation state APIs. The plan allows a narrow compatibility cast only while migrating; remove the old name if the rename remains small after compile errors are resolved.
- Verification: use `rtk python tools/repair_unity_csproj.py --build` instead of direct `dotnet build Assembly-CSharp.csproj --no-restore`, per project instructions.
