using UnityEngine;

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
