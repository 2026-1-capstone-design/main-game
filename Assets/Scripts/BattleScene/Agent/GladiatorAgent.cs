using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size         = GladiatorObservationSchema.TotalSize (= 102)
//   Continuous Actions = GladiatorActionSchema.ContinuousSize (= 3)
//     0=local strafe, 1=local advance, 2=turn
//   Discrete Branches  = 3
//     Branch 0 Size = GladiatorActionSchema.CommandBranchSize (= 3)
//     Branch 1 Size = GladiatorActionSchema.TargetBranchSize (= 6)
//     Branch 2 Size = GladiatorActionSchema.StanceBranchSize (= 3)
//
// Observation (102 floats):
//   자신      (25):     정규화된 경기장 중심 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비,
//                       정규화된 사거리/이동속도/공격 쿨타임, 최근접 적/자신 대상 피해비, 최근접 적 거리,
//                       공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박,
//                       timeout까지 남은 시간 비율, 현재/직전 외부 입력, 스킬 가능 여부, 대상 선택 여부
//   내 팀 동료 (5 × 7): 정규화된 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비, 사거리, 이동속도
//   상대팀    (6 × 7): 정규화된 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비, 사거리, 이동속도
//
// Action:
//   Continuous 0/1/2:   local strafe / local advance / turn
//   Branch 0 (명령):     0=없음  1=기본공격  2=스킬
//   Branch 1 (대상):     0~5=상대팀 고정 슬롯
//   Branch 2 (태세):     0=중립  1=압박  2=거리유지
public class GladiatorAgent : Agent
{
    private const float HardBoundaryRadiusMultiplier = 1.25f;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private TrainingBootstrapper _trainingBootstrapper;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;
    private BattleUnitCombatState _selfState;
    private GladiatorStateRosterView _rosterView;
    private IBattleRuntimeUnitResolver _runtimeResolver;
    private IBattleUnitPoseProvider _poseProvider;
    private GladiatorObservationStats _observationStats;
    private float _prevDistToNearestEnemy;
    private bool _boundaryResetRequested;
    private GladiatorRewardEvaluator _rewardEvaluator;
    private IGladiatorAgentActionSink _actionSink;

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
        _poseProvider = new RuntimeBattleUnitPoseProvider(_selfUnit);
        _rosterView = CreateRosterView();
        _rewardEvaluator = new GladiatorRewardEvaluator(rewardConfig, HardBoundaryRadiusMultiplier);
        _rewardEvaluator.Reset();
        _actionSink = new RuntimeUnitAgentActionSink(_selfUnit, _runtimeResolver);
        _observationStats = ComputeInitialObservationStats();

        if (_selfUnit != null)
        {
            _selfUnit.SetControlMode(BattleUnitControlMode.ExternalAgent);
            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
            _selfUnit.OnSkillActivated += HandleSkillActivated;
            _selfUnit.OnSkillFailed += HandleSkillFailed;
        }

        _prevDistToNearestEnemy = GetDistToNearestOpponent();
    }

    private void HandleDamageTaken(float damage)
    {
        AddReward(rewardConfig.damageTaken);
        AddReward(Mathf.Max(0f, damage) * rewardConfig.damageTakenPerPoint);
    }

    private void HandleSelfDied()
    {
        AddReward(rewardConfig.death);
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
    {
        AddReward(rewardConfig.attackLanded);
        AddReward(actualDamage * rewardConfig.damageDealt);
        if (wasKill)
        {
            AddReward(rewardConfig.kill);
        }
    }

    private void HandleSkillActivated()
    {
        AddReward(rewardConfig.skillActivated);
    }

    private void HandleSkillFailed()
    {
        AddReward(rewardConfig.invalidSkill);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float arenaRadius = _selfUnit != null ? _arenaExtentsMin - _selfUnit.BodyRadius : float.MaxValue;
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(
                _selfState,
                _rosterView != null ? _rosterView.Teammates : null,
                _rosterView != null ? _rosterView.Hostiles : null,
                _observationStats,
                _poseProvider != null ? _poseProvider.CurrentPose : BattleUnitPose.Default,
                _arenaCenter,
                arenaRadius,
                _trainingBootstrapper != null ? _trainingBootstrapper.BattleTimeoutRemainingRatio : 1f,
                _selfUnit != null ? _selfUnit.ExternalSmoothedLocalMove : Vector2.zero,
                _selfUnit != null ? _selfUnit.ExternalSmoothedTurn : 0f,
                _selfUnit != null ? _selfUnit.ExternalPreviousRawLocalMove : Vector2.zero,
                _selfUnit != null ? _selfUnit.ExternalPreviousRawTurn : 0f
            )
        );
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfState == null || _selfState.IsCombatDisabled || _rosterView == null)
        {
            return;
        }

        bool hasValidTarget = HasLivingOpponent();
        if (!hasValidTarget)
            return;

        if (!CanRequestSkill())
        {
            actionMask.SetActionEnabled(GladiatorActionSchema.CommandBranch, GladiatorActionSchema.CommandSkill, false);
        }

        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled;
            if (invalid)
            {
                actionMask.SetActionEnabled(GladiatorActionSchema.TargetBranch, i, false);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfState == null || _selfState.IsCombatDisabled)
        {
            return;
        }

        GladiatorAgentAction action = GladiatorAgentActionParser.Parse(actions);
        BattleUnitCombatState target = ResolveOpponentSlot(action.TargetSlot);
        GladiatorAgentTacticalContext tacticalContext = CreateTacticalContext(target);
        GladiatorRewardEvaluation evaluation = _rewardEvaluator.EvaluateActionStep(action, tacticalContext);
        AddReward(evaluation.Reward);
        if (evaluation.RequestsBoundaryReset)
        {
            RequestBoundaryReset();
            _actionSink?.Clear();
            return;
        }

        if (evaluation.UpdatesNearestOpponentDistance)
        {
            _prevDistToNearestEnemy = evaluation.NearestOpponentDistance;
        }

        BattleUnitCombatState effectiveTarget = tacticalContext.HasValidTarget ? target : null;
        _actionSink?.Apply(evaluation.EffectiveAction, effectiveTarget);
    }

    private GladiatorAgentTacticalContext CreateTacticalContext(BattleUnitCombatState target)
    {
        float playableRadius = _arenaExtentsMin - _selfState.BodyRadius;
        float distanceFromCenter = GetDistanceFromArenaCenter();
        float nearestDist = GetDistToNearestOpponent();
        bool shouldRetreat = ShouldRetreat(nearestDist);
        bool attackBlocked = _selfState.AttackCooldownRemaining > 0f || _selfState.IsAttacking;
        bool hasValidTarget = target != null && !target.IsCombatDisabled;
        return new GladiatorAgentTacticalContext(
            _prevDistToNearestEnemy,
            nearestDist,
            distanceFromCenter,
            playableRadius,
            shouldRetreat,
            HasLivingOpponent(),
            HasAttackableOpponent(),
            hasValidTarget,
            IsOutOfAttackRange(target),
            attackBlocked,
            CanRequestSkill()
        );
    }

    public override void OnEpisodeBegin()
    {
        _boundaryResetRequested = false;
        _prevDistToNearestEnemy = GetDistToNearestOpponent();
        _rewardEvaluator?.Reset();
        _actionSink?.Clear();
    }

    public void GiveEndReward(BattleTeamId? winnerTeamId, bool isTimeout)
    {
        if (isTimeout)
        {
            AddReward(rewardConfig.timeout);
            return;
        }

        if (_selfUnit == null || !winnerTeamId.HasValue)
        {
            AddReward(rewardConfig.loss);
            return;
        }

        AddReward(winnerTeamId.Value == _selfUnit.TeamId ? rewardConfig.win : rewardConfig.loss);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var kb = Keyboard.current;
        var continuous = actionsOut.ContinuousActions;
        var discrete = actionsOut.DiscreteActions;
        if (kb == null)
        {
            return;
        }

        if (continuous.Length >= GladiatorActionSchema.ContinuousSize)
        {
            continuous[GladiatorActionSchema.ContinuousMoveX] =
                (kb.dKey.isPressed ? 1f : 0f) + (kb.aKey.isPressed ? -1f : 0f);
            continuous[GladiatorActionSchema.ContinuousMoveZ] =
                (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
            continuous[GladiatorActionSchema.ContinuousTurn] =
                (kb.eKey.isPressed ? 1f : 0f) + (kb.qKey.isPressed ? -1f : 0f);
        }

        if (discrete.Length < GladiatorActionSchema.DiscreteBranchCount)
        {
            return;
        }

        if (kb.kKey.isPressed)
            discrete[GladiatorActionSchema.CommandBranch] = GladiatorActionSchema.CommandSkill;
        else if (kb.jKey.isPressed)
            discrete[GladiatorActionSchema.CommandBranch] = GladiatorActionSchema.CommandBasicAttack;
        else
            discrete[GladiatorActionSchema.CommandBranch] = GladiatorActionSchema.CommandNone;

        if (kb.digit1Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 0;
        else if (kb.digit2Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 1;
        else if (kb.digit3Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 2;
        else if (kb.digit4Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 3;
        else if (kb.digit5Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 4;
        else if (kb.digit6Key.isPressed)
            discrete[GladiatorActionSchema.TargetBranch] = 5;
        else
            discrete[GladiatorActionSchema.TargetBranch] = 0;

        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StancePressure;
        else if (kb.sKey.isPressed)
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StanceKeepRange;
        else
            discrete[GladiatorActionSchema.StanceBranch] = GladiatorActionSchema.StanceNeutral;
    }

    private bool IsOutOfAttackRange(BattleUnitCombatState target)
    {
        if (target == null || _selfState == null)
        {
            return true;
        }

        Vector3 delta = target.Position - _selfState.Position;
        delta.y = 0f;
        float effectiveRange = _selfState.BodyRadius + target.BodyRadius + _selfState.AttackRange + 0.05f;
        return delta.magnitude > effectiveRange;
    }

    private float GetDistToNearestOpponent() =>
        _rosterView != null ? _rosterView.GetDistanceToNearestHostile(_selfState) : float.MaxValue;

    private BattleUnitCombatState ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(slotIndex) : null;

    private bool HasAttackableOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            if (target != null && !target.IsCombatDisabled && !IsOutOfAttackRange(target))
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

    private bool CanRequestSkill()
    {
        return _selfState != null && _selfState.GetSkill() != WeaponSkillId.None && _selfState.SkillCooldownRemaining <= 0f;
    }

    private float GetDistanceFromArenaCenter()
    {
        Vector3 flatPos = new Vector3(_selfState.Position.x, 0f, _selfState.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        return Vector3.Distance(flatPos, flatCenter);
    }

    private BattleUnitCombatState ResolveNearestOpponent()
    {
        BattleUnitCombatState nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            if (target == null || target.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = target.Position - _selfState.Position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = target;
            }
        }

        return nearest;
    }

    private bool ShouldRetreat(float nearestDist)
    {
        if (_selfState == null || nearestDist >= float.MaxValue)
            return false;

        BattleUnitCombatState nearestOpponent = ResolveNearestOpponent();
        if (nearestOpponent == null)
            return false;

        float healthRatio = _selfState.MaxHealth > 0f ? _selfState.CurrentHealth / _selfState.MaxHealth : 1f;
        float selfEffectiveRange = _selfState.BodyRadius + nearestOpponent.BodyRadius + _selfState.AttackRange + 0.05f;
        float opponentEffectiveRange =
            nearestOpponent.BodyRadius + _selfState.BodyRadius + nearestOpponent.AttackRange + 0.05f;
        bool insideEnemyRange = nearestDist <= opponentEffectiveRange;
        bool closeToEnemy = nearestDist <= Mathf.Max(selfEffectiveRange, opponentEffectiveRange) * 1.15f;
        bool lowHealthAndThreatened = healthRatio <= 0.45f && (insideEnemyRange || closeToEnemy);
        bool criticalHealthNearEnemy = healthRatio <= 0.3f && closeToEnemy;
        bool cooldownSpacing = _selfState.AttackCooldownRemaining > 0f && insideEnemyRange;
        bool rangedTooClose = _selfState.AttackRange >= 3f && nearestDist <= selfEffectiveRange * 0.45f;
        bool outnumberedNearby =
            CountNearbyOpponents(opponentEffectiveRange * 1.25f)
            > CountNearbyTeammates(opponentEffectiveRange * 1.25f) + 1;
        bool lowSupportThreat = healthRatio <= 0.6f && outnumberedNearby && closeToEnemy;
        bool boundaryThreat = IsNearBattlefieldBoundary() && closeToEnemy;

        return lowHealthAndThreatened
            || criticalHealthNearEnemy
            || cooldownSpacing
            || rangedTooClose
            || lowSupportThreat
            || boundaryThreat;
    }

    private int CountNearbyOpponents(float radius)
    {
        int count = 0;
        float sqrRadius = radius * radius;
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState target = ResolveOpponentSlot(i);
            if (target == null || target.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = target.Position - _selfState.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
            {
                count++;
            }
        }

        return count;
    }

    private int CountNearbyTeammates(float radius)
    {
        if (_rosterView == null)
        {
            return 0;
        }

        int count = 0;
        float sqrRadius = radius * radius;
        IReadOnlyList<BattleUnitCombatState> teammates = _rosterView.Teammates;
        for (int i = 0; i < teammates.Count; i++)
        {
            BattleUnitCombatState teammate = teammates[i];
            if (teammate == null || teammate.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = teammate.Position - _selfState.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
            {
                count++;
            }
        }

        return count;
    }

    private bool IsNearBattlefieldBoundary()
    {
        float playableRadius = _arenaExtentsMin - _selfState.BodyRadius;
        if (playableRadius <= 0f || playableRadius >= float.MaxValue)
        {
            return false;
        }

        Vector3 flatPos = new Vector3(_selfState.Position.x, 0f, _selfState.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        return Vector3.Distance(flatPos, flatCenter) >= playableRadius * 0.85f;
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
        _selfUnit.OnSkillActivated -= HandleSkillActivated;
        _selfUnit.OnSkillFailed -= HandleSkillFailed;
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
