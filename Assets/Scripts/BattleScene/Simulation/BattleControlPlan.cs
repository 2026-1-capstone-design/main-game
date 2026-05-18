using System;
using UnityEngine;

public enum BattleMoveIntent
{
    Hold = 0,
    MoveToTarget = 1,
    MoveToAbsolutePosition = 2,
    MoveToRelativePosition = 3,
    MoveToAbsoluteDirection = 4,
    MoveToRelativeDirection = 5,

    // deprecated
    [Obsolete]
    MoveByTacticalInput = 6,

    [Obsolete]
    MoveByWithdrawInput = 7,
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

public readonly struct BattleMove
{
    public readonly BattleMoveIntent Intent;
    public readonly Vector3 Position;
    public readonly BattleUnitCombatState Target;
    public readonly Vector3 Direction;

    private BattleMove(BattleMoveIntent intent, Vector3 position, BattleUnitCombatState target, Vector3 direction)
    {
        Intent = intent;
        Position = position;
        Target = target;
        Direction = direction;
    }

    public static BattleMove Hold() => new BattleMove(BattleMoveIntent.Hold, default, null, default);

    public static bool IsHold(BattleMove plan) => plan.Intent == BattleMoveIntent.Hold;

    public static BattleMove ToAbsolutePosition(Vector3 position) =>
        new BattleMove(BattleMoveIntent.MoveToAbsolutePosition, position, null, default);

    public static bool IsMoveToAbsolutePosition(BattleMove plan) =>
        plan.Intent == BattleMoveIntent.MoveToAbsolutePosition;

    public static BattleMove ToRelativePosition(BattleUnitCombatState target, Vector2 relativePosition) =>
        new BattleMove(
            BattleMoveIntent.MoveToRelativePosition,
            default,
            target,
            new Vector3(relativePosition.x, 0f, relativePosition.y)
        );

    public static bool IsMoveToRelativePosition(BattleMove plan) =>
        plan.Intent == BattleMoveIntent.MoveToRelativePosition && plan.Target != null;

    public static BattleMove ToTarget(BattleUnitCombatState target) =>
        new BattleMove(BattleMoveIntent.MoveToTarget, default, target, default);

    public static BattleMove ToTarget(BattleUnitCombatState target, Vector2 relativeDirection) =>
        new BattleMove(
            BattleMoveIntent.MoveToTarget,
            default,
            target,
            new Vector3(relativeDirection.x, 0f, relativeDirection.y).normalized
        );

    public static bool IsMoveToTarget(BattleMove plan) =>
        plan.Intent == BattleMoveIntent.MoveToTarget && plan.Target != null;

    public static BattleMove ToAbsoluteDirection(Vector3 direction) =>
        new BattleMove(BattleMoveIntent.MoveToAbsoluteDirection, default, null, direction.normalized);

    public static bool IsMoveToAbsoluteDirection(BattleMove plan) =>
        plan.Intent == BattleMoveIntent.MoveToAbsoluteDirection;

    public static BattleMove ToRelativeDirection(BattleUnitCombatState target, Vector2 relativeDirection) =>
        new BattleMove(
            BattleMoveIntent.MoveToRelativeDirection,
            default,
            target,
            new Vector3(relativeDirection.x, 0f, relativeDirection.y).normalized
        );

    public static bool IsMoveToRelativeDirection(BattleMove plan) =>
        plan.Intent == BattleMoveIntent.MoveToRelativeDirection && plan.Target != null;
}

// BattleControlPlan은 planner가 결정한 tick 단위 저수준 실행 명세다.
// 정책/전술 의도는 planner 내부에 남기고, 하위 시스템은 실행 intent와 대상만 읽고 집행한다.
public readonly struct BattleControlPlan
{
    public readonly BattleUnitCombatState TargetEnemy;
    public readonly BattleUnitCombatState TargetAlly;
    public readonly BattleMove Move;

    public readonly BattleCombatIntent CombatIntent;
    public readonly BattleFacingIntent FacingIntent;

    public BattleControlPlan(
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        BattleMove move,
        BattleCombatIntent combatIntent,
        BattleFacingIntent facingIntent
    )
    {
        TargetEnemy = targetEnemy;
        TargetAlly = targetAlly;
        Move = move;
        CombatIntent = combatIntent;
        FacingIntent = facingIntent;
    }
}
