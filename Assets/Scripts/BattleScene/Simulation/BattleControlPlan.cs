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

// BattleControlPlan은 planner가 결정한 tick 단위 저수준 실행 명세다.
// 정책/전술 의도는 planner 내부에 남기고, 하위 시스템은 실행 intent와 대상만 읽고 집행한다.
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
}
