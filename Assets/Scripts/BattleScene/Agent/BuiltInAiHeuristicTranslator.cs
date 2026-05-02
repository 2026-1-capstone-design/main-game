using System.Collections.Generic;
using Unity.MLAgents.Actuators;
using UnityEngine;

public static class BuiltInAiHeuristicTranslator
{
    public static void Write(
        ActionBuffers actionsOut,
        BattleControlPlan plan,
        BattleUnitPose selfPose,
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

        WriteMovement(continuous, plan, selfPose, selfState);
        discrete[GladiatorActionSchema.CommandBranch] = ResolveCommand(plan, selfState);
        discrete[GladiatorActionSchema.TargetBranch] = ResolveTargetSlot(plan.TargetEnemy, rosterView);
        discrete[GladiatorActionSchema.StanceBranch] = ResolveStance(plan.ActionType);
    }

    private static void WriteMovement(
        ActionSegment<float> continuous,
        BattleControlPlan plan,
        BattleUnitPose pose,
        BattleUnitCombatState self
    )
    {
        if (!plan.HasDesiredPosition || self == null)
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
        continuous[GladiatorActionSchema.ContinuousMoveX] = Mathf.Clamp(Vector3.Dot(dir, pose.Right), -1f, 1f);
        continuous[GladiatorActionSchema.ContinuousMoveZ] = Mathf.Clamp(Vector3.Dot(dir, pose.Forward), -1f, 1f);
        float cross = pose.Forward.x * dir.z - pose.Forward.z * dir.x;
        float dot = pose.Forward.x * dir.x + pose.Forward.z * dir.z;
        continuous[GladiatorActionSchema.ContinuousTurn] = Mathf.Clamp(-Mathf.Atan2(cross, dot) / Mathf.PI, -1f, 1f);
    }

    private static void WriteIdleMovement(ActionSegment<float> continuous)
    {
        continuous[GladiatorActionSchema.ContinuousMoveX] = 0f;
        continuous[GladiatorActionSchema.ContinuousMoveZ] = 0f;
        continuous[GladiatorActionSchema.ContinuousTurn] = 0f;
    }

    private static int ResolveCommand(BattleControlPlan plan, BattleUnitCombatState self)
    {
        if (plan.TargetEnemy == null || plan.TargetEnemy.IsCombatDisabled)
            return GladiatorActionSchema.CommandNone;
        if (self == null || self.AttackCooldownRemaining > 0f)
            return GladiatorActionSchema.CommandNone;
        return IsCombatAction(plan.ActionType)
            ? GladiatorActionSchema.CommandBasicAttack
            : GladiatorActionSchema.CommandNone;
    }

    private static int ResolveTargetSlot(BattleUnitCombatState target, GladiatorStateRosterView rosterView)
    {
        if (target == null || rosterView == null)
            return 0;
        IReadOnlyList<BattleUnitCombatState> hostiles = rosterView.Hostiles;
        for (int i = 0; i < hostiles.Count; i++)
            if (hostiles[i] == target)
                return i;
        return 0;
    }

    private static int ResolveStance(BattleActionType actionType) =>
        actionType switch
        {
            BattleActionType.EscapeFromPressure => GladiatorActionSchema.StanceKeepRange,
            BattleActionType.AssassinateIsolatedEnemy => GladiatorActionSchema.StancePressure,
            BattleActionType.DiveEnemyBackline => GladiatorActionSchema.StancePressure,
            BattleActionType.CollapseOnCluster => GladiatorActionSchema.StancePressure,
            _ => GladiatorActionSchema.StanceNeutral,
        };

    private static bool IsCombatAction(BattleActionType actionType) =>
        actionType switch
        {
            BattleActionType.EngageNearest => true,
            BattleActionType.AssassinateIsolatedEnemy => true,
            BattleActionType.DiveEnemyBackline => true,
            BattleActionType.PeelForWeakAlly => true,
            BattleActionType.CollapseOnCluster => true,
            _ => false,
        };
}
