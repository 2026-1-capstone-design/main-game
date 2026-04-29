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
    private GladiatorRosterView _rosterView;
    private GladiatorObservationStats _observationStats;
    private float _prevDistToNearestEnemy;
    private bool _boundaryResetRequested;
    private Vector2 _previousRawMove;
    private float _previousRawTurn;
    private bool _hasPreviousRawAction;
    private readonly List<BattleUnitCombatState> _teammateObservationStates = new List<BattleUnitCombatState>();
    private readonly List<BattleUnitCombatState> _opponentObservationStates = new List<BattleUnitCombatState>();

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
        _flowManager = flowManager;
        _trainingBootstrapper = trainingBootstrapper;
        _boundaryResetRequested = false;

        SphereCollider col = flowManager?.battlefieldCollider;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        _arenaExtentsMin = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : float.MaxValue;
        _rosterView = CreateRosterView();
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
        FillObservationStates();
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(
                _selfUnit != null ? _selfUnit.State : null,
                _teammateObservationStates,
                _opponentObservationStates,
                _observationStats,
                _selfUnit != null ? _selfUnit.transform.right : Vector3.right,
                _selfUnit != null ? _selfUnit.transform.forward : Vector3.forward,
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
        if (_selfUnit == null || _selfUnit.IsCombatDisabled || _rosterView == null)
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
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled;
            if (invalid)
            {
                actionMask.SetActionEnabled(GladiatorActionSchema.TargetBranch, i, false);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            return;
        }

        Vector2 localMove = Vector2.zero;
        if (actions.ContinuousActions.Length >= GladiatorActionSchema.ContinuousSize)
        {
            localMove = new Vector2(
                Mathf.Clamp(actions.ContinuousActions[GladiatorActionSchema.ContinuousMoveX], -1f, 1f),
                Mathf.Clamp(actions.ContinuousActions[GladiatorActionSchema.ContinuousMoveZ], -1f, 1f)
            );
            if (localMove.sqrMagnitude > 1f)
            {
                localMove.Normalize();
            }
        }

        float turn =
            actions.ContinuousActions.Length > GladiatorActionSchema.ContinuousTurn
                ? Mathf.Clamp(actions.ContinuousActions[GladiatorActionSchema.ContinuousTurn], -1f, 1f)
                : 0f;

        int command =
            actions.DiscreteActions.Length > GladiatorActionSchema.CommandBranch
                ? actions.DiscreteActions[GladiatorActionSchema.CommandBranch]
                : GladiatorActionSchema.CommandNone;
        int targetSlot =
            actions.DiscreteActions.Length > GladiatorActionSchema.TargetBranch
                ? actions.DiscreteActions[GladiatorActionSchema.TargetBranch]
                : 0;
        int stance =
            actions.DiscreteActions.Length > GladiatorActionSchema.StanceBranch
                ? actions.DiscreteActions[GladiatorActionSchema.StanceBranch]
                : GladiatorActionSchema.StanceNeutral;

        AddReward(rewardConfig.step);
        ApplyActionSmoothnessReward(localMove, turn);

        float playableRadius = _arenaExtentsMin - _selfUnit.BodyRadius;
        Vector3 flatPos = new Vector3(_selfUnit.Position.x, 0f, _selfUnit.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        float distanceFromCenter = Vector3.Distance(flatPos, flatCenter);
        if (distanceFromCenter >= playableRadius)
        {
            AddReward(rewardConfig.boundary);
            if (distanceFromCenter >= playableRadius * HardBoundaryRadiusMultiplier)
            {
                RequestBoundaryReset();
                _selfUnit.ClearExternalControlInput();
                return;
            }
        }

        float nearestDist = GetDistToNearestOpponent();
        bool shouldRetreat = ShouldRetreat(nearestDist);
        if (_prevDistToNearestEnemy < float.MaxValue && nearestDist < float.MaxValue)
        {
            float approachDelta = _prevDistToNearestEnemy - nearestDist;
            if (Mathf.Abs(approachDelta) > 0.0001f)
            {
                if (approachDelta < 0f && shouldRetreat)
                    AddReward(-approachDelta * rewardConfig.retreatDistance);
                else
                    AddReward(approachDelta * rewardConfig.approach);
            }
        }

        bool attackBlocked = _selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking;
        bool isSpacingMove = localMove.y < -0.2f || stance == GladiatorActionSchema.StanceKeepRange;
        bool wantsBasicAttack = command == GladiatorActionSchema.CommandBasicAttack;
        if (!attackBlocked && !isSpacingMove && !wantsBasicAttack && HasAttackableOpponent())
        {
            AddReward(rewardConfig.inRangeNoAttack);
        }

        if (
            !attackBlocked
            && localMove.sqrMagnitude <= 0.0001f
            && command == GladiatorActionSchema.CommandNone
            && HasLivingOpponent()
        )
        {
            AddReward(rewardConfig.disengaged);
        }

        _prevDistToNearestEnemy = nearestDist;

        BattleRuntimeUnit target = ResolveOpponentSlot(targetSlot);
        bool hasValidTarget = target != null && !target.IsCombatDisabled;
        int effectiveCommand = command;
        BattleRuntimeUnit effectiveTarget = hasValidTarget ? target : null;

        if (command != GladiatorActionSchema.CommandNone && !hasValidTarget)
        {
            AddReward(rewardConfig.invalidAction);
            effectiveCommand = GladiatorActionSchema.CommandNone;
        }

        if (isSpacingMove)
        {
            AddReward(shouldRetreat ? rewardConfig.goodRetreat : rewardConfig.badRetreat);
        }

        if (effectiveCommand == GladiatorActionSchema.CommandBasicAttack)
        {
            if (shouldRetreat)
            {
                AddReward(rewardConfig.dangerousAttack);
            }

            if (IsOutOfAttackRange(target))
            {
                AddReward(rewardConfig.chaseTarget);
            }

            if (attackBlocked)
            {
                AddReward(rewardConfig.invalidSkill * 0.25f);
            }
        }

        if (effectiveCommand == GladiatorActionSchema.CommandSkill && !CanRequestSkill())
        {
            AddReward(rewardConfig.invalidSkill);
            effectiveCommand = GladiatorActionSchema.CommandNone;
        }

        _selfUnit.SetExternalControlInput(localMove, turn, effectiveCommand, stance, effectiveTarget);
    }

    public override void OnEpisodeBegin()
    {
        _boundaryResetRequested = false;
        _prevDistToNearestEnemy = GetDistToNearestOpponent();
        _previousRawMove = Vector2.zero;
        _previousRawTurn = 0f;
        _hasPreviousRawAction = false;
        _selfUnit?.ClearExternalControlInput();
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

    private bool IsOutOfAttackRange(BattleRuntimeUnit target)
    {
        if (target == null || _selfUnit == null)
        {
            return true;
        }

        Vector3 delta = target.Position - _selfUnit.Position;
        delta.y = 0f;
        float effectiveRange = _selfUnit.BodyRadius + target.BodyRadius + _selfUnit.AttackRange + 0.05f;
        return delta.magnitude > effectiveRange;
    }

    private float GetDistToNearestOpponent() =>
        _rosterView != null ? _rosterView.GetDistanceToNearestHostile(_selfUnit) : float.MaxValue;

    private BattleRuntimeUnit ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(_selfUnit, slotIndex) : null;

    private bool HasAttackableOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
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
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target != null && !target.IsCombatDisabled)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyActionSmoothnessReward(Vector2 localMove, float turn)
    {
        if (_hasPreviousRawAction)
        {
            float moveDelta = Vector2.Distance(_previousRawMove, localMove);
            float turnDelta = Mathf.Abs(_previousRawTurn - turn);
            AddReward(moveDelta * rewardConfig.actionDelta);
            AddReward(turnDelta * rewardConfig.turnDelta);

            if (localMove.sqrMagnitude <= 0.0001f && Mathf.Abs(turn) > 0.1f)
            {
                AddReward(Mathf.Abs(turn) * rewardConfig.idleJitter);
            }
        }

        _previousRawMove = localMove;
        _previousRawTurn = turn;
        _hasPreviousRawAction = true;
    }

    private bool CanRequestSkill()
    {
        return _selfUnit != null && _selfUnit.HasReadySkill();
    }

    private Vector3 ResolveKeepRangeDirection(BattleRuntimeUnit target)
    {
        BattleRuntimeUnit resolvedTarget =
            target != null && !target.IsCombatDisabled ? target : ResolveNearestOpponent();
        if (resolvedTarget == null)
            return Vector3.zero;

        Vector3 toTarget = resolvedTarget.Position - _selfUnit.Position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
            return -_selfUnit.transform.forward;

        float effectiveRange = _selfUnit.BodyRadius + resolvedTarget.BodyRadius + _selfUnit.AttackRange;
        float minRange = effectiveRange * 0.65f;
        float maxRange = effectiveRange * 0.95f;
        if (distance < minRange)
            return -toTarget.normalized;
        if (distance > maxRange)
            return toTarget.normalized;

        return Vector3.zero;
    }

    private BattleRuntimeUnit ResolveNearestOpponent()
    {
        BattleRuntimeUnit nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target == null || target.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = target.Position - _selfUnit.Position;
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
        if (_selfUnit == null || nearestDist >= float.MaxValue)
            return false;

        BattleRuntimeUnit nearestOpponent = ResolveNearestOpponent();
        if (nearestOpponent == null)
            return false;

        float healthRatio = _selfUnit.MaxHealth > 0f ? _selfUnit.CurrentHealth / _selfUnit.MaxHealth : 1f;
        float selfEffectiveRange = _selfUnit.BodyRadius + nearestOpponent.BodyRadius + _selfUnit.AttackRange + 0.05f;
        float opponentEffectiveRange =
            nearestOpponent.BodyRadius + _selfUnit.BodyRadius + nearestOpponent.AttackRange + 0.05f;
        bool insideEnemyRange = nearestDist <= opponentEffectiveRange;
        bool closeToEnemy = nearestDist <= Mathf.Max(selfEffectiveRange, opponentEffectiveRange) * 1.15f;
        bool lowHealthAndThreatened = healthRatio <= 0.45f && (insideEnemyRange || closeToEnemy);
        bool criticalHealthNearEnemy = healthRatio <= 0.3f && closeToEnemy;
        bool cooldownSpacing = _selfUnit.AttackCooldownRemaining > 0f && insideEnemyRange;
        bool rangedTooClose = _selfUnit.AttackRange >= 3f && nearestDist <= selfEffectiveRange * 0.45f;
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
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target == null || target.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = target.Position - _selfUnit.Position;
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
        IReadOnlyList<BattleRuntimeUnit> teammates = _rosterView.GetSortedTeammates(_selfUnit);
        for (int i = 0; i < teammates.Count; i++)
        {
            BattleRuntimeUnit teammate = teammates[i];
            if (teammate == null || teammate.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = teammate.Position - _selfUnit.Position;
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
        float playableRadius = _arenaExtentsMin - _selfUnit.BodyRadius;
        if (playableRadius <= 0f || playableRadius >= float.MaxValue)
        {
            return false;
        }

        Vector3 flatPos = new Vector3(_selfUnit.Position.x, 0f, _selfUnit.Position.z);
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

    private GladiatorRosterView CreateRosterView()
    {
        BattleStartPayload payload = _flowManager != null ? _flowManager.CurrentPayload : null;
        IBattleRosterProjection projection = payload != null ? new BattleRosterProjection(payload) : null;
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        return new GladiatorRosterView(_selfUnit, payload, projection, runtimeUnits);
    }

    private void FillObservationStates()
    {
        _teammateObservationStates.Clear();
        _opponentObservationStates.Clear();

        if (_rosterView == null || _selfUnit == null)
        {
            return;
        }

        AddObservationStates(_rosterView.GetSortedTeammates(_selfUnit), _teammateObservationStates);
        AddObservationStates(_rosterView.GetSortedHostiles(_selfUnit), _opponentObservationStates);
    }

    private static void AddObservationStates(
        IReadOnlyList<BattleRuntimeUnit> units,
        List<BattleUnitCombatState> states
    )
    {
        if (units == null)
        {
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            states.Add(unit != null ? unit.State : null);
        }
    }

    private GladiatorObservationStats ComputeInitialObservationStats()
    {
        var maxHealthValues = new List<float>();
        var attackValues = new List<float>();
        float maxMoveSpeed = 0f;

        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        bool sawSelf = false;
        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == _selfUnit)
                {
                    sawSelf = true;
                }

                AddInitialUnitStats(unit, maxHealthValues, attackValues, ref maxMoveSpeed);
            }
        }

        if (!sawSelf)
        {
            AddInitialUnitStats(_selfUnit, maxHealthValues, attackValues, ref maxMoveSpeed);
        }

        float fallbackMaxHealth = _selfUnit != null ? _selfUnit.MaxHealth : 1f;
        float fallbackAttack = _selfUnit != null ? _selfUnit.Attack : 1f;
        float fallbackMoveSpeed = _selfUnit != null ? _selfUnit.MoveSpeed : 1f;

        return new GladiatorObservationStats(
            Median(maxHealthValues, fallbackMaxHealth),
            Median(attackValues, fallbackAttack),
            maxMoveSpeed > 0f ? maxMoveSpeed : fallbackMoveSpeed
        );
    }

    private static void AddInitialUnitStats(
        BattleRuntimeUnit unit,
        List<float> maxHealthValues,
        List<float> attackValues,
        ref float maxMoveSpeed
    )
    {
        if (unit == null)
        {
            return;
        }

        if (unit.MaxHealth > 0f)
        {
            maxHealthValues.Add(unit.MaxHealth);
        }

        if (unit.Attack > 0f)
        {
            attackValues.Add(unit.Attack);
        }

        maxMoveSpeed = Mathf.Max(maxMoveSpeed, unit.MoveSpeed);
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
