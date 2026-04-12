using System.Collections.Generic;

// 기존 built-in AI의 decision + 행동별 execution planner 조합을 담당한다.
public sealed class BuiltInAiUnitPlanner : IBattleUnitPlanner
{
    private readonly BattleDecisionSystem _decisionSystem = new BattleDecisionSystem();
    private readonly Dictionary<BattleActionType, IBattleActionPlanner> _planners = BuildPlannerRegistry();

    public bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan)
    {
        plan = default;
        BattleRuntimeUnit unit = FindRuntimeUnit(self, context.Units);
        if (unit == null || unit.IsCombatDisabled || context.Snapshot == null)
        {
            return false;
        }

        _decisionSystem.DecideBuiltInUnit(context.Units, unit, context.AiTuning, context.TickDeltaTime);
        BattleActionExecutionPlan executionPlan = BuildExecutionPlan(unit, context.Snapshot);
        plan = BattleControlPlan.FromExecutionPlan(unit.State, unit.CurrentActionType, executionPlan);
        return true;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command) { }

    private BattleActionExecutionPlan BuildExecutionPlan(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        BattleActionExecutionPlan plan;
        if (!_planners.TryGetValue(unit.CurrentActionType, out IBattleActionPlanner planner))
        {
            plan = _planners[BattleActionType.EngageNearest].Build(unit, snapshot);
        }
        else
        {
            plan = planner.Build(unit, snapshot);
            if (!planner.IsUsable(unit, plan))
            {
                IBattleActionPlanner engagePlanner = _planners[BattleActionType.EngageNearest];
                BattleActionExecutionPlan engagePlan = engagePlanner.Build(unit, snapshot);
                plan = engagePlanner.IsUsable(unit, engagePlan) ? engagePlan : default;

                if (plan.Action == BattleActionType.None)
                {
                    plan.Action = unit.CurrentActionType;
                    plan.DesiredPosition = unit.Position;
                }
            }
        }

        return plan;
    }

    private static BattleRuntimeUnit FindRuntimeUnit(
        BattleUnitCombatState state,
        IReadOnlyList<BattleRuntimeUnit> units
    )
    {
        if (state == null || units == null)
        {
            return null;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit != null && unit.State == state)
            {
                return unit;
            }
        }

        return null;
    }

    private static Dictionary<BattleActionType, IBattleActionPlanner> BuildPlannerRegistry()
    {
        var planners = new IBattleActionPlanner[]
        {
            new AssassinatePlanner(),
            new DiveBacklinePlanner(),
            new PeelPlanner(),
            new EscapePlanner(),
            new RegroupPlanner(),
            new CollapsePlanner(),
            new EngageNearestPlanner(),
        };

        var dictionary = new Dictionary<BattleActionType, IBattleActionPlanner>(planners.Length);
        for (int i = 0; i < planners.Length; i++)
        {
            dictionary[planners[i].ActionType] = planners[i];
        }

        return dictionary;
    }
}
