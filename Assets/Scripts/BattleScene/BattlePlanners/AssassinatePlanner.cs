public sealed class AssassinatePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.AssassinateIsolatedEnemy;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        BattleUnitCombatState target = field.FindBestIsolatedEnemy(unit.State);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.AssassinateIsolatedEnemy,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field)
        => field.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
