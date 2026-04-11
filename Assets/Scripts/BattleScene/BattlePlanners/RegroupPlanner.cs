public sealed class RegroupPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.RegroupToAllies;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        return new BattleActionExecutionPlan
        {
            Action             = BattleActionType.RegroupToAllies,
            TargetEnemy        = null,
            TargetAlly         = null,
            DesiredPosition    = field.ComputeTeamCenter(unit.IsEnemy),
            HasDesiredPosition = true
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field)
        => plan.HasDesiredPosition || field.IsValidEnemyTarget(unit, plan.TargetEnemy);
}
