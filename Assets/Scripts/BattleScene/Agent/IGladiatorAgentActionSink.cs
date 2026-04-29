public interface IGladiatorAgentActionSink
{
    void Apply(GladiatorAgentAction action, BattleUnitCombatState target);
    void Clear();
}

public sealed class RuntimeUnitAgentActionSink : IGladiatorAgentActionSink
{
    private readonly BattleRuntimeUnit _unit;
    private readonly IBattleRuntimeUnitResolver _runtimeResolver;
    private readonly BattleAgentControlBuffer _controlBuffer;

    public RuntimeUnitAgentActionSink(
        BattleRuntimeUnit unit,
        IBattleRuntimeUnitResolver runtimeResolver,
        BattleAgentControlBuffer controlBuffer = null
    )
    {
        _unit = unit;
        _runtimeResolver = runtimeResolver;
        _controlBuffer = controlBuffer;
    }

    public void Apply(GladiatorAgentAction action, BattleUnitCombatState target)
    {
        if (_unit == null)
        {
            return;
        }

        BattleRuntimeUnit targetRuntime = _runtimeResolver != null ? _runtimeResolver.Resolve(target) : null;
        _controlBuffer?.SetRawInput(_unit.State, action.LocalMove, action.Turn, action.Command, action.Stance, target);
        _unit.SetAgentControlInput(action.LocalMove, action.Turn, action.Command, action.Stance, targetRuntime);
    }

    public void Clear()
    {
        if (_unit != null)
        {
            _controlBuffer?.Clear(_unit.State);
        }

        _unit?.ClearAgentControlInput();
    }
}
