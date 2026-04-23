public sealed class AssassinatePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.AssassinateIsolatedEnemy;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        BattleUnitCombatState target = snapshot.FindBestIsolatedEnemy(unit.State);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.AssassinateIsolatedEnemy,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        BattleFieldQueryHelper.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
