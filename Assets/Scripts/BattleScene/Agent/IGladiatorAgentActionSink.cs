public interface IGladiatorAgentActionSink
{
    void Apply(GladiatorAgentAction action, BattleUnitCombatState target);
    void Clear();
}

public sealed class RuntimeUnitAgentActionSink : IGladiatorAgentActionSink
{
    private readonly BattleRuntimeUnit _unit;
    private readonly IBattleRuntimeUnitResolver _runtimeResolver;

    public RuntimeUnitAgentActionSink(BattleRuntimeUnit unit, IBattleRuntimeUnitResolver runtimeResolver)
    {
        _unit = unit;
        _runtimeResolver = runtimeResolver;
    }

    public void Apply(GladiatorAgentAction action, BattleUnitCombatState target)
    {
        if (_unit == null)
        {
            return;
        }

        BattleRuntimeUnit targetRuntime = _runtimeResolver != null ? _runtimeResolver.Resolve(target) : null;
        _unit.SetExternalControlInput(action.LocalMove, action.Turn, action.Command, action.Stance, targetRuntime);
    }

    public void Clear()
    {
        _unit?.ClearExternalControlInput();
    }
}
