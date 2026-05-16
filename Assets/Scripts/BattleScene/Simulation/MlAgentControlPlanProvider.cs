// ML-Agents 입력 버퍼를 중립 BattleControlPlan으로 변환하는 provider다.
public sealed class MlAgentControlPlanProvider : IBattleControlPlanProvider
{
    private readonly BattleAgentControlBuffer _buffer;

    public MlAgentControlPlanProvider(BattleAgentControlBuffer buffer)
    {
        _buffer = buffer;
    }

    public bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan)
    {
        BattleAgentControlInput input = _buffer != null ? _buffer.GetInput(self) : default;
        BattleUnitCombatState target = BattleFieldSnapshot.IsValidEnemyTarget(self, input.Target) ? input.Target : null;
        BattleUnitCombatState anchorTarget = input.AnchorTarget;
        BattleAnchor anchor = BuildAnchor(self, input, anchorTarget);
        plan = BattleControlPlan.CreateTacticalInputPlan(
            self,
            target,
            input.AnchorKind == GladiatorAnchorKind.Ally ? anchorTarget : null,
            anchor,
            input.RawLocalMove,
            input.Command
        );
        return self != null;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command)
    {
        _buffer?.ConsumeCommand(self, command);
    }

    private static BattleAnchor BuildAnchor(
        BattleUnitCombatState self,
        BattleAgentControlInput input,
        BattleUnitCombatState target
    )
    {
        BattleAnchorKind kind = input.AnchorKind switch
        {
            GladiatorAnchorKind.Ally => BattleAnchorKind.Ally,
            GladiatorAnchorKind.TeamCenter => BattleAnchorKind.TeamCenter,
            _ => BattleAnchorKind.Enemy,
        };

        return new BattleAnchor(
            kind,
            input.AnchorSlot,
            target,
            target != null ? target.Position : (self != null ? self.Position : UnityEngine.Vector3.zero),
            target != null
        );
    }
}
