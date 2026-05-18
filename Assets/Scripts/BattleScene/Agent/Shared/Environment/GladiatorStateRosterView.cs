using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GladiatorStateRosterView
{
    private readonly BattleStartPayload _payload;
    private readonly IReadOnlyList<BattleUnitCombatState> _states;
    private readonly List<BattleUnitCombatState> _teammates;
    private readonly List<BattleUnitCombatState> _hostiles;
    private readonly bool _useSelfRandomizedSlots;

    public GladiatorStateRosterView(
        BattleUnitCombatState self,
        BattleStartPayload payload,
        IReadOnlyList<BattleUnitCombatState> states,
        bool useSelfRandomizedSlots = false
    )
    {
        _payload = payload;
        _states = states ?? Array.Empty<BattleUnitCombatState>();
        _useSelfRandomizedSlots = useSelfRandomizedSlots;
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
        if (self == null)
        {
            return new List<BattleUnitCombatState>();
        }

        if (_payload == null)
        {
            return GetSortedStatesFallback(self, includeAllies, excludeSelf);
        }

        return includeAllies ? BuildTeammateSlots(self, excludeSelf) : BuildHostileSlots(self);
    }

    private List<BattleUnitCombatState> BuildHostileSlots(BattleUnitCombatState self)
    {
        var result = new List<BattleUnitCombatState>(BattleTeamConstants.MaxUnitsPerTeam);
        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            result.Add(null);
        }

        for (int i = 0; i < _states.Count; i++)
        {
            BattleUnitCombatState state = _states[i];
            if (state == null || state.TeamId == self.TeamId)
            {
                continue;
            }

            int slotIndex = ResolveSlotIndex(state);
            if (slotIndex >= 0 && slotIndex < result.Count)
            {
                result[slotIndex] = state;
            }
        }

        ApplySelfRandomizedSlotOrder(result, self, salt: 1);
        return result;
    }

    private List<BattleUnitCombatState> BuildTeammateSlots(BattleUnitCombatState self, bool excludeSelf)
    {
        var result = new List<BattleUnitCombatState>(BattleTeamConstants.MaxUnitsPerTeam - (excludeSelf ? 1 : 0));
        for (int i = 0; i < result.Capacity; i++)
        {
            result.Add(null);
        }

        int selfSlotIndex = ResolveSlotIndex(self);
        if (selfSlotIndex < 0)
        {
            return GetSortedStatesFallback(self, includeAllies: true, excludeSelf);
        }

        for (int i = 0; i < _states.Count; i++)
        {
            BattleUnitCombatState state = _states[i];
            if (state == null || state.TeamId != self.TeamId)
            {
                continue;
            }

            if (excludeSelf && state == self)
            {
                continue;
            }

            int slotIndex = ResolveSlotIndex(state);
            int teammateIndex = slotIndex;
            if (excludeSelf)
            {
                if (slotIndex == selfSlotIndex)
                {
                    continue;
                }

                teammateIndex = slotIndex > selfSlotIndex ? slotIndex - 1 : slotIndex;
            }

            if (teammateIndex >= 0 && teammateIndex < result.Count)
            {
                result[teammateIndex] = state;
            }
        }

        ApplySelfRandomizedSlotOrder(result, self, salt: 2);
        return result;
    }

    private List<BattleUnitCombatState> GetSortedStatesFallback(
        BattleUnitCombatState self,
        bool includeAllies,
        bool excludeSelf
    )
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleUnitCombatState State)>();
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

    private int ResolveSlotIndex(BattleUnitCombatState state)
    {
        if (
            _payload != null
            && state != null
            && _payload.TryGetTeamSlotIndex(state.TeamId, state.UnitNumber, out int slotIndex)
        )
        {
            return slotIndex;
        }

        return ResolveSortIndex(state);
    }

    private int ResolveSortIndex(BattleUnitCombatState state)
    {
        if (_payload != null && _payload.TryGetTeamLocalUnitIndex(state.TeamId, state.UnitNumber, out int localIndex))
        {
            return localIndex;
        }

        return state != null ? state.UnitNumber : int.MaxValue;
    }

    private void ApplySelfRandomizedSlotOrder(List<BattleUnitCombatState> slots, BattleUnitCombatState self, int salt)
    {
        if (!_useSelfRandomizedSlots || _payload == null || self == null || slots == null || slots.Count <= 1)
        {
            return;
        }

        // Agent observations/actions should not share one team-wide slot randomization. Derive a stable
        // per-episode, per-unit permutation without touching UnityEngine.Random's global state.
        int[] permutation = BuildPermutation(slots.Count, BuildSelfRandomSeed(self, salt));
        BattleUnitCombatState[] copy = slots.ToArray();
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i] = copy[permutation[i]];
        }
    }

    private int BuildSelfRandomSeed(BattleUnitCombatState self, int salt)
    {
        unchecked
        {
            int seed = _payload.BattleSeed;
            seed = (seed * 397) ^ self.TeamId.Value;
            seed = (seed * 397) ^ self.UnitNumber;
            seed = (seed * 397) ^ salt;
            return seed & 0x7fffffff;
        }
    }

    private static int[] BuildPermutation(int count, int seed)
    {
        int[] permutation = new int[count];
        for (int i = 0; i < count; i++)
        {
            permutation[i] = i;
        }

        var random = new System.Random(seed);
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
        }

        return permutation;
    }
}
