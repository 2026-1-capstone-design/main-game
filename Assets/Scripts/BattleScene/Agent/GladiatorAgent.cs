using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size        = GladiatorObservationSchema.TotalSize (= 93)
//   Discrete Branches = 4
//     Branch 0 Size = GladiatorActionSchema.IntentBranchSize (= 3)
//     Branch 1 Size = GladiatorActionSchema.MoveBranchSize (= 6)
//     Branch 2 Size = GladiatorActionSchema.TargetBranchSize (= 6)
//     Branch 3 Size = GladiatorActionSchema.RotationBranchSize (= 3)
//
// Observation (93 floats):
//   자신      (16):     경기장 중심 상대좌표(x,z), 체력, 최대 체력, 공격력, 사거리, 이동속도, 공격 쿨타임,
//                       체력비, 낮은 체력비, 최근접 적 거리, 공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박
//   내 팀 동료 (5 × 7): payload team-local index 오름차순 고정 슬롯
//   상대팀    (6 × 7): payload team-local index 오름차순 고정 슬롯
//
// Action:
//   Branch 0 (의도):      0=이동  1=공격  2=유지
//   Branch 1 (이동):      0=없음  1=전진  2=후퇴  3=좌측  4=우측  5=거리유지
//   Branch 2 (대상):      0~5=상대팀 고정 슬롯
//   Branch 3 (회전):      0=없음  1=왼쪽  2=오른쪽
public class GladiatorAgent : Agent
{
    private const float RotationSpeedDegPerSec = 240f;
    private const float HardBoundaryRadiusMultiplier = 1.25f;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private TrainingBootstrapper _trainingBootstrapper;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;
    private GladiatorRosterView _rosterView;
    private float _prevDistToNearestEnemy;
    private bool _boundaryResetRequested;

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

        if (_selfUnit != null)
        {
            _selfUnit.SetControlMode(BattleUnitControlMode.ExternalAgent);
            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
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

    public override void CollectObservations(VectorSensor sensor)
    {
        float arenaRadius = _selfUnit != null ? _arenaExtentsMin - _selfUnit.BodyRadius : float.MaxValue;
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(_selfUnit, _rosterView, _arenaCenter, arenaRadius)
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

        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled;
            if (invalid)
            {
                actionMask.SetActionEnabled(2, i, false);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            return;
        }

        int intent = actions.DiscreteActions[0];
        int moveMode = actions.DiscreteActions[1];
        int targetSlot = actions.DiscreteActions[2];
        int rotateAction = actions.DiscreteActions[3];

        AddReward(rewardConfig.step);

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
                _selfUnit.SetExternalMovement(Vector3.zero, 0f);
                _selfUnit.SetExternalAttackTarget(null);
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

        float rotDelta = rotateAction switch
        {
            1 => -RotationSpeedDegPerSec,
            2 => RotationSpeedDegPerSec,
            _ => 0f,
        };

        bool attackBlocked = _selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking;
        bool isSpacingMove =
            intent == GladiatorActionSchema.IntentMove
            && (moveMode == GladiatorActionSchema.MoveBackward || moveMode == GladiatorActionSchema.MoveKeepRange);
        if (!attackBlocked && !isSpacingMove && intent != GladiatorActionSchema.IntentAttack && HasAttackableOpponent())
        {
            AddReward(rewardConfig.inRangeNoAttack);
        }

        if (!attackBlocked && intent == GladiatorActionSchema.IntentHold && HasLivingOpponent())
        {
            AddReward(rewardConfig.disengaged);
        }

        _prevDistToNearestEnemy = nearestDist;

        if (intent == GladiatorActionSchema.IntentMove)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(targetSlot);
            Vector3 movement = ResolveMovementDirection(moveMode, target);
            if (isSpacingMove)
            {
                AddReward(shouldRetreat ? rewardConfig.goodRetreat : rewardConfig.badRetreat);
            }

            _selfUnit.SetExternalMovement(movement, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
            return;
        }

        if (intent == GladiatorActionSchema.IntentAttack)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(targetSlot);
            if (target == null || target.IsCombatDisabled)
            {
                AddReward(rewardConfig.invalidAction);
                return;
            }

            if (shouldRetreat)
            {
                AddReward(rewardConfig.dangerousAttack);
            }

            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(target);
            if (IsOutOfAttackRange(target))
            {
                AddReward(rewardConfig.chaseTarget);
            }

            return;
        }

        _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
        _selfUnit.SetExternalAttackTarget(null);
    }

    public override void OnEpisodeBegin()
    {
        _boundaryResetRequested = false;
        _prevDistToNearestEnemy = GetDistToNearestOpponent();
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
        var discrete = actionsOut.DiscreteActions;
        if (kb == null)
        {
            return;
        }

        if (kb.jKey.isPressed)
            discrete[0] = GladiatorActionSchema.IntentAttack;
        else if (kb.wKey.isPressed || kb.sKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed)
            discrete[0] = GladiatorActionSchema.IntentMove;
        else
            discrete[0] = GladiatorActionSchema.IntentHold;

        if (kb.qKey.isPressed)
            discrete[3] = GladiatorActionSchema.RotateLeft;
        else if (kb.eKey.isPressed)
            discrete[3] = GladiatorActionSchema.RotateRight;
        else
            discrete[3] = GladiatorActionSchema.RotateNone;

        if (kb.wKey.isPressed)
            discrete[1] = GladiatorActionSchema.MoveForward;
        else if (kb.sKey.isPressed)
            discrete[1] = GladiatorActionSchema.MoveBackward;
        else if (kb.aKey.isPressed)
            discrete[1] = GladiatorActionSchema.MoveStrafeLeft;
        else if (kb.dKey.isPressed)
            discrete[1] = GladiatorActionSchema.MoveStrafeRight;
        else
            discrete[1] = GladiatorActionSchema.MoveNone;

        if (kb.jKey.isPressed)
            discrete[2] = 0;
        else if (kb.kKey.isPressed)
            discrete[2] = 1;
        else if (kb.lKey.isPressed)
            discrete[2] = 2;
        else if (kb.uKey.isPressed)
            discrete[2] = 3;
        else if (kb.iKey.isPressed)
            discrete[2] = 4;
        else if (kb.oKey.isPressed)
            discrete[2] = 5;
        else
            discrete[2] = 0;
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

    private Vector3 ResolveMovementDirection(int moveMode, BattleRuntimeUnit target)
    {
        return moveMode switch
        {
            GladiatorActionSchema.MoveForward => _selfUnit.transform.forward,
            GladiatorActionSchema.MoveBackward => -_selfUnit.transform.forward,
            GladiatorActionSchema.MoveStrafeLeft => -_selfUnit.transform.right,
            GladiatorActionSchema.MoveStrafeRight => _selfUnit.transform.right,
            GladiatorActionSchema.MoveKeepRange => ResolveKeepRangeDirection(target),
            _ => Vector3.zero,
        };
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
}
