using System.Collections.Generic;
using UnityEngine;

public sealed class BattlePhysicsSystem
{
    private SphereCollider _battlefieldCollider;
    private float _desiredPositionStopDistance;
    private IBattleMovementPolicy _movementPolicy = DefaultBattleMovementPolicy.Instance;
    private IReadOnlyList<BattleRuntimeUnit> _units;

    public void Configure(SphereCollider battlefieldCollider, float desiredPositionStopDistance)
    {
        _battlefieldCollider = battlefieldCollider;
        _desiredPositionStopDistance = Mathf.Max(0f, desiredPositionStopDistance);
    }

    public void Execute(
        IReadOnlyList<BattleRuntimeUnit> units,
        float tickDeltaTime,
        BattleControlPlan[] controlPlans = null,
        IBattleMovementPolicy movementPolicy = null,
        BattleSkillChannelSystem channelSystem = null
    )
    {
        if (units == null)
            return;

        _units = units;
        _movementPolicy = movementPolicy ?? DefaultBattleMovementPolicy.Instance;
        ExecuteSpecialEffect(units, tickDeltaTime);
        ExecuteMovementPhase(units, tickDeltaTime, controlPlans, channelSystem);
        ResolveUnitSeparation(units);
    }

    private static void ExecuteSpecialEffect(IReadOnlyList<BattleRuntimeUnit> units, float tickDeltaTime)
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            unit.TickKnockback(tickDeltaTime);
        }
    }

    private void ExecuteMovementPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        float tickDeltaTime,
        BattleControlPlan[] controlPlans,
        BattleSkillChannelSystem channelSystem
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;

            if (unit.IsAttacking)
                continue;

            if (channelSystem != null && channelSystem.IsMovementBlocked(unit))
            {
                unit.State.SetIdleState();
                continue;
            }

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;
            ApplyFacing(unit, plan);
            switch (plan.MoveIntent)
            {
                case BattleMoveIntent.MoveByTacticalInput:
                {
                    bool moved = MoveByTacticalInput(unit, plan, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.MoveToTarget:
                {
                    bool moved = MoveTowardsTarget(unit, plan, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.MoveToPosition:
                {
                    bool moved = MoveTowardsPosition(unit, plan.DesiredPosition, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.Hold:
                case BattleMoveIntent.None:
                default:
                    unit.State.SetIdleState();
                    continue;
            }
        }
    }

    private bool MoveByTacticalInput(BattleRuntimeUnit unit, BattleControlPlan plan, float tickDeltaTime)
    {
        Vector3 moveDirection = BuildMoveDirection(plan.MovementAnchor, plan.RelativeMove, unit);
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return false;

        BattleRuntimeUnit targetRuntime = FindRuntimeUnitForState(plan.TargetEnemy);
        BattleMoveRequest request = BattleMoveRequest.ForMover(
            unit,
            moveDirection.normalized,
            targetRuntime,
            unit.MoveSpeed
        );
        _movementPolicy.ModifyMoveSpeed(ref request);
        unit.SetPosition(unit.Position + request.Direction * request.Speed * tickDeltaTime);
        unit.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private void ApplyFacing(BattleRuntimeUnit unit, in BattleControlPlan plan)
    {
        if (unit == null)
            return;

        switch (plan.FacingIntent)
        {
            case BattleFacingIntent.TargetEnemy:
                if (BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy))
                    unit.FaceTarget(plan.TargetEnemy.Position);
                break;
            case BattleFacingIntent.DesiredPosition:
                if (plan.HasDesiredPosition)
                    unit.FaceTarget(plan.DesiredPosition);
                break;
            case BattleFacingIntent.MoveDirection:
                Vector3 moveDirection = BuildMoveDirection(plan.MovementAnchor, plan.RelativeMove, unit);
                if (moveDirection.sqrMagnitude > 0.0001f)
                    unit.FaceTarget(unit.Position + moveDirection);
                break;
        }
    }

    private Vector3 BuildMoveDirection(BattleAnchor anchor, Vector2 relativeMove, BattleRuntimeUnit unit)
    {
        if (unit == null)
            return Vector3.zero;

        Vector3 anchorForward = ResolveAnchorForward(anchor, unit);
        if (anchorForward.sqrMagnitude <= 0.0001f)
            anchorForward = unit.transform.forward;

        Vector3 anchorLeft = Vector3.Cross(anchorForward, Vector3.up).normalized;
        Vector3 direction = (anchorForward * relativeMove.y) + (anchorLeft * relativeMove.x);
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        return direction;
    }

    private Vector3 ResolveAnchorForward(BattleAnchor anchor, BattleRuntimeUnit unit)
    {
        Vector3 anchorPosition = anchor.HasUnit ? anchor.Unit.Position : anchor.Position;
        if (anchor.Kind == BattleAnchorKind.TeamCenter && _battlefieldCollider != null)
            anchorPosition = _battlefieldCollider.bounds.center;

        Vector3 toAnchor = anchorPosition - unit.Position;
        toAnchor.y = 0f;
        return toAnchor.sqrMagnitude > 0.0001f ? toAnchor.normalized : Vector3.zero;
    }

    private bool MoveTowardsTarget(BattleRuntimeUnit mover, BattleControlPlan plan, float tickDeltaTime)
    {
        BattleUnitCombatState target = plan.TargetEnemy;
        if (mover == null || target == null)
            return false;

        Vector3 currentPosition = mover.Position;
        Vector3 targetPosition = target.Position;
        Vector3 toTarget = targetPosition - currentPosition;
        toTarget.y = 0f;

        float centerDistance = toTarget.magnitude;
        float effectiveAttackDistance = BattleFieldSnapshot.GetEffectiveAttackDistance(mover.State, target);
        if (centerDistance <= effectiveAttackDistance)
            return false;

        Vector3 direction = centerDistance > 0.0001f ? toTarget / centerDistance : Vector3.zero;
        if (plan.CombatIntent == BattleCombatIntent.Attack)
        {
            direction = BuildAttackApproachDirection(plan.RelativeMove, direction);
        }

        float remainingDistanceUntilAttack = Mathf.Max(0f, centerDistance - effectiveAttackDistance);
        BattleRuntimeUnit targetRuntime = FindRuntimeUnitForState(target);
        BattleMoveRequest request = BattleMoveRequest.ForMover(mover, direction, targetRuntime, mover.MoveSpeed);
        _movementPolicy.ModifyMoveSpeed(ref request);
        float moveDistance = Mathf.Min(request.Speed * tickDeltaTime, remainingDistanceUntilAttack);
        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + request.Direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private static Vector3 BuildAttackApproachDirection(Vector2 relativeMove, Vector3 anchorForward)
    {
        if (anchorForward.sqrMagnitude <= 0.0001f)
        {
            return anchorForward;
        }

        Vector2 move = relativeMove;
        if (move.sqrMagnitude <= 0.0001f)
        {
            return anchorForward;
        }

        float rawAngle = Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg;
        float compressedAngle = rawAngle * 0.25f;
        Vector3 rotated = Quaternion.AngleAxis(compressedAngle, Vector3.up) * anchorForward;
        rotated.y = 0f;
        return rotated.sqrMagnitude > 0.0001f ? rotated.normalized : anchorForward;
    }

    private bool MoveTowardsPosition(BattleRuntimeUnit mover, Vector3 desiredPosition, float tickDeltaTime)
    {
        if (mover == null)
            return false;

        Vector3 currentPosition = mover.Position;
        Vector3 toTarget = desiredPosition - currentPosition;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance <= _desiredPositionStopDistance)
            return false;

        Vector3 direction = distance > 0.0001f ? toTarget / distance : Vector3.zero;
        BattleMoveRequest request = BattleMoveRequest.ForMover(mover, direction, null, mover.MoveSpeed);
        _movementPolicy.ModifyMoveSpeed(ref request);
        float moveDistance = Mathf.Min(request.Speed * tickDeltaTime, distance);
        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + request.Direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private BattleRuntimeUnit FindRuntimeUnitForState(BattleUnitCombatState state)
    {
        if (state == null || _units == null)
            return null;

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit unit = _units[i];
            if (unit != null && unit.State == state)
                return unit;
        }

        return null;
    }

    private void ResolveUnitSeparation(IReadOnlyList<BattleRuntimeUnit> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit a = units[i];
            if (a == null || a.IsCombatDisabled)
                continue;

            for (int j = i + 1; j < units.Count; j++)
            {
                BattleRuntimeUnit b = units[j];
                if (b == null || b.IsCombatDisabled)
                    continue;

                Vector3 delta = a.Position - b.Position;
                delta.y = 0f;

                float distance = delta.magnitude;
                float minDistance = a.BodyRadius + b.BodyRadius;
                if (distance >= minDistance)
                    continue;

                Vector3 pushDirection;
                if (distance > 0.0001f)
                {
                    pushDirection = delta / distance;
                }
                else
                {
                    pushDirection = (a.UnitNumber <= b.UnitNumber) ? Vector3.left : Vector3.right;
                    distance = 0f;
                }

                float overlap = minDistance - distance;
                Vector3 push = pushDirection * (overlap * 0.5f);

                a.SetPosition(a.Position + push);
                b.SetPosition(b.Position - push);

                a.ClampInsideBattlefield(_battlefieldCollider);
                b.ClampInsideBattlefield(_battlefieldCollider);
            }
        }
    }
}
