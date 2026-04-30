using UnityEngine;

public sealed class PeelPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.PeelForWeakAlly;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState ally = snapshot.FindMostPressuredAlly(state);
        BattleUnitCombatState enemy = snapshot.FindBestPeelEnemy(state, ally);

        Vector3 desiredPosition = ally != null ? ally.Position : state.Position;
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

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
