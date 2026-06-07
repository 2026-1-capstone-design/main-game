using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class GladiatorAgent : Agent
{
    private const int CommitmentWindowSteps = 8;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    [Header("Heuristic (ONLY Demo Recording)")]
    [SerializeField]
    private bool useBuiltInAiHeuristic = false;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private IGladiatorCurriculumSource _curriculumSource;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;
    private BattleUnitCombatState _selfState;
    private GladiatorStateRosterView _rosterView;
    private GladiatorObservationStats _observationStats;
    private float _prevTargetDistance;
    private GladiatorCommand? _previousCommand;
    private int _previousTargetSlot = -1;
    private GladiatorStrategy? _previousStrategy;
    private int _commandCommitmentSteps;
    private int _anchorCommitmentSteps;
    private int _strategyCommitmentSteps;
    private GladiatorRewardEvaluator _rewardEvaluator;
    private GladiatorPersonalityBias _personalityBias = GladiatorPersonalityBias.Neutral;
    private RuntimeUnitAgentActionSink _actionSink;
    private BattleAgentControlBuffer _agentControlBuffer;
    private LegacyBuiltInAiPlanner _aiHeuristic;
    private readonly GladiatorAgentEpisodeMetrics _episodeMetrics = new GladiatorAgentEpisodeMetrics();

    public bool HasControlledUnit => _selfUnit != null;

    public void Initialize(
        BattleRuntimeUnit unit,
        BattleSceneFlowManager flowManager,
        IGladiatorCurriculumSource curriculumSource
    )
    {
        if (rewardConfig == null)
        {
            Debug.LogError("[GladiatorAgent] Reward config is required.", this);
            enabled = false;
            return;
        }

        CleanupSubscriptions();

        _selfUnit = unit;
        _selfState = unit != null ? unit.State : null;
        _flowManager = flowManager;
        _curriculumSource = curriculumSource;

        SphereCollider col = flowManager?.battlefieldCollider;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        _arenaExtentsMin = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : float.MaxValue;
        _rosterView = CreateRosterView();
        _personalityBias = ResolvePersonalityBias(unit);
        _rewardEvaluator = new GladiatorRewardEvaluator(rewardConfig, AddReward, _personalityBias);
        _rewardEvaluator.Reset();
        _agentControlBuffer =
            _flowManager != null && _flowManager.BattleSimulationManager != null
                ? _flowManager.BattleSimulationManager.AgentControlBuffer
                : null;
        _actionSink = new RuntimeUnitAgentActionSink(
            _selfState,
            _flowManager != null ? _flowManager.RuntimeUnits : null,
            _agentControlBuffer
        );
        _observationStats = ComputeInitialObservationStats();

        if (_selfUnit != null)
        {
            _flowManager?.BattleSimulationManager?.SetUnitControlMode(_selfState, BattleUnitControlMode.AgentPolicy);
            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
        }

        _prevTargetDistance = float.MaxValue;
        _previousCommand = null;
        _previousTargetSlot = -1;
        _previousStrategy = null;
        _commandCommitmentSteps = 0;
        _anchorCommitmentSteps = 0;
        _strategyCommitmentSteps = 0;
        _episodeMetrics.Reset(_personalityBias);

        if (useBuiltInAiHeuristic)
        {
            _aiHeuristic = new LegacyBuiltInAiPlanner();
        }
        else
        {
            _aiHeuristic = null;
        }
    }

    private void HandleDamageTaken(float damage)
    {
        _rewardEvaluator?.RewardDamageTaken(damage, _selfState);
    }

    private void HandleSelfDied()
    {
        _rewardEvaluator?.RewardDeath();
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
    {
        _episodeMetrics.AddDamageDealt(actualDamage);
        _rewardEvaluator?.RewardAttackLanded(target, actualDamage, wasKill);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        GladiatorObservationBuilder.Write(sensor, CreateObservationContext());
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfState == null || _selfState.IsCombatDisabled || _rosterView == null)
        {
            return;
        }

        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        int[] branchSizes =
            behaviorParameters != null ? behaviorParameters.BrainParameters.ActionSpec.BranchSizes : null;
        if (branchSizes == null)
        {
            return;
        }

        ApplyAnchorActionMask(actionMask, branchSizes);
        ApplyStrategyMask(actionMask, branchSizes);
        ApplyCommandMask(actionMask, branchSizes);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfState == null || _selfState.IsCombatDisabled)
        {
            return;
        }

        GladiatorAction action = GladiatorAgentActionParser.Parse(actions);
        BattleUnitCombatState selectedTarget = ResolveAnchorTarget(action);
        GladiatorAction resolvedAction = action.Resolve(
            _selfState.Position,
            _rosterView != null ? _rosterView.Hostiles : null,
            selectedTarget,
            out BattleUnitCombatState target,
            out bool anchorFallbackApplied
        );
        GladiatorObservationContext observationContext = CreateObservationContext();
        GladiatorTacticalContext tacticalContext = GladiatorTacticalContext.Builder.Build(
            _selfState,
            _rosterView != null ? _rosterView.Hostiles : null,
            action,
            target,
            CommitmentWindowSteps,
            _previousCommand,
            _previousTargetSlot,
            _previousStrategy,
            _commandCommitmentSteps,
            _anchorCommitmentSteps,
            _strategyCommitmentSteps,
            _prevTargetDistance,
            anchorFallbackApplied
        );
        _episodeMetrics.RecordAction(
            action,
            tacticalContext,
            _selfState != null ? _selfState.Attack : 0f,
            _selfState != null ? _selfState.AttackSpeed : 0f,
            GetStepDurationSeconds()
        );
        GladiatorCombatSignalFeatures features = GladiatorCombatSignalFeatures.Builder.Build(observationContext);
        GladiatorRewardEvaluation evaluation = _rewardEvaluator.EvaluateActionStep(
            action,
            tacticalContext,
            features,
            observationContext
        );
        _episodeMetrics.RecordStrategyReward(action.Strategy, evaluation.StrategyReward);
        _episodeMetrics.RecordSmoothnessReward(evaluation.SmoothnessReward);
        _episodeMetrics.RecordAllyCrowdingReward(evaluation.AllyCrowdingReward);

        UpdateCommitmentState(action, tacticalContext);
        _previousCommand = action.Command;
        _previousTargetSlot = action.AnchorSlot;
        _previousStrategy = action.Strategy;
        _prevTargetDistance = tacticalContext.TargetDistance;

        BattleUnitCombatState effectiveTarget = tacticalContext.HasValidTarget ? target : null;
        _actionSink?.Apply(action, resolvedAction, effectiveTarget);
    }

    public override void OnEpisodeBegin()
    {
        _prevTargetDistance = float.MaxValue;
        _previousCommand = null;
        _previousTargetSlot = -1;
        _previousStrategy = null;
        _commandCommitmentSteps = 0;
        _anchorCommitmentSteps = 0;
        _strategyCommitmentSteps = 0;
        _rewardEvaluator?.Reset();
        _actionSink?.Clear();
        _episodeMetrics.Reset(_personalityBias);
    }

    public void FlushEpisodeMetrics()
    {
        _episodeMetrics.Flush();
    }

    public void RewardTerminalSurvivalIfAlive()
    {
        if (_selfState == null || _selfState.IsCombatDisabled)
        {
            return;
        }

        _rewardEvaluator?.RewardTerminalSurvival();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (useBuiltInAiHeuristic && _aiHeuristic != null && _selfState != null)
        {
            BattleSimulationManager simulationManager = _flowManager?.BattleSimulationManager;
            var context = new BattlePlanningContext(
                _flowManager != null ? _flowManager.RuntimeUnits : null,
                simulationManager != null ? simulationManager.CurrentSnapshot : null,
                simulationManager != null ? simulationManager.aiTuning : null,
                simulationManager != null ? 1f / simulationManager.simulationTickRate : 1f / 15f
            );
            if (_aiHeuristic.TryBuildPlan(_selfState, context, out BattleControlPlan plan))
            {
                BuiltInAiHeuristicTranslator.Write(actionsOut, plan, _selfState, _rosterView);
                return;
            }
        }

        var kb = Keyboard.current;
        var continuous = actionsOut.ContinuousActions;
        var discrete = actionsOut.DiscreteActions;
        if (kb == null)
        {
            return;
        }

        if (continuous.Length >= GladiatorActionSchema.ContinuousSize)
        {
            continuous[GladiatorActionSchema.ContinuousAnchorStrafe] =
                (kb.aKey.isPressed ? 1f : 0f) + (kb.dKey.isPressed ? -1f : 0f);
            continuous[GladiatorActionSchema.ContinuousAnchorForward] =
                (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
        }

        if (discrete.Length < GladiatorActionSchema.DiscreteBranchCount)
        {
            return;
        }

        if (kb.jKey.isPressed)
            discrete[GladiatorActionSchema.CommandBranch] = (int)GladiatorCommand.Attack;
        else if (kb.xKey.isPressed)
            discrete[GladiatorActionSchema.CommandBranch] = (int)GladiatorCommand.Withdraw;
        else
            discrete[GladiatorActionSchema.CommandBranch] = (int)GladiatorCommand.Move;

        if (kb.digit1Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(0);
        else if (kb.digit2Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(1);
        else if (kb.digit3Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(2);
        else if (kb.digit4Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(3);
        else if (kb.digit5Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(4);
        else if (kb.digit6Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(5);
        else
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(0);

        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            discrete[GladiatorActionSchema.StrategyBranch] = (int)GladiatorStrategy.Pressure;
        else if (kb.sKey.isPressed)
            discrete[GladiatorActionSchema.StrategyBranch] = (int)GladiatorStrategy.KeepRange;
        else if (kb.xKey.isPressed)
            discrete[GladiatorActionSchema.StrategyBranch] = (int)GladiatorStrategy.Retreat;
        else
            discrete[GladiatorActionSchema.StrategyBranch] = (int)GladiatorStrategy.Neutral;
    }

    private float GetStepDurationSeconds()
    {
        BattleSimulationManager simulationManager = _flowManager != null ? _flowManager.BattleSimulationManager : null;
        float tickRate = simulationManager != null ? simulationManager.simulationTickRate : 15f;
        return 1f / Mathf.Max(1f, tickRate);
    }

    private BattleUnitCombatState ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(slotIndex) : null;

    private BattleUnitCombatState ResolveAnchorTarget(GladiatorAction action) => ResolveOpponentSlot(action.AnchorSlot);

    private GladiatorObservationContext CreateObservationContext()
    {
        float arenaRadius = _selfUnit != null ? _arenaExtentsMin - _selfUnit.BodyRadius : float.MaxValue;
        BattleAgentControlInput controlInput =
            _agentControlBuffer != null ? _agentControlBuffer.GetInputSnapshot(_selfState) : default;
        BattleUnitCombatState currentAnchor = ResolveObservationAnchor(controlInput);

        return new GladiatorObservationContext(
            _selfState,
            _rosterView != null ? _rosterView.Teammates : null,
            _rosterView != null ? _rosterView.Hostiles : null,
            _observationStats,
            _arenaCenter,
            ComputeTeamCenter(),
            arenaRadius,
            _curriculumSource != null ? _curriculumSource.BattleTimeoutRemainingRatio : 1f,
            controlInput.RawLocalMove,
            controlInput.PreviousRawLocalMove,
            controlInput.AnchorSlot,
            ToAgentCommand(controlInput.Command),
            controlInput.Strategy,
            _anchorCommitmentSteps,
            _strategyCommitmentSteps,
            _personalityBias.Collectivism,
            _personalityBias.Passiveness,
            currentAnchor
        );
    }

    private static GladiatorCommand ToAgentCommand(BattleCombatCommand command) =>
        command switch
        {
            BattleCombatCommand.BasicAttack => GladiatorCommand.Attack,
            BattleCombatCommand.Withdraw => GladiatorCommand.Withdraw,
            _ => GladiatorCommand.Move,
        };

    private Vector3 ComputeTeamCenter()
    {
        if (_selfState == null)
        {
            return _arenaCenter;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        if (!_selfState.IsCombatDisabled)
        {
            sum += _selfState.Position;
            count++;
        }

        IReadOnlyList<BattleUnitCombatState> teammates = _rosterView != null ? _rosterView.Teammates : null;
        if (teammates != null)
        {
            for (int i = 0; i < teammates.Count; i++)
            {
                BattleUnitCombatState teammate = teammates[i];
                if (teammate == null || teammate.IsCombatDisabled)
                {
                    continue;
                }

                sum += teammate.Position;
                count++;
            }
        }

        return count > 0 ? sum / count : _selfState.Position;
    }

    private BattleUnitCombatState ResolveObservationAnchor(BattleAgentControlInput controlInput)
    {
        if (IsValidAnchorTarget(controlInput.Target))
        {
            return controlInput.Target;
        }

        if (
            _previousTargetSlot < 0
            && TryResolveObservationNearestOpponentAnchor(out BattleUnitCombatState initialAnchor, out _)
        )
        {
            return initialAnchor;
        }

        BattleUnitCombatState selectedAnchor = ResolveOpponentSlot(controlInput.AnchorSlot);
        if (IsValidAnchorTarget(selectedAnchor))
        {
            return selectedAnchor;
        }

        if (TryResolveObservationNearestOpponentAnchor(out BattleUnitCombatState fallbackAnchor, out _))
        {
            return fallbackAnchor;
        }

        return null;
    }

    private void ApplyAnchorActionMask(IDiscreteActionMask actionMask, int[] branchSizes)
    {
        if (branchSizes.Length <= GladiatorActionSchema.AnchorBranch)
        {
            return;
        }

        int branchSize = branchSizes[GladiatorActionSchema.AnchorBranch];
        bool hasEnabledAnchor = false;
        for (int i = 0; i < branchSize; i++)
        {
            bool isValidEnemySlot = IsValidEnemySlot(i);
            hasEnabledAnchor |= isValidEnemySlot;
            if (!isValidEnemySlot)
            {
                actionMask.SetActionEnabled(GladiatorActionSchema.AnchorBranch, i, false);
            }
        }

        if (!hasEnabledAnchor && branchSize > 0)
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.AnchorBranch, 0, true);
        }
    }

    private void ApplyStrategyMask(IDiscreteActionMask actionMask, int[] branchSizes)
    {
        if (branchSizes.Length <= GladiatorActionSchema.StrategyBranch)
        {
            return;
        }

        if (IsKeepRangeUnsupported())
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.StrategyBranch, (int)GladiatorStrategy.KeepRange, false);
        }
    }

    private void ApplyCommandMask(IDiscreteActionMask actionMask, int[] branchSizes)
    {
        if (branchSizes.Length <= GladiatorActionSchema.CommandBranch)
        {
            return;
        }

        int branchSize = branchSizes[GladiatorActionSchema.CommandBranch];
        bool canAttack = HasLivingOpponent();
        if (!canAttack)
        {
            DisableCommandIfPresent(actionMask, branchSize, GladiatorCommand.Attack);
            DisableCommandIfPresent(actionMask, branchSize, GladiatorCommand.Withdraw);
        }
    }

    private static void DisableCommandIfPresent(
        IDiscreteActionMask actionMask,
        int branchSize,
        GladiatorCommand command
    )
    {
        int commandIndex = (int)command;
        if (commandIndex < branchSize)
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.CommandBranch, commandIndex, false);
        }
    }

    private bool IsKeepRangeUnsupported()
    {
        // Temp: 원거리 무기가 아니어도 창 같은 중거리 무기도 있어서, 일단 모든 무기에 대해 카이팅이 가능하도록 함
        // return _selfUnit == null || _selfUnit.Snapshot == null || !_selfUnit.Snapshot.IsRanged;
        return _selfUnit == null || _selfUnit.Snapshot == null;
    }

    private bool IsValidEnemySlot(int slot)
    {
        BattleUnitCombatState target = ResolveOpponentSlot(slot);
        return target != null && !target.IsCombatDisabled;
    }

    private bool HasLivingOpponent()
    {
        if (_rosterView == null)
        {
            return false;
        }

        IReadOnlyList<BattleUnitCombatState> hostiles = _rosterView.Hostiles;
        for (int i = 0; i < hostiles.Count; i++)
        {
            BattleUnitCombatState target = hostiles[i];
            if (target != null && !target.IsCombatDisabled)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidAnchorTarget(BattleUnitCombatState target) => target != null && !target.IsCombatDisabled;

    private bool TryResolveObservationNearestOpponentAnchor(out BattleUnitCombatState target, out int slot)
    {
        target = null;
        slot = 0;
        if (_selfState == null || _rosterView == null)
        {
            return false;
        }

        IReadOnlyList<BattleUnitCombatState> hostiles = _rosterView.Hostiles;
        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < hostiles.Count && i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState candidate = hostiles[i];
            if (candidate == null || candidate.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = candidate.Position - _selfState.Position;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            target = candidate;
            slot = i;
        }

        return target != null;
    }

    private void UpdateCommitmentState(GladiatorAction action, GladiatorTacticalContext context)
    {
        _commandCommitmentSteps = context.CommandCommitmentSteps;
        _anchorCommitmentSteps = context.AnchorCommitmentSteps;
        _strategyCommitmentSteps = context.StrategyCommitmentSteps;
    }

    private void OnDestroy()
    {
        CleanupSubscriptions();
    }

    private void CleanupSubscriptions()
    {
        if (_selfUnit == null)
        {
            return;
        }

        if (_selfUnit.State != null)
        {
            _selfUnit.State.OnDamageTaken -= HandleDamageTaken;
            _selfUnit.State.OnDied -= HandleSelfDied;
        }

        _selfUnit.OnAttackLanded -= HandleAttackLanded;
    }

    private GladiatorStateRosterView CreateRosterView()
    {
        BattleStartPayload payload = _flowManager != null ? _flowManager.CurrentPayload : null;
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        return new GladiatorStateRosterView(_selfState, payload, ToStates(runtimeUnits), useSelfRandomizedSlots: true);
    }

    private GladiatorObservationStats ComputeInitialObservationStats()
    {
        float maxRosterMaxHealth = 0f;
        float maxMoveSpeed = 0f;

        IReadOnlyList<BattleUnitCombatState> states = ToStates(_flowManager != null ? _flowManager.RuntimeUnits : null);
        bool sawSelf = false;
        if (states != null)
        {
            for (int i = 0; i < states.Count; i++)
            {
                BattleUnitCombatState state = states[i];
                if (state == _selfState)
                {
                    sawSelf = true;
                }

                AddInitialUnitStats(state, ref maxRosterMaxHealth, ref maxMoveSpeed);
            }
        }

        if (!sawSelf)
        {
            AddInitialUnitStats(_selfState, ref maxRosterMaxHealth, ref maxMoveSpeed);
        }

        float fallbackMaxHealth = _selfState != null ? _selfState.MaxHealth : 1f;
        float fallbackMoveSpeed = _selfState != null ? _selfState.MoveSpeed : 1f;

        return new GladiatorObservationStats(
            maxRosterMaxHealth > 0f ? maxRosterMaxHealth : fallbackMaxHealth,
            maxMoveSpeed > 0f ? maxMoveSpeed : fallbackMoveSpeed
        );
    }

    private static void AddInitialUnitStats(
        BattleUnitCombatState state,
        ref float maxRosterMaxHealth,
        ref float maxMoveSpeed
    )
    {
        if (state == null)
        {
            return;
        }

        if (state.MaxHealth > 0f)
        {
            maxRosterMaxHealth = Mathf.Max(maxRosterMaxHealth, state.MaxHealth);
        }

        maxMoveSpeed = Mathf.Max(maxMoveSpeed, state.MoveSpeed);
    }

    private static IReadOnlyList<BattleUnitCombatState> ToStates(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        if (runtimeUnits == null)
        {
            return null;
        }

        var states = new List<BattleUnitCombatState>(runtimeUnits.Count);
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            states.Add(runtimeUnits[i] != null ? runtimeUnits[i].State : null);
        }

        return states;
    }

    private static GladiatorPersonalityBias ResolvePersonalityBias(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null)
        {
            return GladiatorPersonalityBias.Neutral;
        }

        return unit.Snapshot.PersonalityBias;
    }
}
