using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;

public static class BuiltInAiHeuristicTranslator
{
    public static void Write(
        ActionBuffers actionsOut,
        BattleControlPlan plan,
        BattleUnitCombatState selfState,
        GladiatorStateRosterView rosterView
    )
    {
        ActionSegment<float> continuous = actionsOut.ContinuousActions;
        ActionSegment<int> discrete = actionsOut.DiscreteActions;

        if (
            continuous.Length < GladiatorActionSchema.ContinuousSize
            || discrete.Length < GladiatorActionSchema.DiscreteBranchCount
        )
        {
            return;
        }

        WriteMovement(continuous, plan, selfState);
        discrete[GladiatorActionSchema.CommandBranch] = ResolveCommand(plan, selfState);
        discrete[GladiatorActionSchema.RoleBranch] = ResolveRole(plan.ActionType);
        discrete[GladiatorActionSchema.FightModeBranch] = ResolveFightMode(plan.ActionType);
        discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeAnchorAction(
            GladiatorAnchorKind.Enemy,
            ResolveTargetSlot(plan.TargetEnemy, rosterView)
        );
    }

    private static void WriteMovement(
        ActionSegment<float> continuous,
        BattleControlPlan plan,
        BattleUnitCombatState self
    )
    {
        if (
            BattleFieldSnapshot.IsValidEnemyTarget(self, plan.TargetEnemy)
            && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(self, plan.TargetEnemy)
        )
        {
            WriteIdleMovement(continuous);
            return;
        }

        if (plan.MoveIntent != BattleMoveIntent.MoveToPosition || !plan.HasDesiredPosition || self == null)
        {
            WriteIdleMovement(continuous);
            return;
        }

        Vector3 toTarget = plan.DesiredPosition - self.Position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        if (dist < 0.01f)
        {
            WriteIdleMovement(continuous);
            return;
        }

        Vector3 dir = toTarget / dist;
        Vector2 relativeMove = new Vector2(dir.x, dir.z);
        Vector2 anchorForward = Vector2.up;
        if (BattleFieldSnapshot.IsValidEnemyTarget(self, plan.TargetEnemy))
        {
            Vector3 toAnchor3 = plan.TargetEnemy.Position - self.Position;
            toAnchor3.y = 0f;
            Vector2 anchorDelta = new Vector2(toAnchor3.x, toAnchor3.z);
            if (anchorDelta.sqrMagnitude > 0.0001f)
            {
                anchorForward = anchorDelta.normalized;
            }
        }

        Vector2 anchorLeft = new Vector2(-anchorForward.y, anchorForward.x);
        continuous[GladiatorActionSchema.ContinuousAnchorStrafe] = Mathf.Clamp(
            Vector2.Dot(relativeMove, anchorLeft),
            -1f,
            1f
        );
        continuous[GladiatorActionSchema.ContinuousAnchorForward] = Mathf.Clamp(
            Vector2.Dot(relativeMove, anchorForward),
            -1f,
            1f
        );
    }

    private static void WriteIdleMovement(ActionSegment<float> continuous)
    {
        continuous[GladiatorActionSchema.ContinuousAnchorStrafe] = 0f;
        continuous[GladiatorActionSchema.ContinuousAnchorForward] = 0f;
    }

    private static int ResolveCommand(BattleControlPlan plan, BattleUnitCombatState self)
    {
        if (plan.TargetEnemy == null || plan.TargetEnemy.IsCombatDisabled)
            return (int)GladiatorCommand.Move;
        if (self == null || self.AttackCooldownRemaining > 0f)
            return (int)GladiatorCommand.Move;
        return plan.CombatIntent == BattleCombatIntent.Attack
            ? (int)GladiatorCommand.Attack
            : (int)GladiatorCommand.Move;
    }

    private static int ResolveTargetSlot(BattleUnitCombatState target, GladiatorStateRosterView rosterView)
    {
        if (rosterView == null)
            return 0;

        IReadOnlyList<BattleUnitCombatState> hostiles = rosterView.Hostiles;
        for (int i = 0; i < hostiles.Count; i++)
            if (hostiles[i] == target)
                return i;

        for (int i = 0; i < hostiles.Count; i++)
            if (hostiles[i] != null && !hostiles[i].IsCombatDisabled)
                return i;

        return 0;
    }

    private static int ResolveFightMode(BattleActionType actionType) =>
        actionType switch
        {
            BattleActionType.EscapeFromPressure => (int)GladiatorFightMode.KeepRange,
            BattleActionType.AssassinateIsolatedEnemy => (int)GladiatorFightMode.Pressure,
            BattleActionType.DiveEnemyBackline => (int)GladiatorFightMode.Pressure,
            BattleActionType.CollapseOnCluster => (int)GladiatorFightMode.Pressure,
            _ => (int)GladiatorFightMode.Neutral,
        };

    private static int ResolveRole(BattleActionType actionType) =>
        actionType switch
        {
            BattleActionType.AssassinateIsolatedEnemy => (int)GladiatorActionRole.Assassinate,
            BattleActionType.EscapeFromPressure => (int)GladiatorActionRole.Regroup,
            BattleActionType.PeelForWeakAlly => (int)GladiatorActionRole.Regroup,
            _ => (int)GladiatorActionRole.Engage,
        };
}
