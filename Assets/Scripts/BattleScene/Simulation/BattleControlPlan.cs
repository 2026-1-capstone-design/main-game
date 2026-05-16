using UnityEngine;

public enum BattleMoveIntent
{
    None = 0,
    Hold = 1,
    MoveToTarget = 2,
    MoveToPosition = 3,
    MoveByTacticalInput = 4,
}

public enum BattleCombatIntent
{
    None = 0,
    Attack = 1,
    Skill = 2,
}

public enum BattleFacingIntent
{
    KeepCurrent = 0,
    TargetEnemy = 1,
    DesiredPosition = 2,
    MoveDirection = 3,
}

public enum BattleAnchorKind
{
    Enemy = 0,
    Ally = 1,
    TeamCenter = 2,
}

public readonly struct BattleAnchor
{
    public readonly BattleAnchorKind Kind;
    public readonly int SlotIndex;
    public readonly BattleUnitCombatState Unit;
    public readonly Vector3 Position;
    public readonly bool HasUnit;

    public BattleAnchor(
        BattleAnchorKind kind,
        int slotIndex,
        BattleUnitCombatState unit,
        Vector3 position,
        bool hasUnit
    )
    {
        Kind = kind;
        SlotIndex = slotIndex;
        Unit = unit;
        Position = position;
        HasUnit = hasUnit;
    }
}

// BattleControlPlan은 planner가 결정한 tick 단위 최종 실행 명세다.
// 하위 시스템은 explicit/built-in 분기 없이 intent만 읽고 집행한다.
public readonly struct BattleControlPlan
{
    public readonly BattleUnitCombatState TargetEnemy;
    public readonly BattleUnitCombatState TargetAlly;
    public readonly Vector3 DesiredPosition;
    public readonly bool HasDesiredPosition;
    public readonly BattleAnchor MovementAnchor;
    public readonly Vector2 RelativeMove;
    public readonly BattleMoveIntent MoveIntent;
    public readonly BattleCombatIntent CombatIntent;
    public readonly BattleFacingIntent FacingIntent;

    public BattleControlPlan(
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        Vector3 desiredPosition,
        bool hasDesiredPosition,
        BattleAnchor movementAnchor,
        Vector2 relativeMove,
        BattleMoveIntent moveIntent,
        BattleCombatIntent combatIntent,
        BattleFacingIntent facingIntent
    )
    {
        TargetEnemy = targetEnemy;
        TargetAlly = targetAlly;
        DesiredPosition = desiredPosition;
        HasDesiredPosition = hasDesiredPosition;
        MovementAnchor = movementAnchor;
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        MoveIntent = moveIntent;
        CombatIntent = combatIntent;
        FacingIntent = facingIntent;
    }

    public static BattleControlPlan CreateResolvedPlan(
        BattleUnitCombatState self,
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        Vector3 desiredPosition,
        bool hasDesiredPosition,
        BattleAnchor movementAnchor = default,
        Vector2 relativeMove = default
    )
    {
        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, targetEnemy);
        bool inAttackRange = hasValidTarget && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(self, targetEnemy);

        BattleMoveIntent moveIntent;
        BattleCombatIntent combatIntent;
        BattleFacingIntent facingIntent;
        if (inAttackRange)
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.Attack;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else if (hasDesiredPosition)
        {
            moveIntent = BattleMoveIntent.MoveToPosition;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.DesiredPosition;
        }
        else if (hasValidTarget)
        {
            moveIntent = BattleMoveIntent.MoveToTarget;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.KeepCurrent;
        }

        return new BattleControlPlan(
            targetEnemy,
            targetAlly,
            desiredPosition,
            hasDesiredPosition,
            movementAnchor,
            relativeMove,
            moveIntent,
            combatIntent,
            facingIntent
        );
    }

    public static BattleControlPlan CreateTargetPlan(
        BattleUnitCombatState self,
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly = null,
        BattleAnchor movementAnchor = default,
        Vector2 relativeMove = default
    )
    {
        return CreateResolvedPlan(self, targetEnemy, targetAlly, Vector3.zero, false, movementAnchor, relativeMove);
    }

    public static BattleControlPlan CreatePositionPlan(
        BattleUnitCombatState self,
        Vector3 desiredPosition,
        BattleUnitCombatState targetEnemy = null,
        BattleUnitCombatState targetAlly = null,
        BattleAnchor movementAnchor = default,
        Vector2 relativeMove = default
    )
    {
        return CreateResolvedPlan(self, targetEnemy, targetAlly, desiredPosition, true, movementAnchor, relativeMove);
    }

    public static BattleControlPlan CreateTacticalInputPlan(
        BattleUnitCombatState self,
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        BattleAnchor movementAnchor,
        Vector2 relativeMove,
        BattleCombatCommand command
    )
    {
        targetEnemy = BattleFieldSnapshot.IsValidEnemyTarget(self, targetEnemy) ? targetEnemy : null;
        relativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        BattleCombatIntent combatIntent = ResolveCombatIntent(command);
        BattleMoveIntent moveIntent = ResolveTacticalMoveIntent(targetEnemy, relativeMove, combatIntent);
        BattleFacingIntent facingIntent = ResolveTacticalFacingIntent(targetEnemy, moveIntent, combatIntent);
        return new BattleControlPlan(
            targetEnemy,
            targetAlly,
            Vector3.zero,
            false,
            movementAnchor,
            relativeMove,
            moveIntent,
            combatIntent,
            facingIntent
        );
    }

    private static BattleCombatIntent ResolveCombatIntent(BattleCombatCommand command) =>
        command switch
        {
            BattleCombatCommand.BasicAttack => BattleCombatIntent.Attack,
            BattleCombatCommand.Skill => BattleCombatIntent.Skill,
            _ => BattleCombatIntent.None,
        };

    private static BattleMoveIntent ResolveTacticalMoveIntent(
        BattleUnitCombatState target,
        Vector2 relativeMove,
        BattleCombatIntent combatIntent
    )
    {
        if (combatIntent == BattleCombatIntent.Attack)
            return target != null ? BattleMoveIntent.MoveToTarget : BattleMoveIntent.Hold;

        if (relativeMove.sqrMagnitude > 0.0001f)
            return BattleMoveIntent.MoveByTacticalInput;

        return BattleMoveIntent.Hold;
    }

    private static BattleFacingIntent ResolveTacticalFacingIntent(
        BattleUnitCombatState target,
        BattleMoveIntent moveIntent,
        BattleCombatIntent combatIntent
    )
    {
        if (moveIntent == BattleMoveIntent.MoveByTacticalInput)
        {
            return combatIntent == BattleCombatIntent.Attack && target != null
                ? BattleFacingIntent.TargetEnemy
                : BattleFacingIntent.MoveDirection;
        }

        if (target != null)
            return BattleFacingIntent.TargetEnemy;

        return BattleFacingIntent.KeepCurrent;
    }
}
