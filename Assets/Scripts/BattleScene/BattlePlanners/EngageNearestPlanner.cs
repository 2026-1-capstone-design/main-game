public sealed class EngageNearestPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EngageNearest;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindNearestLivingEnemy(state);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EngageNearest,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : state.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
