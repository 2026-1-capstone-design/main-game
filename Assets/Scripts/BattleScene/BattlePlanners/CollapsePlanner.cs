using UnityEngine;

public sealed class CollapsePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.CollapseOnCluster;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        Vector3 clusterCenter = field.ComputeTeamCenter(!unit.State.IsEnemy);
        BattleUnitCombatState target = field.FindEnemyClosestToPoint(unit.State, clusterCenter);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.CollapseOnCluster,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = clusterCenter,
            HasDesiredPosition = true
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field)
        => plan.HasDesiredPosition || field.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
