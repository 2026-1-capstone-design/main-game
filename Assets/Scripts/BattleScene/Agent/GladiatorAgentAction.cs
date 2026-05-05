using UnityEngine;

public readonly struct GladiatorAgentAction
{
    public readonly Vector2 WorldMove;
    public readonly int Command;
    public readonly int TargetSlot;
    public readonly int Stance;

    public GladiatorAgentAction(Vector2 worldMove, int command, int targetSlot, int stance)
    {
        WorldMove = worldMove;
        Command = command;
        TargetSlot = targetSlot;
        Stance = stance;
    }

    public bool WantsBasicAttack => Command == GladiatorActionSchema.CommandBasicAttack;
    public bool IsSpacingMove => Stance == GladiatorActionSchema.StanceKeepRange;

    public GladiatorAgentAction WithCommand(int command) =>
        new GladiatorAgentAction(WorldMove, command, TargetSlot, Stance);
}
