using UnityEngine;

public struct BattleAgentControlInput
{
    public Vector2 RawLocalMove;
    public Vector2 SmoothedLocalMove;
    public Vector2 PreviousRawLocalMove;
    public int Role;
    public int AnchorKind;
    public int AnchorSlot;
    public int PathMode;
    public BattleUnitCombatState AnchorTarget;
    public BattleCombatCommand Command;
    public BattleControlStance Stance;
    public BattleUnitCombatState Target;
    public bool WantsBasicAttack;
}
