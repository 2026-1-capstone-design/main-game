using System.Collections.Generic;
using UnityEngine;

// GladiatorAgent가 출력한 Action을 보관하고 있다가 ML Planner의 TryBuildPlan() 시 Action을 제공한다.
public sealed class BattleAgentControlBuffer
{
    private readonly Dictionary<BattleUnitCombatState, BattleAgentControlInput> _inputs =
        new Dictionary<BattleUnitCombatState, BattleAgentControlInput>();

    public void SetResolvedInput(
        BattleUnitCombatState self,
        Vector2 rawRelativeMove,
        Vector2 resolvedRelativeMove,
        GladiatorStrategy strategy,
        int anchorSlot,
        GladiatorCommand command,
        BattleUnitCombatState target
    )
    {
        if (self == null)
        {
            return;
        }

        rawRelativeMove = Vector2.ClampMagnitude(rawRelativeMove, 1f);
        resolvedRelativeMove = Vector2.ClampMagnitude(resolvedRelativeMove, 1f);

        _inputs.TryGetValue(self, out BattleAgentControlInput input);
        input.PreviousRawLocalMove = input.RawLocalMove;
        input.RawLocalMove = rawRelativeMove;
        input.ResolvedRelativeMove = resolvedRelativeMove;
        input.Strategy = strategy;
        input.AnchorSlot = Mathf.Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);
        input.Command = ToCommand(command);

        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, target);
        input.Target = hasValidTarget ? target : null;

        _inputs[self] = input;
        self.SetAgentStrategy(strategy);
    }

    public BattleAgentControlInput GetInput(BattleUnitCombatState self)
    {
        if (self == null)
        {
            return default;
        }

        return _inputs.TryGetValue(self, out BattleAgentControlInput input) ? input : default;
    }

    public BattleAgentControlInput GetInputSnapshot(BattleUnitCombatState self)
    {
        return self != null && _inputs.TryGetValue(self, out BattleAgentControlInput input) ? input : default;
    }

    public void Clear(BattleUnitCombatState self)
    {
        if (self == null)
        {
            return;
        }

        _inputs.Remove(self);
        self.SetAgentStrategy(GladiatorStrategy.Neutral);
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
            case GladiatorCommand.Withdraw:
                return BattleCombatCommand.Withdraw;
            default:
                return BattleCombatCommand.None;
        }
    }
}
