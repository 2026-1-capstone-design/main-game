using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    public void Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleUnitPlannerRegistry planners,
        BattleAITuningSO aiTuning,
        float tickDeltaTime,
        BattleControlPlan[] controlPlans,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (units == null || controlPlans == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            if (i >= controlPlans.Length)
                break;

            controlPlans[i] = default;
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            if (rosterMutationSystem != null && rosterMutationSystem.IsCommandDisabled(unit))
                continue;

            if (planners == null || !planners.TryGet(unit.State, out IBattleUnitPlanner planner))
            {
                continue;
            }

            var context = new BattlePlanningContext(units, snapshot, aiTuning, tickDeltaTime);
            if (!planner.TryBuildPlan(unit.State, context, out BattleControlPlan plan))
            {
                continue;
            }

            controlPlans[i] = plan;
            SyncPlannedState(unit.State, plan);
        }
    }

    private static void SyncPlannedState(BattleUnitCombatState state, in BattleControlPlan plan)
    {
        if (state == null)
            return;

        state.SetPlannedTargets(plan.TargetEnemy, plan.TargetAlly);
        state.SetPlannedAnchor(plan.TacticalCommand.Anchor);
        if (plan.MoveIntent == BattleMoveIntent.MoveToPosition && plan.HasDesiredPosition)
        {
            state.SetExecutionPlanPosition(plan.DesiredPosition, true);
            return;
        }

        state.ClearExecutionPlanPosition();
    }
}
