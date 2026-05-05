using System.Collections.Generic;
using UnityEngine;

public sealed class BattleAgentControlBuffer
{
    private const float MoveInputChangePerSecond = 8f;

    private readonly Dictionary<BattleUnitCombatState, BattleAgentControlInput> _inputs =
        new Dictionary<BattleUnitCombatState, BattleAgentControlInput>();

    public void SetRawInput(
        BattleUnitCombatState self,
        Vector2 rawRelativeMove,
        int anchorKind,
        int anchorSlot,
        int pathMode,
        int command,
        int stance,
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
        input.AnchorKind = anchorKind;
        input.AnchorSlot = anchorSlot;
        input.PathMode = pathMode;
        input.Command = ToCommand(command);
        input.Stance = ToStance(stance);

        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, target);
        input.AnchorTarget = target;
        input.Target = hasValidTarget ? target : null;
        input.WantsBasicAttack = input.Command == BattleCombatCommand.BasicAttack && hasValidTarget;

        _inputs[self] = input;
        self.SetPlannedTargets(input.Target, input.AnchorKind == GladiatorActionSchema.AnchorKindAlly ? input.AnchorTarget : null);
    }

    public BattleAgentControlInput GetSmoothedInput(BattleUnitCombatState self, float tickDeltaTime)
    {
        if (self == null)
        {
            return default;
        }

        _inputs.TryGetValue(self, out BattleAgentControlInput input);
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
        self.SetPlannedTargets(null, null);
    }

    public void ClearAll()
    {
        _inputs.Clear();
    }

    private static BattleCombatCommand ToCommand(int command)
    {
        switch (command)
        {
            case GladiatorActionSchema.CommandBasicAttack:
                return BattleCombatCommand.BasicAttack;
            default:
                return BattleCombatCommand.None;
        }
    }

    private static BattleControlStance ToStance(int stance)
    {
        switch (stance)
        {
            case GladiatorActionSchema.StancePressure:
                return BattleControlStance.Pressure;
            case GladiatorActionSchema.StanceKeepRange:
                return BattleControlStance.KeepRange;
            default:
                return BattleControlStance.Neutral;
        }
    }
}
