using UnityEngine;

public readonly struct GladiatorPolicyAction
{
    public readonly Vector2 RelativeMove;
    public readonly int AnchorKind;
    public readonly int AnchorSlot;
    public readonly int PathMode;
    public readonly int Command;
    public readonly int Stance;

    public GladiatorPolicyAction(
        Vector2 relativeMove,
        int anchorKind,
        int anchorSlot,
        int pathMode,
        int command,
        int stance
    )
    {
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        AnchorKind = anchorKind;
        AnchorSlot = anchorSlot;
        PathMode = pathMode;
        Command = command;
        Stance = stance;
    }

    public bool WantsBasicAttack => Command == GladiatorActionSchema.CommandBasicAttack;

    public GladiatorPolicyAction WithCommand(int command) =>
        new GladiatorPolicyAction(RelativeMove, AnchorKind, AnchorSlot, PathMode, command, Stance);
}
