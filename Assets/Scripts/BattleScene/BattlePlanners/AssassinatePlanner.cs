public sealed class AssassinatePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.AssassinateIsolatedEnemy;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindBestIsolatedEnemy(state);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.AssassinateIsolatedEnemy,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : state.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
