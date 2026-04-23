public sealed class DiveBacklinePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.DiveEnemyBackline;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindBestBacklineEnemy(unit.State);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.DiveEnemyBackline,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        BattleFieldQueryHelper.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
