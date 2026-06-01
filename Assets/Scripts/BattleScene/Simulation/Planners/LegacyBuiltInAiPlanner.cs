using System.Collections.Generic;

// Deprecated: 기존 built-in AI의 decision + 행동별 execution planner 조합을 보관하는 legacy planner다.
// 신규 제어 로직은 BattleControlPlan을 직접 공급하는 별도 planner로 추가한다.
public sealed class LegacyBuiltInAiPlanner : IBattleControlPlanner
{
    private readonly BattleDecisionSystem _decisionSystem = new BattleDecisionSystem();
    private readonly Dictionary<BattleActionType, IBattleActionPlanner> _planners = BuildPlannerRegistry();

    public bool IsActive(BattleUnitCombatState self, in BattlePlanningContext context) =>
        self != null && context.Snapshot != null;

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
        plan = BuildControlPlan(unit.State, executionPlan);
        return true;
    }

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

    private static BattleControlPlan BuildControlPlan(
        BattleUnitCombatState self,
        BattleActionExecutionPlan executionPlan
    )
    {
        BattleUnitCombatState targetEnemy = executionPlan.TargetEnemy;
        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, targetEnemy);
        bool inAttackRange = hasValidTarget && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(self, targetEnemy);
        bool shouldUseSkill = CanAutoCastSkillToEnemy(self, inAttackRange);

        BattleMoveIntent moveIntent;
        BattleCombatIntent combatIntent;
        BattleFacingIntent facingIntent;
        if (shouldUseSkill)
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.Skill;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else if (inAttackRange)
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.Attack;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else if (executionPlan.HasDesiredPosition)
        {
            moveIntent = BattleMoveIntent.MoveToAbsolutePosition;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.DesiredPosition;
        }
        else if (hasValidTarget)
        {
            moveIntent = BattleMoveIntent.MoveToTarget;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.KeepCurrent;
        }

        BattleMove move = moveIntent switch
        {
            BattleMoveIntent.MoveToAbsolutePosition => BattleMove.ToAbsolutePosition(executionPlan.DesiredPosition),
            BattleMoveIntent.MoveToTarget => BattleMove.ToTarget(targetEnemy),
            _ => BattleMove.Hold(),
        };

        return new BattleControlPlan(targetEnemy, executionPlan.TargetAlly, move, combatIntent, facingIntent);
    }

    private static bool CanAutoCastSkillToEnemy(BattleUnitCombatState self, bool inAttackRange)
    {
        if (self == null || self.IsSkillDisabled)
            return false;

        if (self.IsCastingSkill)
            return true;

        if (!inAttackRange || self.GetSkill() == WeaponSkillId.None)
            return false;

        return self.SkillCooldownRemaining <= 0f;
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
