using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GladiatorRosterView
{
    private readonly BattleStartPayload _payload;
    private readonly IBattleRosterProjection _projection;
    private readonly IReadOnlyList<BattleRuntimeUnit> _runtimeUnits;
    private readonly List<BattleRuntimeUnit> _cachedTeammates;
    private readonly List<BattleRuntimeUnit> _cachedHostiles;

    public GladiatorRosterView(
        BattleRuntimeUnit self,
        BattleStartPayload payload,
        IBattleRosterProjection projection,
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits
    )
    {
        _payload = payload;
        _projection = projection;
        _runtimeUnits = runtimeUnits ?? Array.Empty<BattleRuntimeUnit>();
        _cachedTeammates = GetSortedUnits(self, includeAllies: true, excludeSelf: true);
        _cachedHostiles = GetSortedUnits(self, includeAllies: false, excludeSelf: false);
    }

    public IReadOnlyList<BattleRuntimeUnit> GetSortedTeammates(BattleRuntimeUnit self) => _cachedTeammates;

    public IReadOnlyList<BattleRuntimeUnit> GetSortedHostiles(BattleRuntimeUnit self) => _cachedHostiles;

    public BattleRuntimeUnit ResolveHostileSlot(BattleRuntimeUnit self, int slotIndex)
    {
        if (slotIndex < 0)
        {
            return null;
        }

        IReadOnlyList<BattleRuntimeUnit> hostiles = GetSortedHostiles(self);
        return slotIndex < hostiles.Count ? hostiles[slotIndex] : null;
    }

    public float GetDistanceToNearestHostile(BattleRuntimeUnit self)
    {
        BattleRuntimeUnit nearest = GetNearestHostile(self);
        if (nearest == null || self == null)
        {
            return float.MaxValue;
        }

        Vector3 delta = nearest.Position - self.Position;
        delta.y = 0f;
        return delta.magnitude;
    }

    public float GetAverageDistanceToHostiles(BattleRuntimeUnit self)
    {
        if (self == null)
        {
            return float.MaxValue;
        }

        float totalDistance = 0f;
        int count = 0;
        IReadOnlyList<BattleRuntimeUnit> hostiles = GetSortedHostiles(self);
        for (int i = 0; i < hostiles.Count; i++)
        {
            BattleRuntimeUnit hostile = hostiles[i];
            if (hostile == null || hostile.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = hostile.Position - self.Position;
            delta.y = 0f;
            totalDistance += delta.magnitude;
            count++;
        }

        return count > 0 ? totalDistance / count : float.MaxValue;
    }

    private BattleRuntimeUnit GetNearestHostile(BattleRuntimeUnit self)
    {
        if (self == null)
        {
            return null;
        }

        BattleRuntimeUnit nearest = null;
        float minSqrDistance = float.MaxValue;
        IReadOnlyList<BattleRuntimeUnit> hostiles = GetSortedHostiles(self);
        for (int i = 0; i < hostiles.Count; i++)
        {
            BattleRuntimeUnit hostile = hostiles[i];
            if (hostile == null || hostile.IsCombatDisabled)
            {
                continue;
            }

            float sqrDistance = (hostile.Position - self.Position).sqrMagnitude;
            if (sqrDistance < minSqrDistance)
            {
                minSqrDistance = sqrDistance;
                nearest = hostile;
            }
        }

        return nearest;
    }

    private List<BattleRuntimeUnit> GetSortedUnits(BattleRuntimeUnit self, bool includeAllies, bool excludeSelf)
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleRuntimeUnit Unit)>();
        if (self == null)
        {
            return new List<BattleRuntimeUnit>();
        }

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            if (excludeSelf && unit == self)
            {
                continue;
            }

            bool matchesPerspective = includeAllies ? unit.TeamId == self.TeamId : unit.TeamId != self.TeamId;
            if (!matchesPerspective)
            {
                continue;
            }

            sorted.Add((ResolveSortIndex(unit), unit.UnitNumber, unit));
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

    private int ResolveSortIndex(BattleRuntimeUnit unit)
    {
        if (_payload != null && _payload.TryGetTeamLocalUnitIndex(unit.TeamId, unit.UnitNumber, out int localIndex))
        {
            return localIndex;
        }

        if (_projection != null)
        {
            if (_projection.TryGetPlayerIndex(unit, out int playerIndex))
            {
                return playerIndex;
            }

            if (_projection.TryGetHostileIndex(unit, out int hostileIndex))
            {
                return hostileIndex;
            }
        }

        return unit != null ? unit.UnitNumber : int.MaxValue;
    }
}
