public sealed class DiveBacklinePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.DiveEnemyBackline;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        BattleRuntimeUnit target = field.FindBestBacklineEnemy(unit);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.DiveEnemyBackline,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field) =>
        field.IsValidEnemyTarget(unit, plan.TargetEnemy);
}
