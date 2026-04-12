using Unity.MLAgents.Actuators;
using UnityEngine;

public static class GladiatorAgentActionParser
{
    public static GladiatorAction Parse(ActionBuffers actions)
    {
        Vector2 relativeMove = ReadMove(actions.ContinuousActions);
        int command = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.CommandBranch,
            (int)GladiatorCommand.Move
        );
        command = Mathf.Clamp(command, 0, GladiatorActionSchema.CommandBranchSize - 1);
        int role = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.RoleBranch,
            (int)GladiatorActionRole.Engage
        );
        role = Mathf.Clamp(role, 0, GladiatorActionSchema.RoleBranchSize - 1);
        int fightMode = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.FightModeBranch,
            (int)GladiatorFightMode.Neutral
        );
        fightMode = Mathf.Clamp(fightMode, 0, GladiatorActionSchema.FightModeBranchSize - 1);
        int anchorAction = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.AnchorBranch,
            GladiatorActionSchema.EnemyAnchorActionOffset
        );
        if (
            !GladiatorActionSchema.TryDecodeAnchorAction(
                anchorAction,
                out GladiatorAnchorKind anchorKind,
                out int anchorSlot
            )
        )
        {
            anchorKind = GladiatorAnchorKind.Enemy;
            anchorSlot = 0;
        }

        return new GladiatorAction(
            relativeMove,
            (GladiatorActionRole)role,
            (GladiatorFightMode)fightMode,
            anchorKind,
            anchorSlot,
            (GladiatorCommand)command
        );
    }

    private static Vector2 ReadMove(ActionSegment<float> continuousActions)
    {
        if (continuousActions.Length < GladiatorActionSchema.ContinuousSize)
        {
            return Vector2.zero;
        }

        var worldMove = new Vector2(
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousAnchorStrafe], -1f, 1f),
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousAnchorForward], -1f, 1f)
        );
        if (worldMove.sqrMagnitude > 1f)
        {
            worldMove.Normalize();
        }

        return worldMove;
    }

    private static int ReadDiscrete(ActionSegment<int> discreteActions, int branch, int fallback) =>
        discreteActions.Length > branch ? discreteActions[branch] : fallback;
}
