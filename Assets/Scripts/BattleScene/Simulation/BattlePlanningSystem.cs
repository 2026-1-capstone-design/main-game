using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    public void Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleControlSourceRegistry controlSources,
        float tickDeltaTime,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (units == null || snapshot == null || controlSources == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            unit.State.ClearCurrentPlan();

            if (rosterMutationSystem != null && rosterMutationSystem.IsCommandDisabled(unit))
                continue;

            if (!controlSources.TryGet(unit.State, out IBattleUnitControlSource source))
                continue;

            if (!source.TryBuildPlan(unit.State, snapshot, tickDeltaTime, out BattleControlPlan plan))
                continue;

            unit.State.SetCurrentPlan(plan);
        }
    }
}
