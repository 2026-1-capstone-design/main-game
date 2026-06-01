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
        BattleActionType currentAction =
            selfState != null ? selfState.CurrentActionType : BattleActionType.EngageNearest;
        discrete[GladiatorActionSchema.StrategyBranch] = ResolveStrategy(currentAction);
        discrete[GladiatorActionSchema.AnchorBranch] = GladiatorActionSchema.EncodeEnemyAnchorAction(
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

        if (plan.Move.Intent != BattleMoveIntent.MoveToAbsolutePosition || self == null)
        {
            WriteIdleMovement(continuous);
            return;
        }

        Vector3 toTarget = plan.Move.Position - self.Position;
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
        if (IsWithdrawMove(plan, self))
            return (int)GladiatorCommand.Withdraw;
        if (self == null || self.AttackCooldownRemaining > 0f)
            return (int)GladiatorCommand.Move;
        return plan.CombatIntent == BattleCombatIntent.Attack
            ? (int)GladiatorCommand.Attack
            : (int)GladiatorCommand.Move;
    }

    private static bool IsWithdrawMove(BattleControlPlan plan, BattleUnitCombatState self)
    {
        if (
            plan.Move.Intent != BattleMoveIntent.MoveToAbsolutePosition
            || !BattleFieldSnapshot.IsValidEnemyTarget(self, plan.TargetEnemy)
        )
        {
            return false;
        }

        float currentDistance = Distance2D(self.Position, plan.TargetEnemy.Position);
        float desiredDistance = Distance2D(plan.Move.Position, plan.TargetEnemy.Position);
        return desiredDistance > currentDistance;
    }

    private static float Distance2D(Vector3 a, Vector3 b)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return delta.magnitude;
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

    private static int ResolveStrategy(BattleActionType actionType) =>
        actionType switch
        {
            BattleActionType.EscapeFromPressure => (int)GladiatorStrategy.Retreat,
            BattleActionType.AssassinateIsolatedEnemy => (int)GladiatorStrategy.Pressure,
            BattleActionType.DiveEnemyBackline => (int)GladiatorStrategy.Pressure,
            BattleActionType.CollapseOnCluster => (int)GladiatorStrategy.Pressure,
            _ => (int)GladiatorStrategy.Neutral,
        };
}
