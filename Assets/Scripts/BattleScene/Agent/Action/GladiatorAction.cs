using UnityEngine;

// GladiatorAgent의 정책 출력 action을 시스템 내부에서 공유하는 값 타입이다.
// ActionBuffers의 raw branch 값은 이 contract로 정규화된 뒤 reward, tactical context, sink, metrics가 함께 해석한다.
public readonly struct GladiatorAction
{
    public readonly Vector2 RelativeMove;
    public readonly GladiatorActionRole Role;
    public readonly GladiatorFightMode FightMode;
    public readonly GladiatorAnchorKind AnchorKind;
    public readonly int AnchorSlot;
    public readonly GladiatorCommand Command;

    public GladiatorAction(
        Vector2 relativeMove,
        GladiatorActionRole role,
        GladiatorFightMode fightMode,
        GladiatorAnchorKind anchorKind,
        int anchorSlot,
        GladiatorCommand command
    )
    {
        RelativeMove = Vector2.ClampMagnitude(relativeMove, 1f);
        Role = role;
        FightMode = fightMode;
        AnchorKind = anchorKind;
        AnchorSlot = anchorSlot;
        Command = command;
    }

    public bool WantsBasicAttack => Command == GladiatorCommand.Attack;

    public GladiatorAction WithCommand(GladiatorCommand command) =>
        new GladiatorAction(RelativeMove, Role, FightMode, AnchorKind, AnchorSlot, command);

    public GladiatorAction WithAnchor(GladiatorAnchorKind anchorKind, int anchorSlot) =>
        new GladiatorAction(RelativeMove, Role, FightMode, anchorKind, anchorSlot, Command);
}
