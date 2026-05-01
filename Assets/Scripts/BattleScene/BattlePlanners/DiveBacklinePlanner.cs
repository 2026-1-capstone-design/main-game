public sealed class DiveBacklinePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.DiveEnemyBackline;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindBestBacklineEnemy(state);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.DiveEnemyBackline,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : state.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
