using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GladiatorStateRosterView
{
    private readonly BattleStartPayload _payload;
    private readonly IReadOnlyList<BattleUnitCombatState> _states;
    private readonly List<BattleUnitCombatState> _teammates;
    private readonly List<BattleUnitCombatState> _hostiles;

    public GladiatorStateRosterView(
        BattleUnitCombatState self,
        BattleStartPayload payload,
        IReadOnlyList<BattleUnitCombatState> states
    )
    {
        _payload = payload;
        _states = states ?? Array.Empty<BattleUnitCombatState>();
        _teammates = GetSortedStates(self, includeAllies: true, excludeSelf: true);
        _hostiles = GetSortedStates(self, includeAllies: false, excludeSelf: false);
    }

    public IReadOnlyList<BattleUnitCombatState> Teammates => _teammates;

    public IReadOnlyList<BattleUnitCombatState> Hostiles => _hostiles;

    public BattleUnitCombatState ResolveHostileSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return null;
        }

        return slotIndex < _hostiles.Count ? _hostiles[slotIndex] : null;
    }

    public BattleUnitCombatState ResolveTeammateSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            return null;
        }

        return slotIndex < _teammates.Count ? _teammates[slotIndex] : null;
    }

    private List<BattleUnitCombatState> GetSortedStates(
        BattleUnitCombatState self,
        bool includeAllies,
        bool excludeSelf
    )
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleUnitCombatState State)>();
        if (self == null)
        {
            return new List<BattleUnitCombatState>();
        }

        for (int i = 0; i < _states.Count; i++)
        {
            BattleUnitCombatState state = _states[i];
            if (state == null)
            {
                continue;
            }

            if (excludeSelf && state == self)
            {
                continue;
            }

            bool matchesPerspective = includeAllies ? state.TeamId == self.TeamId : state.TeamId != self.TeamId;
            if (!matchesPerspective)
            {
                continue;
            }

            sorted.Add((ResolveSortIndex(state), state.UnitNumber, state));
        }

        sorted.Sort(
            (left, right) =>
            {
                int byIndex = left.SortIndex.CompareTo(right.SortIndex);
                return byIndex != 0 ? byIndex : left.UnitNumber.CompareTo(right.UnitNumber);
            }
        );

        var result = new List<BattleUnitCombatState>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            result.Add(sorted[i].State);
        }

        return result;
    }

    private int ResolveSortIndex(BattleUnitCombatState state)
    {
        if (_payload != null && _payload.TryGetTeamLocalUnitIndex(state.TeamId, state.UnitNumber, out int localIndex))
        {
            return localIndex;
        }

        return state != null ? state.UnitNumber : int.MaxValue;
    }
}
