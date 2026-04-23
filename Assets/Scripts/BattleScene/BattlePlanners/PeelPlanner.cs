using UnityEngine;

public sealed class PeelPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.PeelForWeakAlly;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState ally = snapshot.FindMostPressuredAlly(unit.State);
        BattleUnitCombatState enemy = snapshot.FindBestPeelEnemy(unit.State, ally);

        Vector3 desiredPosition = ally != null ? ally.Position : unit.Position;
        bool hasDesiredPosition = ally != null;

        if (enemy != null)
        {
            desiredPosition = enemy.Position;
            hasDesiredPosition = true;
        }

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.PeelForWeakAlly,
            TargetEnemy = enemy,
            TargetAlly = ally,
            DesiredPosition = desiredPosition,
            HasDesiredPosition = hasDesiredPosition,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        BattleFieldQueryHelper.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
