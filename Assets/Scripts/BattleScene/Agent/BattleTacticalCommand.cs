using UnityEngine;

public enum BattlePathMode
{
    Direct = 0,
    FlankLeft = 1,
    FlankRight = 2,
    Regroup = 3,
}

public readonly struct BattleTacticalCommand
{
    public readonly BattleAnchor Anchor;
    public readonly BattlePathMode PathMode;
    public readonly Vector2 RelativeMove;
    public readonly BattleCombatCommand Command;
    public readonly BattleControlStance Stance;

    public BattleTacticalCommand(
        BattleAnchor anchor,
        BattlePathMode pathMode,
        Vector2 relativeMove,
        BattleCombatCommand command,
        BattleControlStance stance
    )
    {
        Anchor = anchor;
        PathMode = pathMode;
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        Command = command;
        Stance = stance;
    }
}
