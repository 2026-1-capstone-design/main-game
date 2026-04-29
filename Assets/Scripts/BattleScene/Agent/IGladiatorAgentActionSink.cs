public interface IGladiatorAgentActionSink
{
    void Apply(GladiatorAgentAction action, BattleUnitCombatState target);
    void Clear();
}

public sealed class RuntimeUnitAgentActionSink : IGladiatorAgentActionSink
{
    private readonly BattleRuntimeUnit _unit;
    private readonly GladiatorRosterView _rosterView;

    public RuntimeUnitAgentActionSink(BattleRuntimeUnit unit, GladiatorRosterView rosterView)
    {
        _unit = unit;
        _rosterView = rosterView;
    }

    public void Apply(GladiatorAgentAction action, BattleUnitCombatState target)
    {
        if (_unit == null)
        {
            return;
        }

        BattleRuntimeUnit targetRuntime = _rosterView != null ? _rosterView.ResolveRuntimeUnit(target) : null;
        _unit.SetExternalControlInput(action.LocalMove, action.Turn, action.Command, action.Stance, targetRuntime);
    }

    public void Clear()
    {
        _unit?.ClearExternalControlInput();
    }
}
