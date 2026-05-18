using System;
using System.Collections.Generic;

public sealed class BattleRuntimeUnitResolver
{
    private readonly IReadOnlyList<BattleRuntimeUnit> _runtimeUnits;

    public BattleRuntimeUnitResolver(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        _runtimeUnits = runtimeUnits ?? Array.Empty<BattleRuntimeUnit>();
    }

    public BattleRuntimeUnit Resolve(BattleUnitCombatState state)
    {
        if (state == null)
        {
            return null;
        }

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit != null && unit.State == state)
            {
                return unit;
            }
        }

        return null;
    }
}
