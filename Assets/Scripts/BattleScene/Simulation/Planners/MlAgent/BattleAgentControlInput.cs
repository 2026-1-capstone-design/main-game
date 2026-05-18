using UnityEngine;

public struct BattleAgentControlInput
{
    public Vector2 RawLocalMove;
    public Vector2 PreviousRawLocalMove;
    public GladiatorActionRole Role;
    public GladiatorFightMode FightMode;
    public GladiatorAnchorKind AnchorKind;
    public int AnchorSlot;
    public BattleUnitCombatState AnchorTarget;
    public BattleCombatCommand Command;
    public BattleUnitCombatState Target;
}
