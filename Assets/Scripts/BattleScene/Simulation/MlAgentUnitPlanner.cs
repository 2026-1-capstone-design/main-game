// ML-Agents 입력을 BattleAgentControlBuffer에서 읽어 최종 BattleControlPlan으로 변환한다.
public sealed class MlAgentUnitPlanner : IBattleUnitPlanner
{
    private readonly BattleAgentControlBuffer _buffer;

    public MlAgentUnitPlanner(BattleAgentControlBuffer buffer)
    {
        _buffer = buffer;
    }

    public bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan)
    {
        BattleAgentControlInput input =
            _buffer != null ? _buffer.GetSmoothedInput(self, context.TickDeltaTime) : default;
        plan = BattleControlPlan.FromAgentInput(self, input);
        return self != null;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command)
    {
        _buffer?.ConsumeCommand(self, command);
    }
}
