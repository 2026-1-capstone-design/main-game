using System.Collections.Generic;

// 유닛별 controller priority stack을 관리한다.
// 높은 우선순위의 활성 controller 하나만 선택해 하위 controller가 같은 tick에 개입하지 못하게 한다.
public sealed class BattleControlPlannerRegistry
{
    private sealed class ControllerEntry
    {
        public readonly IBattleControlPlanner Planner;
        public readonly int Priority;
        public readonly bool IsGlobal;
        public readonly HashSet<BattleUnitCombatState> EnabledStates;

        public ControllerEntry(IBattleControlPlanner planner, int priority, bool isGlobal)
        {
            Planner = planner;
            Priority = priority;
            IsGlobal = isGlobal;
            EnabledStates = isGlobal ? null : new HashSet<BattleUnitCombatState>();
        }

        public bool AppliesTo(BattleUnitCombatState state) =>
            IsGlobal || (state != null && EnabledStates != null && EnabledStates.Contains(state));
    }

    private readonly List<ControllerEntry> _entries = new List<ControllerEntry>();

    public void RegisterGlobal(IBattleControlPlanner planner, int priority)
    {
        if (planner == null)
        {
            return;
        }

        _entries.Add(new ControllerEntry(planner, priority, true));
        SortEntries();
    }

    public void SetUnitPlannerEnabled(
        BattleUnitCombatState state,
        IBattleControlPlanner planner,
        int priority,
        bool enabled
    )
    {
        if (state == null || planner == null)
        {
            return;
        }

        ControllerEntry entry = FindEntry(planner, priority, false);
        if (entry == null)
        {
            entry = new ControllerEntry(planner, priority, false);
            _entries.Add(entry);
            SortEntries();
        }

        if (enabled)
            entry.EnabledStates.Add(state);
        else
            entry.EnabledStates.Remove(state);
    }

    public bool IsUnitPlannerEnabled(BattleUnitCombatState state, IBattleControlPlanner planner)
    {
        if (state == null || planner == null)
            return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            ControllerEntry entry = _entries[i];
            if (!entry.IsGlobal && ReferenceEquals(entry.Planner, planner) && entry.AppliesTo(state))
                return true;
        }

        return false;
    }

    public bool TryGet(BattleUnitCombatState state, in BattlePlanningContext context, out IBattleControlPlanner planner)
    {
        planner = null;
        if (state == null)
            return false;

        for (int i = 0; i < _entries.Count; i++)
        {
            ControllerEntry entry = _entries[i];
            if (!entry.AppliesTo(state) || entry.Planner == null)
                continue;

            if (!entry.Planner.IsActive(state, context))
                continue;

            planner = entry.Planner;
            return true;
        }

        return false;
    }

    public void Clear()
    {
        _entries.Clear();
    }

    private ControllerEntry FindEntry(IBattleControlPlanner planner, int priority, bool isGlobal)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            ControllerEntry entry = _entries[i];
            if (ReferenceEquals(entry.Planner, planner) && entry.Priority == priority && entry.IsGlobal == isGlobal)
            {
                return entry;
            }
        }

        return null;
    }

    private void SortEntries()
    {
        _entries.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }
}
