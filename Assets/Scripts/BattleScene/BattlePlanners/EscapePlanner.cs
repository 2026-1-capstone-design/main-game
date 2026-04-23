using UnityEngine;

public sealed class EscapePlanner : IBattleActionPlanner
{
    public BattleActionType ActionType => BattleActionType.EscapeFromPressure;

    public BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldView field)
    {
        Vector3 selfPos = unit.Position;
        Vector3 pressureCenter = field.ComputeEnemyPressureCenter(unit);
        Vector3 away = selfPos - pressureCenter;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
            away = unit.IsEnemy ? Vector3.right : Vector3.left;
        away.Normalize();

        Vector3 teamCenter = field.ComputeTeamCenter(unit.IsEnemy);
        Vector3 towardTeam = (teamCenter - selfPos);
        towardTeam.y = 0f;
        towardTeam.Normalize();

        float blend = field.EscapeTowardTeamBlend;
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

    public bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan, BattleFieldView field) =>
        plan.HasDesiredPosition || field.IsValidEnemyTarget(unit, plan.TargetEnemy);
}
