using Unity.MLAgents.Actuators;
using UnityEngine;

public static class GladiatorAgentActionParser
{
    public static GladiatorAgentAction Parse(ActionBuffers actions)
    {
        Vector2 worldMove = ReadMove(actions.ContinuousActions);
        int command = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.CommandBranch,
            GladiatorActionSchema.CommandNone
        );
        command = Mathf.Clamp(command, 0, GladiatorActionSchema.CommandBranchSize - 1);
        int targetSlot = ReadDiscrete(actions.DiscreteActions, GladiatorActionSchema.TargetBranch, 0);
        int stance = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.StanceBranch,
            GladiatorActionSchema.StanceNeutral
        );

        return new GladiatorAgentAction(worldMove, command, targetSlot, stance);
    }

    private static Vector2 ReadMove(ActionSegment<float> continuousActions)
    {
        if (continuousActions.Length < GladiatorActionSchema.ContinuousSize)
        {
            return Vector2.zero;
        }

        var worldMove = new Vector2(
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousWorldMoveX], -1f, 1f),
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousWorldMoveZ], -1f, 1f)
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
