using UnityEngine;

public readonly struct BattleTacticalCommand
{
    public readonly BattleAnchor Anchor;
    public readonly Vector2 RelativeMove;
    public readonly BattleCombatCommand Command;
    public readonly GladiatorFightMode FightMode;

    public BattleTacticalCommand(
        BattleAnchor anchor,
        Vector2 relativeMove,
        BattleCombatCommand command,
        GladiatorFightMode fightMode
    )
    {
        Anchor = anchor;
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        Command = command;
        FightMode = fightMode;
    }
}
