using System.Collections.Generic;
using UnityEngine;

public sealed class BattlePhysicsSystem
{
    private SphereCollider _battlefieldCollider;
    private float _desiredPositionStopDistance;
    private IBattleMovementPolicy _movementPolicy = DefaultBattleMovementPolicy.Instance;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;

    public void Configure(SphereCollider battlefieldCollider, float desiredPositionStopDistance)
    {
        _battlefieldCollider = battlefieldCollider;
        _desiredPositionStopDistance = Mathf.Max(0f, desiredPositionStopDistance);
    }

    public void Execute(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        float tickDeltaTime,
        BattleControlPlan[] controlPlans = null,
        IBattleMovementPolicy movementPolicy = null,
        BattleSkillChannelSystem channelSystem = null
    )
    {
        if (units == null)
            return;

        _runtimeUnitByState = runtimeUnitByState;
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
            if (unit == null)
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
            switch (plan.Move.Intent)
            {
                case BattleMoveIntent.MoveToAbsoluteDirection:
                {
                    bool moved = MoveByAbsoluteDirection(unit, plan.Move.Direction, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.MoveToRelativeDirection:
                {
                    bool moved = MoveByRelativeDirection(unit, plan.Move.Target, plan.Move.Direction, tickDeltaTime);
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
                case BattleMoveIntent.MoveToAbsolutePosition:
                {
                    bool moved = MoveTowardsPosition(unit, plan.Move.Position, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.MoveToRelativePosition:
                {
                    bool moved = MoveTowardsRelativePosition(
                        unit,
                        plan.Move.Target,
                        plan.Move.Direction,
                        tickDeltaTime
                    );
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                    continue;
                }
                case BattleMoveIntent.Hold:
                default:
                    unit.State.SetIdleState();
                    continue;
            }
        }
    }

    private bool MoveByAbsoluteDirection(BattleRuntimeUnit unit, Vector3 direction, float tickDeltaTime)
    {
        if (unit == null)
            return false;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        BattleMoveRequest request = BattleMoveRequest.ForMover(unit, direction.normalized, null, unit.MoveSpeed);
        _movementPolicy.ModifyMoveSpeed(ref request);
        unit.SetPosition(unit.Position + request.Direction * request.Speed * tickDeltaTime);
        unit.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private bool MoveByRelativeDirection(
        BattleRuntimeUnit unit,
        BattleUnitCombatState target,
        Vector3 relativeDirection,
        float tickDeltaTime
    )
    {
        if (unit == null)
            return false;

        Vector3 moveDirection = BuildRelativeMoveDirection(target, relativeDirection, unit);
        if (moveDirection.sqrMagnitude <= 0.0001f)
            return false;

        BattleRuntimeUnit targetRuntime = FindRuntimeUnitForState(target);
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
                if (TryResolveDesiredPosition(unit, plan.Move, out Vector3 desiredPosition))
                    unit.FaceTarget(desiredPosition);
                break;
            case BattleFacingIntent.MoveDirection:
                Vector3 moveDirection = ResolveMoveDirection(unit, plan.Move);
                if (moveDirection.sqrMagnitude > 0.0001f)
                    unit.FaceTarget(unit.Position + moveDirection);
                break;
        }
    }

    private static Vector3 ResolveMoveDirection(BattleRuntimeUnit unit, BattleMove move)
    {
        if (unit == null)
            return Vector3.zero;

        Vector3 direction;

        switch (move.Intent)
        {
            case BattleMoveIntent.MoveToAbsolutePosition:
                direction = move.Position - unit.Position;
                break;

            case BattleMoveIntent.MoveToRelativePosition:
                if (!TryResolveRelativePosition(unit, move.Target, move.Direction, out Vector3 relativePosition))
                    return Vector3.zero;

                direction = relativePosition - unit.Position;
                break;

            case BattleMoveIntent.MoveToTarget:
                if (move.Target == null)
                    return Vector3.zero;

                direction = move.Target.Position - unit.Position;
                break;

            case BattleMoveIntent.MoveToRelativeDirection:
                direction = BuildRelativeMoveDirection(move.Target, move.Direction, unit);
                break;

            case BattleMoveIntent.MoveToAbsoluteDirection:
                direction = move.Direction;
                break;

            default:
                return Vector3.zero;
        }

        direction.y = 0f;
        return direction;
    }

    private static Vector3 BuildRelativeMoveDirection(
        BattleUnitCombatState target,
        Vector3 relativeDirection,
        BattleRuntimeUnit unit
    )
    {
        if (unit == null || target == null)
            return Vector3.zero;

        Vector3 anchorForward = target.Position - unit.Position;
        anchorForward.y = 0f;
        if (anchorForward.sqrMagnitude <= 0.0001f)
            anchorForward = unit.transform.forward;

        anchorForward.y = 0f;
        if (anchorForward.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        anchorForward.Normalize();
        Vector3 anchorLeft = Vector3.Cross(anchorForward, Vector3.up).normalized;
        Vector3 direction = (anchorForward * relativeDirection.z) + (anchorLeft * relativeDirection.x);
        direction.y = 0f;
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        return direction;
    }

    private static bool TryResolveRelativePosition(
        BattleRuntimeUnit unit,
        BattleUnitCombatState target,
        Vector3 relativePosition,
        out Vector3 position
    )
    {
        position = default;
        if (unit == null || target == null)
            return false;

        Vector3 offset = BuildRelativeMoveDirection(target, relativePosition, unit);
        position = target.Position + offset;
        return true;
    }

    private static bool TryResolveDesiredPosition(BattleRuntimeUnit unit, BattleMove move, out Vector3 position)
    {
        switch (move.Intent)
        {
            case BattleMoveIntent.MoveToAbsolutePosition:
                position = move.Position;
                return true;
            case BattleMoveIntent.MoveToRelativePosition:
                return TryResolveRelativePosition(unit, move.Target, move.Direction, out position);
            default:
                position = default;
                return false;
        }
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
        if (plan.CombatIntent == BattleCombatIntent.Attack && plan.Move.Direction.sqrMagnitude > 0.0001f)
        {
            direction = BuildRelativeMoveDirection(target, plan.Move.Direction, mover);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = centerDistance > 0.0001f ? toTarget / centerDistance : Vector3.zero;
            }
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

    private bool MoveTowardsRelativePosition(
        BattleRuntimeUnit mover,
        BattleUnitCombatState target,
        Vector3 relativePosition,
        float tickDeltaTime
    )
    {
        if (!TryResolveRelativePosition(mover, target, relativePosition, out Vector3 desiredPosition))
            return false;

        BattleRuntimeUnit targetRuntime = FindRuntimeUnitForState(target);
        return MoveTowardsPosition(mover, desiredPosition, tickDeltaTime, targetRuntime);
    }

    private bool MoveTowardsPosition(BattleRuntimeUnit mover, Vector3 desiredPosition, float tickDeltaTime)
    {
        return MoveTowardsPosition(mover, desiredPosition, tickDeltaTime, null);
    }

    private bool MoveTowardsPosition(
        BattleRuntimeUnit mover,
        Vector3 desiredPosition,
        float tickDeltaTime,
        BattleRuntimeUnit target
    )
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
        BattleMoveRequest request = BattleMoveRequest.ForMover(mover, direction, target, mover.MoveSpeed);
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
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
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
