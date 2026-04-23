using UnityEngine;

public sealed class CollapsePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.CollapseOnCluster;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        Vector3 clusterCenter = snapshot.ComputeTeamCenter(!unit.State.IsEnemy);
        BattleUnitCombatState target = snapshot.FindEnemyClosestToPoint(unit.State, clusterCenter);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.CollapseOnCluster,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = clusterCenter,
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldQueryHelper.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
