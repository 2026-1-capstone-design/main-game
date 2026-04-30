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
        IBattleMovementPolicy movementPolicy = null
    )
    {
        if (units == null)
            return;

        _units = units;
        _movementPolicy = movementPolicy ?? DefaultBattleMovementPolicy.Instance;
        ExecuteSpecialEffect(units, tickDeltaTime);
        ExecuteMovementPhase(units, tickDeltaTime);
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

    private void ExecuteMovementPhase(IReadOnlyList<BattleRuntimeUnit> units, float tickDeltaTime)
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;

            if (unit.IsAttacking)
                continue;

            if (unit.IsExternallyControlled)
            {
                if (unit.ExternalRotationDelta != 0f)
                    unit.Rotate(unit.ExternalRotationDelta * tickDeltaTime);

                if (unit.ExternalMoveDirection.sqrMagnitude > 0.0001f)
                {
                    BattleMoveRequest request = BattleMoveRequest.ForMover(
                        unit,
                        unit.ExternalMoveDirection.normalized,
                        null,
                        unit.MoveSpeed
                    );
                    _movementPolicy.ModifyMoveSpeed(ref request);
                    unit.SetPosition(unit.Position + request.Direction * request.Speed * tickDeltaTime);
                    unit.ClampInsideBattlefield(_battlefieldCollider);
                    unit.State.SetMovementState(true);
                }
                else
                {
                    unit.State.SetIdleState();
                }

                continue;
            }

            BattleUnitCombatState targetEnemy = unit.PlannedTargetEnemy;
            if (
                BattleFieldSnapshot.IsValidEnemyTarget(unit.State, targetEnemy)
                && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(unit.State, targetEnemy)
            )
            {
                if (unit.IsMoving)
                    unit.State.SetIdleState();

                continue;
            }

            if (unit.HasPlannedDesiredPosition)
            {
                unit.FaceTarget(unit.PlannedDesiredPosition);
                bool moved = MoveTowardsPosition(unit, unit.PlannedDesiredPosition, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();

                continue;
            }

            if (BattleFieldSnapshot.IsValidEnemyTarget(unit.State, targetEnemy))
            {
                unit.FaceTarget(unit.PlannedDesiredPosition);
                bool moved = MoveTowardsTarget(unit, targetEnemy, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();

                continue;
            }

            unit.State.SetIdleState();
        }
    }

    private bool MoveTowardsTarget(BattleRuntimeUnit mover, BattleUnitCombatState target, float tickDeltaTime)
    {
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
        if (state == null)
            return null;

        if (_units == null)
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
