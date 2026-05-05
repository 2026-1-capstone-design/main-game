using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size         = GladiatorObservationSchema.TotalSize (= 125)
//   Continuous Actions = GladiatorActionSchema.ContinuousSize (= 2)
//     0=anchor strafe, 1=anchor forward
//   Discrete Branches  = 5
//     Branch 0 Size = GladiatorActionSchema.CommandBranchSize (= 2)
//     Branch 1 Size = GladiatorActionSchema.StanceBranchSize (= 3)
//     Branch 2 Size = GladiatorActionSchema.PathModeBranchSize (= 4)
//     Branch 3 Size = GladiatorActionSchema.AnchorKindBranchSize (= 3)
//     Branch 4 Size = GladiatorActionSchema.AnchorSlotBranchSize (= 6)
//
// Observation (125 floats):
//   자신      (31):     월드 좌표축 기준 정규화된 경기장 중심 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비,
//                       정규화된 사거리/이동속도/공격 쿨타임, 최근접 적/자신 대상 피해비, 최근접 적 거리,
//                       공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박,
//                       timeout까지 남은 시간 비율, 현재/직전 agent 월드 이동 입력, 대상 선택 여부, 대상 슬롯 one-hot
//   내 팀 동료 (5 × 8): 월드 좌표축 기준 정규화된 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비, 사거리, 이동속도, 공격 쿨타임
//   상대팀    (6 × 9): 위 동일 + 자신을 Neutral/Pressure 태세로 노리고 있는지 여부
//
// Action:
//   Continuous 0/1:     anchor strafe / anchor forward
//   Branch 0 (명령):     0=없음  1=기본공격
//   Branch 1 (태세):     0=중립  1=압박  2=거리유지
//   Branch 2 (경로):     0=직접  1=좌측 우회  2=우측 우회  3=합류
//   Branch 3 (anchor):   0=적  1=아군  2=팀 중심
//   Branch 4 (slot):     0~5=anchor 슬롯
public class GladiatorAgent : Agent
{
    private const float HardBoundaryRadiusMultiplier = 1.25f;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    [Header("Heuristic (ONLY Demo Recording)")]
    [SerializeField]
    private bool useBuiltInAiHeuristic = false;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private TrainingBootstrapper _trainingBootstrapper;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;
    private BattleUnitCombatState _selfState;
    private GladiatorStateRosterView _rosterView;
    private IBattleRuntimeUnitResolver _runtimeResolver;
    private GladiatorObservationStats _observationStats;
    private float _prevTargetDistance;
    private int _previousTargetSlot = -1;
    private int _previousStance = -1;
    private bool _boundaryResetRequested;
    private GladiatorRewardEvaluator _rewardEvaluator;
    private IGladiatorAgentActionSink _actionSink;
    private BattleAgentControlBuffer _agentControlBuffer;
    private BuiltInAiControlSource _aiHeuristic;
    private readonly GladiatorAgentEpisodeMetrics _episodeMetrics = new GladiatorAgentEpisodeMetrics();

    public bool HasControlledUnit => _selfUnit != null;

    public void Initialize(
        BattleRuntimeUnit unit,
        BattleSceneFlowManager flowManager,
        TrainingBootstrapper trainingBootstrapper
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
        _trainingBootstrapper = trainingBootstrapper;
        _boundaryResetRequested = false;

        SphereCollider col = flowManager?.battlefieldCollider;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        _arenaExtentsMin = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : float.MaxValue;
        _runtimeResolver = new BattleRuntimeUnitResolver(_flowManager != null ? _flowManager.RuntimeUnits : null);
        _rosterView = CreateRosterView();
        _rewardEvaluator = new GladiatorRewardEvaluator(rewardConfig, HardBoundaryRadiusMultiplier);
        _rewardEvaluator.Reset();
        _agentControlBuffer =
            _flowManager != null && _flowManager.BattleSimulationManager != null
                ? _flowManager.BattleSimulationManager.AgentControlBuffer
                : null;
        _actionSink = new RuntimeUnitAgentActionSink(_selfUnit, _runtimeResolver, _agentControlBuffer);
        _observationStats = ComputeInitialObservationStats();

        if (_selfUnit != null)
        {
            _selfUnit.SetControlMode(BattleUnitControlMode.AgentPolicy);
            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
        }

        _prevTargetDistance = float.MaxValue;
        _previousTargetSlot = -1;
        _previousStance = -1;
        _episodeMetrics.Reset();

        if (useBuiltInAiHeuristic)
        {
            _aiHeuristic = new BuiltInAiControlSource();
            IReadOnlyList<BattleRuntimeUnit> units = _flowManager != null ? _flowManager.RuntimeUnits : null;
            BattleAITuningSO tuning = _flowManager?.BattleSimulationManager?.aiTuning;
            _aiHeuristic.Configure(units, tuning);
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
        _episodeMetrics.AddDamageTakenRatio(ratio);
        AddReward(ratio * rewardConfig.damageTakenRatio);
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
        _episodeMetrics.AddDamageDealtRatio(ratio);
        _episodeMetrics.RecordAttackLanded();
        AddReward(ratio * rewardConfig.damageDealtRatio);
        if (wasKill)
        {
            AddReward(rewardConfig.kill);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        BattleObservationBuilder.Write(sensor, CreateObservationContext());
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfState == null || _selfState.IsCombatDisabled || _rosterView == null)
        {
            return;
        }

        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        int[] branchSizes = behaviorParameters != null ? behaviorParameters.BrainParameters.ActionSpec.BranchSizes : null;
        if (branchSizes == null)
        {
            return;
        }

        int tacticMode = _trainingBootstrapper != null ? _trainingBootstrapper.CurrentTacticMode : 0;
        ApplyAnchorSlotMask(actionMask, branchSizes, tacticMode);
        ApplyPathModeMask(actionMask, branchSizes, tacticMode);
        ApplyAnchorKindMask(actionMask, branchSizes, tacticMode);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfState == null || _selfState.IsCombatDisabled)
        {
            return;
        }

        GladiatorPolicyAction action = GladiatorAgentActionParser.Parse(actions);
        BattleUnitCombatState target = ResolveAnchorTarget(action);
        GladiatorAgentTacticalContext tacticalContext = CreateTacticalContext(action, target);
        _episodeMetrics.RecordAction(action, tacticalContext);
        GladiatorTacticalFeatures features = BattleObservationBuilder.BuildTacticalFeatures(CreateObservationContext());
        GladiatorRewardEvaluation evaluation = _rewardEvaluator.EvaluateActionStep(action, tacticalContext, features);
        AddReward(evaluation.Reward);
        if (evaluation.RequestsBoundaryReset)
        {
            RequestBoundaryReset();
            _actionSink?.Clear();
            return;
        }

        _previousTargetSlot = action.AnchorSlot;
        _previousStance = action.Stance;
        _prevTargetDistance = tacticalContext.TargetDistance;

        BattleUnitCombatState effectiveTarget = tacticalContext.HasValidTarget ? target : null;
        _actionSink?.Apply(evaluation.EffectiveAction, effectiveTarget);
    }

    private GladiatorAgentTacticalContext CreateTacticalContext(
        GladiatorPolicyAction action,
        BattleUnitCombatState target
    )
    {
        float playableRadius = _arenaExtentsMin - _selfState.BodyRadius;
        float distanceFromCenter = GetDistanceFromArenaCenter();
        bool attackBlocked = _selfState.AttackCooldownRemaining > 0f || _selfState.IsAttacking;
        bool hasValidTarget = target != null && !target.IsCombatDisabled;
        float targetDistance = GetDistanceToTarget(target);
        float previousTargetDistance =
            _previousTargetSlot == action.AnchorSlot && _prevTargetDistance < float.MaxValue
                ? _prevTargetDistance
                : targetDistance;
        float targetEffectiveRange = GetEffectiveAttackRange(_selfState, target);
        return new GladiatorAgentTacticalContext(
            _previousTargetSlot,
            action.AnchorSlot,
            _previousStance,
            action.Stance,
            previousTargetDistance,
            targetDistance,
            targetEffectiveRange,
            distanceFromCenter,
            playableRadius,
            HasLivingOpponent(),
            HasAttackableOpponent(),
            hasValidTarget,
            !hasValidTarget || targetDistance > targetEffectiveRange,
            attackBlocked
        );
    }

    public override void OnEpisodeBegin()
    {
        _boundaryResetRequested = false;
        _prevTargetDistance = float.MaxValue;
        _previousTargetSlot = -1;
        _previousStance = -1;
        _rewardEvaluator?.Reset();
        _actionSink?.Clear();
        _episodeMetrics.Reset();
    }

    public void FlushEpisodeMetrics()
    {
        _episodeMetrics.RecordFinalHealthRatios(GetHealthRatio(_selfState), GetAverageOpponentHealthRatio());
        _episodeMetrics.Flush();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (useBuiltInAiHeuristic && _aiHeuristic != null && _selfState != null)
        {
            BattleFieldSnapshot snapshot = _flowManager?.BattleSimulationManager?.CurrentSnapshot;
            float tickDelta =
                _flowManager?.BattleSimulationManager != null
                    ? 1f / _flowManager.BattleSimulationManager.simulationTickRate
                    : 1f / 15f;
            if (_aiHeuristic.TryBuildPlan(_selfState, snapshot, tickDelta, out BattleControlPlan plan))
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
                (kb.dKey.isPressed ? 1f : 0f) + (kb.aKey.isPressed ? -1f : 0f);
            continuous[GladiatorActionSchema.ContinuousAnchorForward] =
                (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
        }

        if (discrete.Length < GladiatorActionSchema.DiscreteBranchCount)
        {
            return;
        }

        if (kb.jKey.isPressed)
            discrete[GladiatorActionSchema.CommandBranch] = GladiatorActionSchema.CommandBasicAttack;
        else
            discrete[GladiatorActionSchema.CommandBranch] = GladiatorActionSchema.CommandNone;

        if (kb.digit1Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 0;
        else if (kb.digit2Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 1;
        else if (kb.digit3Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 2;
        else if (kb.digit4Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 3;
        else if (kb.digit5Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 4;
        else if (kb.digit6Key.isPressed)
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 5;
        else
            discrete[GladiatorActionSchema.AnchorSlotBranch] = 0;

        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StancePressure;
        else if (kb.sKey.isPressed)
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StanceKeepRange;
        else
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StanceNeutral;

        discrete[GladiatorActionSchema.PathModeBranch] = kb.qKey.isPressed
            ? GladiatorActionSchema.PathModeFlankLeft
            : kb.eKey.isPressed
                ? GladiatorActionSchema.PathModeFlankRight
                : kb.rKey.isPressed
                    ? GladiatorActionSchema.PathModeRegroup
                    : GladiatorActionSchema.PathModeDirect;
        discrete[GladiatorActionSchema.AnchorKindBranch] = GladiatorActionSchema.AnchorKindEnemy;
    }

    private float GetAverageOpponentHealthRatio()
    {
        if (_rosterView == null)
        {
            return 0f;
        }

        IReadOnlyList<BattleUnitCombatState> hostiles = _rosterView.Hostiles;
        if (hostiles == null || hostiles.Count == 0)
        {
            return 0f;
        }

        float sum = 0f;
        int count = 0;
        for (int i = 0; i < hostiles.Count; i++)
        {
            BattleUnitCombatState hostile = hostiles[i];
            if (hostile == null)
            {
                continue;
            }

            sum += GetHealthRatio(hostile);
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    private static float GetHealthRatio(BattleUnitCombatState state)
    {
        return state != null && state.MaxHealth > 0f
            ? Mathf.Clamp01(Mathf.Max(0f, state.CurrentHealth) / state.MaxHealth)
            : 0f;
    }

    private float GetDistanceToTarget(BattleUnitCombatState target)
    {
        if (target == null || _selfState == null)
        {
            return float.MaxValue;
        }

        Vector3 delta = target.Position - _selfState.Position;
        delta.y = 0f;
        return delta.magnitude;
    }

    private static float GetEffectiveAttackRange(BattleUnitCombatState attacker, BattleUnitCombatState target)
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attacker.AttackRange) + 0.05f;
    }

    private BattleUnitCombatState ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(slotIndex) : null;

    private BattleUnitCombatState ResolveTeammateSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveTeammateSlot(slotIndex) : null;

    private BattleUnitCombatState ResolveAnchorTarget(GladiatorPolicyAction action) =>
        action.AnchorKind switch
        {
            GladiatorActionSchema.AnchorKindAlly => ResolveTeammateSlot(action.AnchorSlot),
            GladiatorActionSchema.AnchorKindTeamCenter => null,
            _ => ResolveOpponentSlot(action.AnchorSlot),
        };

    private GladiatorObservationContext CreateObservationContext()
    {
        float arenaRadius = _selfUnit != null ? _arenaExtentsMin - _selfUnit.BodyRadius : float.MaxValue;
        BattleAgentControlInput controlInput =
            _agentControlBuffer != null ? _agentControlBuffer.GetInputSnapshot(_selfState) : default;
        BattleUnitCombatState currentAnchor = controlInput.AnchorTarget;
        if (currentAnchor == null)
        {
            currentAnchor =
                controlInput.AnchorKind == GladiatorActionSchema.AnchorKindAlly
                    ? ResolveTeammateSlot(controlInput.AnchorSlot)
                    : ResolveOpponentSlot(controlInput.AnchorSlot);
        }

        return new GladiatorObservationContext(
            _selfState,
            _rosterView != null ? _rosterView.Teammates : null,
            _rosterView != null ? _rosterView.Hostiles : null,
            _observationStats,
            _arenaCenter,
            arenaRadius,
            _trainingBootstrapper != null ? _trainingBootstrapper.BattleTimeoutRemainingRatio : 1f,
            controlInput.SmoothedLocalMove,
            controlInput.PreviousRawLocalMove,
            controlInput.AnchorKind,
            controlInput.PathMode,
            currentAnchor
        );
    }

    private void ApplyAnchorSlotMask(IDiscreteActionMask actionMask, int[] branchSizes, int tacticMode)
    {
        if (branchSizes.Length <= GladiatorActionSchema.AnchorSlotBranch)
        {
            return;
        }

        if (tacticMode >= 2)
        {
            return;
        }

        int slotBranchSize = branchSizes[GladiatorActionSchema.AnchorSlotBranch];
        int enemySlotCount = Mathf.Min(GladiatorObservationSchema.OpponentSlots, slotBranchSize);
        for (int i = 0; i < enemySlotCount; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled;
            if (invalid)
            {
                actionMask.SetActionEnabled(GladiatorActionSchema.AnchorSlotBranch, i, false);
            }
        }
    }

    private static void ApplyPathModeMask(IDiscreteActionMask actionMask, int[] branchSizes, int tacticMode)
    {
        if (branchSizes.Length <= GladiatorActionSchema.PathModeBranch)
        {
            return;
        }

        int branchSize = branchSizes[GladiatorActionSchema.PathModeBranch];
        if (tacticMode <= 0)
        {
            for (int i = 0; i < branchSize; i++)
            {
                if (i != GladiatorActionSchema.PathModeDirect)
                {
                    actionMask.SetActionEnabled(GladiatorActionSchema.PathModeBranch, i, false);
                }
            }

            return;
        }

        if (tacticMode == 1 && branchSize > GladiatorActionSchema.PathModeRegroup)
        {
            actionMask.SetActionEnabled(
                GladiatorActionSchema.PathModeBranch,
                GladiatorActionSchema.PathModeRegroup,
                false
            );
        }
    }

    private static void ApplyAnchorKindMask(IDiscreteActionMask actionMask, int[] branchSizes, int tacticMode)
    {
        if (branchSizes.Length <= GladiatorActionSchema.AnchorKindBranch)
        {
            return;
        }

        int branchSize = branchSizes[GladiatorActionSchema.AnchorKindBranch];
        if (tacticMode < 2)
        {
            for (int i = 0; i < branchSize; i++)
            {
                if (i != GladiatorActionSchema.AnchorKindEnemy)
                {
                    actionMask.SetActionEnabled(GladiatorActionSchema.AnchorKindBranch, i, false);
                }
            }
        }
    }

    private bool HasAttackableOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            if (
                target != null
                && !target.IsCombatDisabled
                && GetDistanceToTarget(target) <= GetEffectiveAttackRange(_selfState, target)
            )
            {
                return true;
            }
        }

        return false;
    }

    private bool HasLivingOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            if (target != null && !target.IsCombatDisabled)
            {
                return true;
            }
        }

        return false;
    }

    private float GetDistanceFromArenaCenter()
    {
        Vector3 flatPos = new Vector3(_selfState.Position.x, 0f, _selfState.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        return Vector3.Distance(flatPos, flatCenter);
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

    private void RequestBoundaryReset()
    {
        if (_boundaryResetRequested)
        {
            return;
        }

        _boundaryResetRequested = true;
        _trainingBootstrapper?.RequestEpisodeReset();
    }

    private GladiatorStateRosterView CreateRosterView()
    {
        BattleStartPayload payload = _flowManager != null ? _flowManager.CurrentPayload : null;
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        return new GladiatorStateRosterView(_selfState, payload, ToStates(runtimeUnits));
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
