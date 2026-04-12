using System.Collections.Generic;
using UnityEngine;

// ML-Agents에서 받은 원시 입력을 저장하고, smoothed 입력을 계산한다.
public sealed class BattleAgentControlBuffer
{
    private static readonly bool EnableMoveInputSmoothing = false;
    private const float MoveInputChangePerSecond = 8f;

    private readonly Dictionary<BattleUnitCombatState, BattleAgentControlInput> _inputs =
        new Dictionary<BattleUnitCombatState, BattleAgentControlInput>();

    public void SetRawInput(
        BattleUnitCombatState self,
        Vector2 rawRelativeMove,
        GladiatorActionRole role,
        GladiatorFightMode fightMode,
        GladiatorAnchorKind anchorKind,
        int anchorSlot,
        GladiatorCommand command,
        BattleUnitCombatState target
    )
    {
        if (self == null)
        {
            return;
        }

        if (rawRelativeMove.sqrMagnitude > 1f)
        {
            rawRelativeMove.Normalize();
        }

        _inputs.TryGetValue(self, out BattleAgentControlInput input);
        input.PreviousRawLocalMove = input.RawLocalMove;
        input.RawLocalMove = rawRelativeMove;
        input.Role = role;
        input.FightMode = fightMode;
        input.AnchorKind = anchorKind;
        input.AnchorSlot = anchorSlot;
        input.Command = ToCommand(command);

        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, target);
        input.AnchorTarget = target;
        input.Target = hasValidTarget ? target : null;
        input.WantsBasicAttack = input.Command == BattleCombatCommand.BasicAttack && hasValidTarget;

        _inputs[self] = input;
        self.SetAgentFightMode(fightMode);
    }

    public BattleAgentControlInput GetSmoothedInput(BattleUnitCombatState self, float tickDeltaTime)
    {
        if (self == null)
        {
            return default;
        }

        _inputs.TryGetValue(self, out BattleAgentControlInput input);
        if (!EnableMoveInputSmoothing)
        {
            input.SmoothedLocalMove = input.RawLocalMove;
            _inputs[self] = input;
            return input;
        }

        float moveStep = MoveInputChangePerSecond * Mathf.Max(0f, tickDeltaTime);

        Vector2 smoothed = input.SmoothedLocalMove;
        smoothed.x = Mathf.MoveTowards(smoothed.x, input.RawLocalMove.x, moveStep);
        smoothed.y = Mathf.MoveTowards(smoothed.y, input.RawLocalMove.y, moveStep);
        if (smoothed.sqrMagnitude > 1f)
        {
            smoothed.Normalize();
        }

        input.SmoothedLocalMove = smoothed;
        _inputs[self] = input;
        return input;
    }

    public BattleAgentControlInput GetInputSnapshot(BattleUnitCombatState self)
    {
        return self != null && _inputs.TryGetValue(self, out BattleAgentControlInput input) ? input : default;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command)
    {
        if (self == null || !_inputs.TryGetValue(self, out BattleAgentControlInput input))
        {
            return;
        }

        if (command == BattleCombatCommand.BasicAttack)
        {
            input.WantsBasicAttack = false;
        }
        if (input.Command == command)
        {
            input.Command = BattleCombatCommand.None;
        }

        _inputs[self] = input;
    }

    public void Clear(BattleUnitCombatState self)
    {
        if (self == null)
        {
            return;
        }

        _inputs.Remove(self);
        self.SetAgentFightMode(GladiatorFightMode.Neutral);
        self.SetPlannedTargets(null, null);
    }

    public void ClearAll()
    {
        _inputs.Clear();
    }

    private static BattleCombatCommand ToCommand(GladiatorCommand command)
    {
        switch (command)
        {
            case GladiatorCommand.Attack:
                return BattleCombatCommand.BasicAttack;
            default:
                return BattleCombatCommand.None;
        }
    }
}
