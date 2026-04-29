public sealed class MlAgentControlSource : IBattleUnitControlSource
{
    private readonly BattleAgentControlBuffer _buffer;

    public MlAgentControlSource(BattleAgentControlBuffer buffer)
    {
        _buffer = buffer;
    }

    public bool TryBuildPlan(
        BattleUnitCombatState self,
        BattleFieldSnapshot snapshot,
        float tickDeltaTime,
        out BattleControlPlan plan
    )
    {
        BattleAgentControlInput input = _buffer != null ? _buffer.GetSmoothedInput(self, tickDeltaTime) : default;
        plan = BattleControlPlan.FromAgentInput(self, input);
        return self != null;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command)
    {
        _buffer?.ConsumeCommand(self, command);
    }
}
