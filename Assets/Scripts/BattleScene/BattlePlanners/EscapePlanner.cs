using UnityEngine;

public sealed class EscapePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EscapeFromPressure;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot)
    {
        Vector3 selfPos = unit.Position;
        Vector3 pressureCenter = snapshot.ComputeEnemyPressureCenter(unit.State);
        Vector3 away = selfPos - pressureCenter;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
            away = unit.IsPlayerOwned ? Vector3.left : Vector3.right;
        away.Normalize();

        Vector3 teamCenter = snapshot.ComputeTeamCenter(unit.State.TeamId);
        Vector3 towardTeam = (teamCenter - selfPos);
        towardTeam.y = 0f;
        towardTeam.Normalize();

        float blend = snapshot.EscapeTowardTeamBlend;
        Vector3 escapeDir = (away * (1f - blend) + towardTeam * blend).normalized;
        Vector3 desiredPosition = selfPos + escapeDir * Mathf.Max(80f, unit.MoveSpeed);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EscapeFromPressure,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = desiredPosition,
            HasDesiredPosition = true,
        };
    }

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan) =>
        plan.HasDesiredPosition || BattleFieldSnapshot.IsValidEnemyTarget(unit.State, plan.TargetEnemy);
}
