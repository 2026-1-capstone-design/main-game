using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size         = GladiatorObservationSchema.TotalSize (= 149)
//   Continuous Actions = GladiatorActionSchema.ContinuousSize (= 2)
//     0=anchor strafe, 1=anchor forward
//   Discrete Branches  = 4
//     Branch 0 Size = GladiatorActionSchema.CommandBranchSize (= 2)
//     Branch 1 Size = GladiatorActionSchema.RoleBranchSize (= 3)
//     Branch 2 Size = GladiatorActionSchema.FightModeBranchSize (= 4)
//     Branch 3 Size = GladiatorActionSchema.AnchorActionBranchSize (= 12)
//
// Observation (148 floats):
//   자신      (43):     월드 좌표축 기준 정규화된 경기장 중심 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비,
//                       정규화된 사거리/이동속도/공격 쿨타임, 최근접 적/자신 대상 피해비, 최근접 적 거리,
//                       공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박, role/commitment/anchor relation 요약,
//                       timeout까지 남은 시간 비율, 현재/직전 agent 월드 이동 입력, anchor kind/slot/path/role one-hot
//   내 팀 동료 (5 × 8): 월드 좌표축 기준 정규화된 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비, 사거리, 이동속도, 공격 쿨타임
//   상대팀    (6 × 9): 위 동일 + 자신을 Neutral/Pressure 태세로 노리고 있는지 여부
//
// Action:
//   Continuous 0/1:     anchor strafe / anchor forward
//   Branch 0 (명령):     0=없음  1=기본공격
//   Branch 1 (역할):     0=engage  1=assassinate  2=regroup
//   Branch 2 (전투모드): 0=중립  1=압박  2=거리유지  3=후퇴
//   Branch 3 (anchor):   0=팀 중심  1~5=아군 슬롯 0~4  6~11=적 슬롯 0~5
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
    private GladiatorActionRole? _previousRole;
    private GladiatorAnchorKind? _previousAnchorKind;
    private int _previousTargetSlot = -1;
    private GladiatorFightMode? _previousFightMode;
    private int _commandCommitmentSteps;
    private int _anchorCommitmentSteps;
    private int _roleCommitmentSteps;
    private int _fightModeCommitmentSteps;
    private GladiatorRewardEvaluator _rewardEvaluator;
    private RuntimeUnitAgentActionSink _actionSink;
    private BattleAgentControlBuffer _agentControlBuffer;
    private LegacyBuiltInAiPlanProvider _aiHeuristic;
    private GladiatorAction _lastAction;
    private GladiatorTacticalContext _lastTacticalContext;
    private bool _hasLastRewardContext;
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
        _rewardEvaluator = new GladiatorRewardEvaluator(rewardConfig);
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
        _previousRole = null;
        _previousAnchorKind = null;
        _previousTargetSlot = -1;
        _previousFightMode = null;
        _commandCommitmentSteps = 0;
        _anchorCommitmentSteps = 0;
        _roleCommitmentSteps = 0;
        _fightModeCommitmentSteps = 0;
        _hasLastRewardContext = false;
        _episodeMetrics.Reset();

        if (useBuiltInAiHeuristic)
        {
            _aiHeuristic = new LegacyBuiltInAiPlanProvider();
        }
        else
        {
            _aiHeuristic = null;
        }
    }

    private void HandleDamageTaken(float damage)
    {
        float ratio =
            _selfState != null && _selfState.MaxHealth > 0f ? Mathf.Max(0f, damage) / _selfState.MaxHealth : 0f;
        AddReward(ratio * rewardConfig.damageTakenRatio);
        AddReward(ratio * EvaluateConditionalDamageTakenReward());
    }

    private void HandleSelfDied()
    {
        AddReward(rewardConfig.death);
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
    {
        AddReward(rewardConfig.attackLanded);
        float ratio =
            target != null && target.State != null && target.State.MaxHealth > 0f
                ? Mathf.Max(0f, actualDamage) / target.State.MaxHealth
                : 0f;
        _episodeMetrics.AddDamageDealt(actualDamage);
        AddReward(ratio * rewardConfig.damageDealtRatio);
        if (wasKill)
        {
            AddReward(rewardConfig.kill);
        }
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

        GladiatorAnchorCurriculum anchorCurriculum =
            _curriculumSource != null
                ? _curriculumSource.CurrentAnchorCurriculum
                : GladiatorAnchorCurriculum.EnemyAnchorSlotsOnly;
        GladiatorRoleCurriculum roleCurriculum =
            _curriculumSource != null ? _curriculumSource.CurrentRoleCurriculum : GladiatorRoleCurriculum.EngageOnly;
        ApplyAnchorActionMask(actionMask, branchSizes, anchorCurriculum);
        ApplyRoleMask(actionMask, branchSizes, roleCurriculum);
        ApplyFightModeMask(actionMask, branchSizes);
        ApplyCommandMask(actionMask, branchSizes);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfState == null || _selfState.IsCombatDisabled)
        {
            return;
        }

        GladiatorAction action = GladiatorAgentActionParser.Parse(actions);
        BattleUnitCombatState target = ResolveAnchorTarget(action);
        bool anchorFallbackApplied = TryApplyNearestEnemyAnchorFallback(ref action, ref target);
        action = NormalizeAllyAnchorAction(action);
        GladiatorObservationContext observationContext = CreateObservationContext();
        GladiatorTacticalContext tacticalContext = GladiatorTacticalContext.Builder.Build(
            _selfState,
            _rosterView != null ? _rosterView.Hostiles : null,
            action,
            target,
            CommitmentWindowSteps,
            _previousCommand,
            _previousRole,
            _previousAnchorKind,
            _previousTargetSlot,
            _previousFightMode,
            _commandCommitmentSteps,
            _anchorCommitmentSteps,
            _roleCommitmentSteps,
            _fightModeCommitmentSteps,
            _prevTargetDistance,
            anchorFallbackApplied
        );
        _episodeMetrics.RecordAction(
            action,
            tacticalContext,
            _selfState != null ? _selfState.Attack : 0f,
            _selfState != null ? _selfState.AttackSpeed : 0f,
            _selfState != null ? _selfState.AttackRange : 0f,
            GetStepDurationSeconds()
        );
        GladiatorCombatSignalFeatures features = GladiatorCombatSignalFeatures.Builder.Build(observationContext);
        GladiatorRewardEvaluation evaluation = _rewardEvaluator.EvaluateActionStep(action, tacticalContext, features);
        _episodeMetrics.RecordSmoothnessReward(evaluation.SmoothnessReward);
        AddReward(evaluation.Reward);
        RecordLastRewardContext(action, tacticalContext);

        UpdateCommitmentState(action, tacticalContext);
        _previousCommand = action.Command;
        _previousRole = action.Role;
        _previousAnchorKind = action.AnchorKind;
        _previousTargetSlot = action.AnchorSlot;
        _previousFightMode = action.FightMode;
        _prevTargetDistance = tacticalContext.TargetDistance;

        BattleUnitCombatState effectiveTarget = tacticalContext.HasValidTarget ? target : null;
        _actionSink?.Apply(evaluation.EffectiveAction, effectiveTarget);
    }

    public override void OnEpisodeBegin()
    {
        _prevTargetDistance = float.MaxValue;
        _previousCommand = null;
        _previousRole = null;
        _previousAnchorKind = null;
        _previousTargetSlot = -1;
        _previousFightMode = null;
        _commandCommitmentSteps = 0;
        _anchorCommitmentSteps = 0;
        _roleCommitmentSteps = 0;
        _fightModeCommitmentSteps = 0;
        _hasLastRewardContext = false;
        _rewardEvaluator?.Reset();
        _actionSink?.Clear();
        _episodeMetrics.Reset();
    }

    public void FlushEpisodeMetrics()
    {
        _episodeMetrics.Flush();
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
        else
            discrete[GladiatorActionSchema.CommandBranch] = (int)GladiatorCommand.Move;

        discrete[GladiatorActionSchema.RoleBranch] =
            kb.rKey.isPressed ? (int)GladiatorActionRole.Regroup
            : kb.cKey.isPressed ? (int)GladiatorActionRole.Assassinate
            : (int)GladiatorActionRole.Engage;

        if (kb.digit1Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                0
            );
        else if (kb.digit2Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                1
            );
        else if (kb.digit3Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                2
            );
        else if (kb.digit4Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                3
            );
        else if (kb.digit5Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                4
            );
        else if (kb.digit6Key.isPressed)
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                5
            );
        else
            discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
                GladiatorAnchorKind.Enemy,
                0
            );

        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            discrete[GladiatorActionSchema.FightModeBranch] = (int)GladiatorFightMode.Pressure;
        else if (kb.sKey.isPressed)
            discrete[GladiatorActionSchema.FightModeBranch] = (int)GladiatorFightMode.KeepRange;
        else if (kb.xKey.isPressed)
            discrete[GladiatorActionSchema.FightModeBranch] = (int)GladiatorFightMode.Retreat;
        else
            discrete[GladiatorActionSchema.FightModeBranch] = (int)GladiatorFightMode.Neutral;
    }

    private float GetStepDurationSeconds()
    {
        BattleSimulationManager simulationManager = _flowManager != null ? _flowManager.BattleSimulationManager : null;
        float tickRate = simulationManager != null ? simulationManager.simulationTickRate : 15f;
        return 1f / Mathf.Max(1f, tickRate);
    }

    private void RecordLastRewardContext(GladiatorAction action, GladiatorTacticalContext tacticalContext)
    {
        _lastAction = action;
        _lastTacticalContext = tacticalContext;
        _hasLastRewardContext = true;
    }

    private float EvaluateConditionalDamageTakenReward()
    {
        if (!_hasLastRewardContext || !_lastTacticalContext.HasValidTarget)
        {
            return 0f;
        }

        switch (_lastAction.FightMode)
        {
            case GladiatorFightMode.Pressure:
                return IsUnsafePressureDamageState() ? rewardConfig.pressureUnsafeDamageTakenRatio : 0f;
            case GladiatorFightMode.KeepRange:
                return IsTooCloseKeepRangeDamageState() ? rewardConfig.keepRangeTooCloseDamageTakenRatio : 0f;
            default:
                return 0f;
        }
    }

    private bool IsUnsafePressureDamageState()
    {
        return !_lastTacticalContext.IsTargetOutOfAttackRange
            && _lastTacticalContext.SelfThreatToTargetRatio < _lastTacticalContext.TargetThreatToSelfRatio;
    }

    private bool IsTooCloseKeepRangeDamageState()
    {
        float effectiveRange = Mathf.Max(rewardConfig.minimumEffectiveRange, _lastTacticalContext.TargetEffectiveRange);
        float distanceRatio = _lastTacticalContext.TargetDistance / effectiveRange;
        return distanceRatio < rewardConfig.keepRangeBandMin;
    }

    private BattleUnitCombatState ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(slotIndex) : null;

    private BattleUnitCombatState ResolveTeammateSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveTeammateSlot(slotIndex) : null;

    private BattleUnitCombatState ResolveAnchorTarget(GladiatorAction action) =>
        action.AnchorKind switch
        {
            GladiatorAnchorKind.Ally => ResolveTeammateSlot(action.AnchorSlot),
            GladiatorAnchorKind.TeamCenter => null,
            _ => ResolveOpponentSlot(action.AnchorSlot),
        };

    private static GladiatorAction NormalizeAllyAnchorAction(GladiatorAction action)
    {
        if (action.AnchorKind != GladiatorAnchorKind.Ally)
        {
            return action;
        }

        // Ally anchors are formation references, so combat-only branches are canonicalized before reward/context use.
        return new GladiatorAction(
            action.RelativeMove,
            GladiatorActionRole.Regroup,
            GladiatorFightMode.Neutral,
            action.AnchorKind,
            action.AnchorSlot,
            GladiatorCommand.Move
        );
    }

    private bool TryApplyNearestEnemyAnchorFallback(ref GladiatorAction action, ref BattleUnitCombatState target)
    {
        if (action.AnchorKind == GladiatorAnchorKind.TeamCenter || IsValidAnchorTarget(target))
        {
            return false;
        }

        if (!TryResolveNearestOpponentAnchor(out BattleUnitCombatState fallbackTarget, out int fallbackSlot))
        {
            return false;
        }

        target = fallbackTarget;
        action = action.WithAnchor(GladiatorAnchorKind.Enemy, fallbackSlot);
        return true;
    }

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
            controlInput.AnchorKind,
            controlInput.AnchorSlot,
            controlInput.FightMode,
            controlInput.Role,
            _anchorCommitmentSteps,
            _roleCommitmentSteps,
            currentAnchor
        );
    }

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
        if (IsValidAnchorTarget(controlInput.AnchorTarget))
        {
            return controlInput.AnchorTarget;
        }

        if (
            !_previousAnchorKind.HasValue
            && TryResolveNearestOpponentAnchor(out BattleUnitCombatState initialAnchor, out _)
        )
        {
            return initialAnchor;
        }

        BattleUnitCombatState selectedAnchor = controlInput.AnchorKind switch
        {
            GladiatorAnchorKind.Ally => ResolveTeammateSlot(controlInput.AnchorSlot),
            GladiatorAnchorKind.TeamCenter => null,
            _ => ResolveOpponentSlot(controlInput.AnchorSlot),
        };
        if (IsValidAnchorTarget(selectedAnchor))
        {
            return selectedAnchor;
        }

        if (
            controlInput.AnchorKind != GladiatorAnchorKind.TeamCenter
            && TryResolveNearestOpponentAnchor(out BattleUnitCombatState fallbackAnchor, out _)
        )
        {
            return fallbackAnchor;
        }

        return null;
    }

    private void ApplyAnchorActionMask(
        IDiscreteActionMask actionMask,
        int[] branchSizes,
        GladiatorAnchorCurriculum anchorCurriculum
    )
    {
        if (branchSizes.Length <= GladiatorActionSchema.AnchorBranch)
        {
            return;
        }

        int branchSize = branchSizes[GladiatorActionSchema.AnchorBranch];
        bool hasEnabledAnchorAction = false;
        for (int i = 0; i < branchSize; i++)
        {
            if (IsValidAnchorActionForCurrentArena(i, anchorCurriculum))
            {
                hasEnabledAnchorAction = true;
                break;
            }
        }

        for (int i = 0; i < branchSize; i++)
        {
            bool invalid = !IsValidAnchorActionForCurrentArena(i, anchorCurriculum);
            bool fallbackAction = i == GladiatorActionSchema.TeamCenterAnchorAction;
            if (invalid && (hasEnabledAnchorAction || !fallbackAction))
            {
                actionMask.SetActionEnabled(GladiatorActionSchema.AnchorBranch, i, false);
            }
        }
    }

    private void ApplyFightModeMask(IDiscreteActionMask actionMask, int[] branchSizes)
    {
        if (branchSizes.Length <= GladiatorActionSchema.FightModeBranch)
        {
            return;
        }

        if (IsKeepRangeUnsupported())
        {
            actionMask.SetActionEnabled(
                GladiatorActionSchema.FightModeBranch,
                (int)GladiatorFightMode.KeepRange,
                false
            );
        }
    }

    private void ApplyCommandMask(IDiscreteActionMask actionMask, int[] branchSizes)
    {
        if (branchSizes.Length <= GladiatorActionSchema.CommandBranch)
        {
            return;
        }

        bool canAttack = HasLivingOpponent();
        if (!canAttack)
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.CommandBranch, (int)GladiatorCommand.Attack, false);
        }
    }

    private static void ApplyRoleMask(
        IDiscreteActionMask actionMask,
        int[] branchSizes,
        GladiatorRoleCurriculum roleCurriculum
    )
    {
        if (branchSizes.Length <= GladiatorActionSchema.RoleBranch)
        {
            return;
        }

        if (roleCurriculum <= GladiatorRoleCurriculum.EngageOnly)
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.RoleBranch, (int)GladiatorActionRole.Assassinate, false);
            actionMask.SetActionEnabled(GladiatorActionSchema.RoleBranch, (int)GladiatorActionRole.Regroup, false);
            return;
        }

        if (roleCurriculum == GladiatorRoleCurriculum.AssassinateUnlocked)
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.RoleBranch, (int)GladiatorActionRole.Regroup, false);
        }
    }

    private bool IsValidAnchorActionForCurrentArena(int anchorAction, GladiatorAnchorCurriculum anchorCurriculum)
    {
        if (
            !GladiatorActionSchema.TryDecodeAnchorAction(anchorAction, out GladiatorAnchorKind anchorKind, out int slot)
        )
        {
            return false;
        }

        if (anchorCurriculum < GladiatorAnchorCurriculum.AllSlotsUnlocked && anchorKind != GladiatorAnchorKind.Enemy)
        {
            return false;
        }

        switch (anchorKind)
        {
            case GladiatorAnchorKind.TeamCenter:
                return true;
            case GladiatorAnchorKind.Ally:
                return IsValidTeammateObservationSlot(slot);
            default:
                return IsValidEnemySlot(slot);
        }
    }

    private bool IsKeepRangeUnsupported()
    {
        return _selfUnit == null || _selfUnit.Snapshot == null || !_selfUnit.Snapshot.IsRanged;
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

    private bool TryResolveNearestOpponentAnchor(out BattleUnitCombatState target, out int slot)
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

    private bool IsValidTeammateObservationSlot(int slot)
    {
        if (slot < 0 || slot >= GladiatorObservationSchema.TeammateSlots)
        {
            return false;
        }

        BattleUnitCombatState teammate = ResolveTeammateSlot(slot);
        return teammate != null && !teammate.IsCombatDisabled;
    }

    private void UpdateCommitmentState(GladiatorAction action, GladiatorTacticalContext context)
    {
        _commandCommitmentSteps = context.CommandCommitmentSteps;
        _anchorCommitmentSteps = context.AnchorCommitmentSteps;
        _roleCommitmentSteps = context.RoleCommitmentSteps;
        _fightModeCommitmentSteps = context.FightModeCommitmentSteps;
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
        var maxHealthValues = new List<float>();
        var attackValues = new List<float>();
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

                AddInitialUnitStats(state, maxHealthValues, attackValues, ref maxMoveSpeed);
            }
        }

        if (!sawSelf)
        {
            AddInitialUnitStats(_selfState, maxHealthValues, attackValues, ref maxMoveSpeed);
        }

        float fallbackMaxHealth = _selfState != null ? _selfState.MaxHealth : 1f;
        float fallbackAttack = _selfState != null ? _selfState.Attack : 1f;
        float fallbackMoveSpeed = _selfState != null ? _selfState.MoveSpeed : 1f;

        return new GladiatorObservationStats(
            Median(maxHealthValues, fallbackMaxHealth),
            Median(attackValues, fallbackAttack),
            maxMoveSpeed > 0f ? maxMoveSpeed : fallbackMoveSpeed
        );
    }

    private static void AddInitialUnitStats(
        BattleUnitCombatState state,
        List<float> maxHealthValues,
        List<float> attackValues,
        ref float maxMoveSpeed
    )
    {
        if (state == null)
        {
            return;
        }

        if (state.MaxHealth > 0f)
        {
            maxHealthValues.Add(state.MaxHealth);
        }

        if (state.Attack > 0f)
        {
            attackValues.Add(state.Attack);
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

    private static float Median(List<float> values, float fallback)
    {
        if (values.Count == 0)
        {
            return Mathf.Max(1e-6f, fallback);
        }

        values.Sort();

        int mid = values.Count / 2;
        if (values.Count % 2 == 1)
        {
            return values[mid];
        }

        return (values[mid - 1] + values[mid]) * 0.5f;
    }
}
