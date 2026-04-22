using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    private readonly Dictionary<BattleActionType, IBattleActionPlanner> _planners;

    public BattlePlanningSystem()
    {
        _planners = BuildPlannerRegistry();
    }

    public void Build(IReadOnlyList<BattleRuntimeUnit> units, BattleFieldView fieldView)
    {
        if (units == null || fieldView == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            if (unit.IsExternallyControlled)
                continue;

            BattleActionExecutionPlan plan;
            if (!_planners.TryGetValue(unit.CurrentActionType, out IBattleActionPlanner planner))
            {
                plan = _planners[BattleActionType.EngageNearest].Build(unit, fieldView);
            }
            else
            {
                plan = planner.Build(unit, fieldView);
                if (!planner.IsUsable(unit, plan, fieldView))
                {
                    IBattleActionPlanner engagePlanner = _planners[BattleActionType.EngageNearest];
                    BattleActionExecutionPlan engagePlan = engagePlanner.Build(unit, fieldView);
                    plan = engagePlanner.IsUsable(unit, engagePlan, fieldView) ? engagePlan : default;

                    if (plan.Action == BattleActionType.None)
                    {
                        plan.Action = unit.CurrentActionType;
                        plan.DesiredPosition = unit.Position;
                    }
                }
            }

            unit.SetExecutionPlan(plan);
        }
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
            new EngageNearestPlanner()
        };

        var dictionary = new Dictionary<BattleActionType, IBattleActionPlanner>(planners.Length);
        for (int i = 0; i < planners.Length; i++)
            dictionary[planners[i].ActionType] = planners[i];

        return dictionary;
    }
}
