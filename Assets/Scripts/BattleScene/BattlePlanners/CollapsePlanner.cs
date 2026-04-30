using UnityEngine;

public sealed class CollapsePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.CollapseOnCluster;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        Vector3 clusterCenter = snapshot.ComputeEnemyPressureCenter(state);
        BattleUnitCombatState target = snapshot.FindEnemyClosestToPoint(state, clusterCenter);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.CollapseOnCluster,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = clusterCenter,
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
