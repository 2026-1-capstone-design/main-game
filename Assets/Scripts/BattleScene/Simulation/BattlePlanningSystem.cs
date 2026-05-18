using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    public void Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleControlPlannerRegistry planners,
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

            var context = new BattlePlanningContext(units, snapshot, aiTuning, tickDeltaTime);
            if (planners == null || !planners.TryGet(unit.State, context, out IBattleControlPlanner planner))
            {
                continue;
            }

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
        state.SetPlannedAnchor(ResolvePlannedAnchor(state, plan));
        if (plan.Move.Intent == BattleMoveIntent.MoveToAbsolutePosition)
        {
            state.SetExecutionPlanPosition(plan.Move.Position, true);
            return;
        }

        state.ClearExecutionPlanPosition();
    }

    private static BattleAnchor ResolvePlannedAnchor(BattleUnitCombatState state, in BattleControlPlan plan)
    {
        if (BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy))
        {
            return new BattleAnchor(
                BattleAnchorKind.Enemy,
                plan.TargetEnemy.UnitNumber,
                plan.TargetEnemy,
                plan.TargetEnemy.Position,
                true
            );
        }

        if (plan.TargetAlly != null && !plan.TargetAlly.IsCombatDisabled)
        {
            return new BattleAnchor(
                BattleAnchorKind.Ally,
                plan.TargetAlly.UnitNumber,
                plan.TargetAlly,
                plan.TargetAlly.Position,
                true
            );
        }

        return default;
    }
}
