using System.Collections.Generic;
using UnityEngine;

public sealed class BattlePhysicsSystem
{
    private SphereCollider _battlefieldCollider;
    private float _desiredPositionStopDistance;

    public void Configure(SphereCollider battlefieldCollider, float desiredPositionStopDistance)
    {
        _battlefieldCollider = battlefieldCollider;
        _desiredPositionStopDistance = Mathf.Max(0f, desiredPositionStopDistance);
    }

    public void Execute(IReadOnlyList<BattleRuntimeUnit> units, float tickDeltaTime, BattleControlPlan[] controlPlans)
    {
        if (units == null)
            return;

        ExecuteSpecialEffect(units, tickDeltaTime);
        ExecuteMovementPhase(units, tickDeltaTime, controlPlans);
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
        BattleControlPlan[] controlPlans
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;

            if (unit.IsAttacking)
                continue;

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;

            if (plan.UsesExplicitCombatCommands)
            {
                Vector3 explicitMoveDirection = GetWorldMoveDirection(plan.LocalMove);
                if (explicitMoveDirection.sqrMagnitude > 0.0001f)
                {
                    if (
                        plan.Command == BattleCombatCommand.BasicAttack
                        && BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy)
                    )
                    {
                        unit.FaceTarget(plan.TargetEnemy.Position);
                    }
                    else
                    {
                        unit.FaceTarget(unit.Position + explicitMoveDirection);
                    }

                    unit.SetPosition(unit.Position + explicitMoveDirection * unit.MoveSpeed * tickDeltaTime);
                    unit.ClampInsideBattlefield(_battlefieldCollider);
                    unit.State.SetMovementState(true);
                }
                else if (
                    (plan.Command == BattleCombatCommand.BasicAttack
                        || plan.Stance == BattleControlStance.Pressure
                        || plan.Stance == BattleControlStance.Neutral)
                    && BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy)
                )
                {
                    unit.FaceTarget(plan.TargetEnemy.Position);
                    bool moved = MoveTowardsTarget(unit, plan.TargetEnemy, tickDeltaTime);
                    unit.State.SetMovementState(moved);
                    if (!moved)
                        unit.State.SetIdleState();
                }
                else
                {
                    if (BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy))
                        unit.FaceTarget(plan.TargetEnemy.Position);

                    unit.State.SetIdleState();
                }

                continue;
            }

            BattleUnitCombatState targetEnemy = plan.TargetEnemy != null ? plan.TargetEnemy : unit.PlannedTargetEnemy;
            if (
                BattleFieldSnapshot.IsValidEnemyTarget(unit.State, targetEnemy)
                && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(unit.State, targetEnemy)
            )
            {
                if (unit.IsMoving)
                    unit.State.SetIdleState();

                continue;
            }

            if (plan.HasDesiredPosition || unit.HasPlannedDesiredPosition)
            {
                Vector3 desiredPosition = plan.HasDesiredPosition ? plan.DesiredPosition : unit.PlannedDesiredPosition;
                unit.FaceTarget(desiredPosition);
                bool moved = MoveTowardsPosition(unit, desiredPosition, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();

                continue;
            }

            if (BattleFieldSnapshot.IsValidEnemyTarget(unit.State, targetEnemy))
            {
                unit.FaceTarget(targetEnemy.Position);
                bool moved = MoveTowardsTarget(unit, targetEnemy, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();

                continue;
            }

            unit.State.SetIdleState();
        }
    }

    private static Vector3 GetWorldMoveDirection(Vector2 localMove)
    {
        Vector3 direction = new Vector3(localMove.x, 0f, localMove.y);
        direction.y = 0f;
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
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
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, remainingDistanceUntilAttack);
        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + direction * moveDistance);
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
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, distance);
        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldCollider);
        return true;
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
