using UnityEngine;

public sealed class PeelPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.PeelForWeakAlly;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        BattleRuntimeUnit ally = field.FindMostPressuredAlly(unit);
        BattleRuntimeUnit enemy = field.FindBestPeelEnemy(unit, ally);

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

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field) =>
        field.IsValidEnemyTarget(unit, plan.TargetEnemy);
}
