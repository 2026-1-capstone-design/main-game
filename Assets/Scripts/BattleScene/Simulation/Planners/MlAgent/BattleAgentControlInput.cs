using UnityEngine;

public struct BattleAgentControlInput
{
    public Vector2 RawLocalMove;
    public Vector2 PreviousRawLocalMove;
    public Vector2 ResolvedRelativeMove;
    public GladiatorStrategy Strategy;
    public int AnchorSlot;
    public BattleCombatCommand Command;
    public BattleUnitCombatState Target;
}
