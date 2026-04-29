using UnityEngine;

public struct BattleAgentControlInput
{
    public Vector2 RawLocalMove;
    public Vector2 SmoothedLocalMove;
    public Vector2 PreviousRawLocalMove;
    public float RawTurn;
    public float SmoothedTurn;
    public float PreviousRawTurn;
    public BattleCombatCommand Command;
    public BattleControlStance Stance;
    public BattleUnitCombatState Target;
    public bool WantsBasicAttack;
    public bool WantsSkill;
}
