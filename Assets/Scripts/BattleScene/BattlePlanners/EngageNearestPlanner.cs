public sealed class EngageNearestPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EngageNearest;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        BattleRuntimeUnit target = field.FindNearestLivingEnemy(unit);
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EngageNearest,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field)
        => field.IsValidEnemyTarget(unit, plan.TargetEnemy);
}
