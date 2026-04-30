public interface IGladiatorAgentActionSink
{
    void Apply(GladiatorAgentAction action, BattleUnitCombatState target);
    void Clear();
}

public sealed class RuntimeUnitAgentActionSink : IGladiatorAgentActionSink
{
    private readonly BattleRuntimeUnit _unit;
    private readonly BattleAgentControlBuffer _controlBuffer;

    public RuntimeUnitAgentActionSink(BattleRuntimeUnit unit, BattleAgentControlBuffer controlBuffer = null)
    {
        _unit = unit;
        _controlBuffer = controlBuffer;
    }

    public void Apply(GladiatorAgentAction action, BattleUnitCombatState target)
    {
        if (_unit == null)
        {
            return;
        }

        _controlBuffer?.SetRawInput(_unit.State, action.LocalMove, action.Turn, action.Command, action.Stance, target);
    }

    public void Clear()
    {
        if (_unit != null)
        {
            _controlBuffer?.Clear(_unit.State);
        }
    }
}
