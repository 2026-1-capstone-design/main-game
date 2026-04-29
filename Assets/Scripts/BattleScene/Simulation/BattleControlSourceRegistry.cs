using System.Collections.Generic;

public sealed class BattleControlSourceRegistry
{
    private readonly Dictionary<BattleUnitCombatState, IBattleUnitControlSource> _sources =
        new Dictionary<BattleUnitCombatState, IBattleUnitControlSource>();

    public void Set(BattleUnitCombatState state, IBattleUnitControlSource source)
    {
        if (state == null)
        {
            return;
        }

        if (source == null)
        {
            _sources.Remove(state);
            return;
        }

        _sources[state] = source;
    }

    public bool TryGet(BattleUnitCombatState state, out IBattleUnitControlSource source)
    {
        if (state == null)
        {
            source = null;
            return false;
        }

        return _sources.TryGetValue(state, out source);
    }

    public void Clear()
    {
        _sources.Clear();
    }
}
