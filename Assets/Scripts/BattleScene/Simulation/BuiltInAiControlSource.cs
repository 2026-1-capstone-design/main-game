using System.Collections.Generic;

public sealed class BuiltInAiControlSource : IBattleUnitControlSource
{
    private readonly BattleDecisionSystem _decisionSystem = new BattleDecisionSystem();
    private readonly Dictionary<BattleActionType, IBattleActionPlanner> _planners;

    private IReadOnlyList<BattleUnitCombatState> _states;
    private BattleAITuningSO _aiTuning;
    private BattleSkillChannelSystem _channelSystem;
    private BattleRosterMutationSystem _rosterMutationSystem;

    public BuiltInAiControlSource()
    {
        _planners = BuildPlannerRegistry();
    }

    public void Configure(
        IReadOnlyList<BattleUnitCombatState> states,
        BattleAITuningSO aiTuning,
        BattleSkillChannelSystem channelSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        _states = states;
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
        if (self == null || self.IsCombatDisabled || snapshot == null)
        {
            return false;
        }

        _decisionSystem.DecideBuiltInUnit(
            _states,
            self,
            _aiTuning,
            tickDeltaTime,
            _channelSystem,
            _rosterMutationSystem
        );
        BattleActionExecutionPlan executionPlan = BuildExecutionPlan(self, snapshot);
        BattleCombatCommand command = ResolveCombatCommand(self, executionPlan);
        plan = BattleControlPlan.FromExecutionPlan(self.CurrentActionType, executionPlan, command);
        return true;
    }

    public void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command) { }

    private BattleActionExecutionPlan BuildExecutionPlan(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        BattleActionExecutionPlan plan;
        if (!_planners.TryGetValue(state.CurrentActionType, out IBattleActionPlanner planner))
        {
            plan = _planners[BattleActionType.EngageNearest].Build(state, snapshot);
        }
        else
        {
            plan = planner.Build(state, snapshot);
            if (!planner.IsUsable(state, plan))
            {
                IBattleActionPlanner engagePlanner = _planners[BattleActionType.EngageNearest];
                BattleActionExecutionPlan engagePlan = engagePlanner.Build(state, snapshot);
                plan = engagePlanner.IsUsable(state, engagePlan) ? engagePlan : default;

                if (plan.Action == BattleActionType.None)
                {
                    plan.Action = state.CurrentActionType;
                    plan.DesiredPosition = state.Position;
                }
            }
        }

        return plan;
    }

    private static BattleCombatCommand ResolveCombatCommand(BattleUnitCombatState state, BattleActionExecutionPlan plan)
    {
        if (state == null || state.IsCombatDisabled)
        {
            return BattleCombatCommand.None;
        }

        if (HasReadySkill(state))
        {
            return BattleCombatCommand.Skill;
        }

        return IsCombatAction(plan.Action) && BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy)
            ? BattleCombatCommand.BasicAttack
            : BattleCombatCommand.None;
    }

    private static bool HasReadySkill(BattleUnitCombatState state) =>
        state != null && state.GetSkill() != WeaponSkillId.None && state.SkillCooldownRemaining <= 0f;

    private static bool IsCombatAction(BattleActionType actionType)
    {
        switch (actionType)
        {
            case BattleActionType.EngageNearest:
            case BattleActionType.AssassinateIsolatedEnemy:
            case BattleActionType.DiveEnemyBackline:
            case BattleActionType.PeelForWeakAlly:
            case BattleActionType.CollapseOnCluster:
                return true;
            default:
                return false;
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
            new EngageNearestPlanner(),
        };

        var dictionary = new Dictionary<BattleActionType, IBattleActionPlanner>(planners.Length);
        for (int i = 0; i < planners.Length; i++)
            dictionary[planners[i].ActionType] = planners[i];

        return dictionary;
    }
}
