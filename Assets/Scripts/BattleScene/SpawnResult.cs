using System;
using System.Collections.Generic;

public sealed class SpawnResult
{
    private readonly IReadOnlyList<BattleRuntimeUnit> _units;
    private readonly IReadOnlyDictionary<int, BattleRuntimeUnit> _byUnitNumber;

    public IReadOnlyList<BattleRuntimeUnit> Units => _units;
    public IReadOnlyDictionary<int, BattleRuntimeUnit> ByUnitNumber => _byUnitNumber;

    public SpawnResult(List<BattleRuntimeUnit> units)
    {
        if (units == null)
            throw new ArgumentNullException(nameof(units));

        var copiedUnits = new List<BattleRuntimeUnit>(units.Count);
        var indexedUnits = new Dictionary<int, BattleRuntimeUnit>(units.Count);

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null)
                continue;

            copiedUnits.Add(unit);
            indexedUnits[unit.UnitNumber] = unit;
        }

        _units = copiedUnits.AsReadOnly();
        _byUnitNumber = indexedUnits;
    }
}
