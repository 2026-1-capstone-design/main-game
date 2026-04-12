public sealed class BattlePlanningSystem
{
    public void Build(
        System.Collections.Generic.IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleControlSourceRegistry controlSources,
        float tickDeltaTime,
        BattleControlPlan[] controlPlans
    )
    {
        if (units == null || snapshot == null || controlSources == null || controlPlans == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            if (i >= controlPlans.Length)
                break;

            controlPlans[i] = default;
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            if (!controlSources.TryGet(unit.State, out IBattleUnitControlSource source))
                continue;

            if (!source.TryBuildPlan(unit.State, snapshot, tickDeltaTime, out BattleControlPlan plan))
                continue;

            controlPlans[i] = plan;
            unit.State.SetPlannedTargets(plan.TargetEnemy, plan.TargetAlly);
            unit.State.SetExecutionPlanPosition(plan.DesiredPosition, plan.HasDesiredPosition);
        }
    }
}
