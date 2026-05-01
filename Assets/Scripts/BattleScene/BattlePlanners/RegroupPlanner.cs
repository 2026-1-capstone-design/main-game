public sealed class RegroupPlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.RegroupToAllies;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.RegroupToAllies,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = snapshot.ComputeTeamCenter(state.TeamId),
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
