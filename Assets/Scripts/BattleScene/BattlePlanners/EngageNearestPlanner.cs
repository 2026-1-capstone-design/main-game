public sealed class EngageNearestPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EngageNearest;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindNearestLivingEnemy(unit.State);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EngageNearest,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        BattleFieldQueryHelper.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
