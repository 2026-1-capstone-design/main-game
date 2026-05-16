using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    public void Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleControlPlanProviderRegistry providers,
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

            if (providers == null || !providers.TryGet(unit.State, out IBattleControlPlanProvider provider))
            {
                continue;
            }

            var context = new BattlePlanningContext(units, snapshot, aiTuning, tickDeltaTime);
            if (!provider.TryBuildPlan(unit.State, context, out BattleControlPlan plan))
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
        state.SetPlannedAnchor(plan.MovementAnchor);
        if (plan.MoveIntent == BattleMoveIntent.MoveToPosition && plan.HasDesiredPosition)
        {
            state.SetExecutionPlanPosition(plan.DesiredPosition, true);
            return;
        }

        state.ClearExecutionPlanPosition();
    }
}
