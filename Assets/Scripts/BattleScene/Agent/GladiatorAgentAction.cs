using UnityEngine;

public readonly struct GladiatorAgentAction
{
    public readonly Vector2 LocalMove;
    public readonly float Turn;
    public readonly int Command;
    public readonly int TargetSlot;
    public readonly int Stance;

    public GladiatorAgentAction(Vector2 localMove, float turn, int command, int targetSlot, int stance)
    {
        LocalMove = localMove;
        Turn = turn;
        Command = command;
        TargetSlot = targetSlot;
        Stance = stance;
    }

    public bool WantsBasicAttack => Command == GladiatorActionSchema.CommandBasicAttack;
    public bool WantsSkill => Command == GladiatorActionSchema.CommandSkill;
    public bool IsSpacingMove => LocalMove.y < -0.2f || Stance == GladiatorActionSchema.StanceKeepRange;

    public GladiatorAgentAction WithCommand(int command) =>
        new GladiatorAgentAction(LocalMove, Turn, command, TargetSlot, Stance);
}
