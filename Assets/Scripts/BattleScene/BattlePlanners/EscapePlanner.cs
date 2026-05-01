using UnityEngine;

public sealed class EscapePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EscapeFromPressure;

    public BattleActionExecutionPlan Build(BattleUnitCombatState state, BattleFieldSnapshot snapshot)
    {
        Vector3 selfPos = state.Position;
        Vector3 pressureCenter = snapshot.ComputeEnemyPressureCenter(state);
        Vector3 away = selfPos - pressureCenter;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
            away = state.TeamId == BattleTeamIds.Player ? Vector3.left : Vector3.right;
        away.Normalize();

        Vector3 teamCenter = snapshot.ComputeTeamCenter(state.TeamId);
        Vector3 towardTeam = (teamCenter - selfPos);
        towardTeam.y = 0f;
        towardTeam.Normalize();

        float blend = snapshot.EscapeTowardTeamBlend;
        Vector3 escapeDir = (away * (1f - blend) + towardTeam * blend).normalized;
        Vector3 desiredPosition = selfPos + escapeDir * Mathf.Max(80f, state.MoveSpeed);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EscapeFromPressure,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = desiredPosition,
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleUnitCombatState state, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldSnapshot.IsValidEnemyTarget(state, plan.TargetEnemy);
}
