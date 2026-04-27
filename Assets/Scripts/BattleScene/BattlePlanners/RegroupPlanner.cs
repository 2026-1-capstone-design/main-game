public sealed class RegroupPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.RegroupToAllies;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.RegroupToAllies,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = snapshot.ComputeTeamCenter(unit.State.IsEnemy),
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
