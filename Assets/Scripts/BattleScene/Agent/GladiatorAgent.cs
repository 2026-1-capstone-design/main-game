using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size        = GladiatorObservationSchema.TotalSize (= 85)
//   Discrete Branches = 2
//     Branch 0 Size = GladiatorActionSchema.MoveAttackBranchSize (= 8)
//     Branch 1 Size = GladiatorActionSchema.RotationBranchSize (= 3)
//
// Observation (85 floats):
//   자신      (8):      경기장 중심 상대좌표(x,z), 체력, 최대 체력, 공격력, 사거리, 이동속도, 공격 쿨타임
//   내 팀 동료 (5 × 7): payload team-local index 오름차순 고정 슬롯
//   상대팀    (6 × 7): payload team-local index 오름차순 고정 슬롯
//
// Action:
//   Branch 0 (이동/공격): 0=멈춤  1=앞으로  2~7=상대팀 고정 슬롯 0~5 공격
//   Branch 1 (회전):      0=없음  1=왼쪽    2=오른쪽
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
    }

    private void HandleSelfDied()
    {
        AddReward(rewardConfig.death);
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, bool wasKill)
    {
        AddReward(rewardConfig.attackLanded);
        if (wasKill)
        {
            AddReward(rewardConfig.kill);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        BattleObservationBuilder.Write(sensor, new GladiatorObservationContext(_selfUnit, _rosterView, _arenaCenter));
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled || _rosterView == null)
        {
            return;
        }

        bool attackBlocked = _selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking;
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled || attackBlocked || IsOutOfAttackRange(target);
            if (invalid)
            {
                actionMask.SetActionEnabled(0, i + GladiatorActionSchema.AttackStart, false);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            return;
        }

        int mainAction = actions.DiscreteActions[0];
        int rotateAction = actions.DiscreteActions[1];

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

        float avgDist = GetAverageDistToOpponents();
        if (avgDist < float.MaxValue)
        {
            AddReward((1f / (1f + avgDist)) * rewardConfig.distanceShapingScale);
        }

        float rotDelta = rotateAction switch
        {
            1 => -RotationSpeedDegPerSec,
            2 => RotationSpeedDegPerSec,
            _ => 0f,
        };

        if (mainAction == GladiatorActionSchema.Forward)
        {
            _selfUnit.SetExternalMovement(_selfUnit.transform.forward, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
            return;
        }

        if (mainAction >= GladiatorActionSchema.AttackStart)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(mainAction - GladiatorActionSchema.AttackStart);
            if (_selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking)
            {
                AddReward(rewardConfig.invalidAction);
                return;
            }

            if (target == null || target.IsCombatDisabled || IsOutOfAttackRange(target))
            {
                AddReward(rewardConfig.invalidAction);
                return;
            }

            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(target);
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

    public void GiveEndReward(bool allyWon) { }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var kb = Keyboard.current;
        var discrete = actionsOut.DiscreteActions;
        if (kb == null)
        {
            return;
        }

        if (kb.wKey.isPressed)
            discrete[0] = GladiatorActionSchema.Forward;
        else if (kb.jKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart;
        else if (kb.kKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart + 1;
        else if (kb.lKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart + 2;
        else if (kb.uKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart + 3;
        else if (kb.iKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart + 4;
        else if (kb.oKey.isPressed)
            discrete[0] = GladiatorActionSchema.AttackStart + 5;
        else
            discrete[0] = GladiatorActionSchema.Idle;

        if (kb.qKey.isPressed)
            discrete[1] = 1;
        else if (kb.eKey.isPressed)
            discrete[1] = 2;
        else
            discrete[1] = 0;
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

    private float GetAverageDistToOpponents() =>
        _rosterView != null ? _rosterView.GetAverageDistanceToHostiles(_selfUnit) : float.MaxValue;

    private BattleRuntimeUnit ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(_selfUnit, slotIndex) : null;

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
