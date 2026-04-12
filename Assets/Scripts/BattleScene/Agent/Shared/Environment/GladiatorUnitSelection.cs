using System.Collections.Generic;

public static class GladiatorUnitSelection
{
    public static List<BattleRuntimeUnit> GetSortedUnitsForTeam(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BattleTeamId teamId,
        BattleRosterProjection projection
    )
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleRuntimeUnit Unit)>();
        if (runtimeUnits == null)
        {
            return new List<BattleRuntimeUnit>();
        }

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null || unit.TeamId != teamId)
            {
                continue;
            }

            sorted.Add((ResolveSortIndex(unit, projection), unit.UnitNumber, unit));
        }

        sorted.Sort(
            (left, right) =>
            {
                int byIndex = left.SortIndex.CompareTo(right.SortIndex);
                return byIndex != 0 ? byIndex : left.UnitNumber.CompareTo(right.UnitNumber);
            }
        );

        var result = new List<BattleRuntimeUnit>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            result.Add(sorted[i].Unit);
        }

        return result;
    }

    private static int ResolveSortIndex(BattleRuntimeUnit unit, BattleRosterProjection projection)
    {
        if (
            projection != null
            && projection.IsPlayerUnit(unit)
            && projection.TryGetPlayerIndex(unit, out int playerIndex)
        )
        {
            return playerIndex;
        }

        if (projection != null && projection.TryGetHostileIndex(unit, out int hostileIndex))
        {
            return hostileIndex;
        }

        return unit != null ? unit.UnitNumber : int.MaxValue;
    }
}
