using Unity.MLAgents.Actuators;
using UnityEngine;

public static class GladiatorAgentActionParser
{
    public static GladiatorPolicyAction Parse(ActionBuffers actions)
    {
        Vector2 relativeMove = ReadMove(actions.ContinuousActions);
        int command = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.CommandBranch,
            GladiatorActionSchema.CommandNone
        );
        command = Mathf.Clamp(command, 0, GladiatorActionSchema.CommandBranchSize - 1);
        int stance = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.StanceBranch,
            GladiatorActionSchema.StanceNeutral
        );
        int pathMode = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.PathModeBranch,
            GladiatorActionSchema.PathModeDirect
        );
        int anchorKind = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.AnchorKindBranch,
            GladiatorActionSchema.AnchorKindEnemy
        );
        int anchorSlot = ReadDiscrete(actions.DiscreteActions, GladiatorActionSchema.AnchorSlotBranch, 0);

        return new GladiatorPolicyAction(relativeMove, anchorKind, anchorSlot, pathMode, command, stance);
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
