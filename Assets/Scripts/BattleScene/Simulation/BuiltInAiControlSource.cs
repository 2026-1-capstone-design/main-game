using System.Collections.Generic;

public sealed class BuiltInAiControlSource : IBattleUnitControlSource
{
    private readonly BattleDecisionSystem _decisionSystem = new BattleDecisionSystem();
    private readonly Dictionary<BattleActionType, IBattleActionPlanner> _planners;

    private IReadOnlyList<BattleRuntimeUnit> _units;
    private BattleAITuningSO _aiTuning;
    private BattleSkillChannelSystem _channelSystem;
    private BattleRosterMutationSystem _rosterMutationSystem;

    public BuiltInAiControlSource()
    {
        _planners = BuildPlannerRegistry();
    }

    public void Configure(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleAITuningSO aiTuning,
        BattleSkillChannelSystem channelSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        _units = units;
        _aiTuning = aiTuning;
        _channelSystem = channelSystem;
        _rosterMutationSystem = rosterMutationSystem;
    }

    public bool TryBuildPlan(
        BattleUnitCombatState self,
        BattleFieldSnapshot snapshot,
        float tickDeltaTime,
        out BattleControlPlan plan
    )
    {
        plan = default;
        BattleRuntimeUnit unit = FindRuntimeUnit(self);
        if (unit == null || unit.IsCombatDisabled || snapshot == null)
        {
            return false;
        }

        _decisionSystem.DecideBuiltInUnit(
            _units,
            unit,
            _aiTuning,
            tickDeltaTime,
            _channelSystem,
            _rosterMutationSystem
        );
        BattleActionExecutionPlan executionPlan = BuildExecutionPlan(unit, snapshot);
        plan = BattleControlPlan.FromExecutionPlan(unit.CurrentActionType, executionPlan);
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

    private BattleRuntimeUnit FindRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _units == null)
        {
            return null;
        }

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit unit = _units[i];
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
            dictionary[planners[i].ActionType] = planners[i];

        return dictionary;
    }
}
