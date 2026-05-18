using Unity.MLAgents.Actuators;
using UnityEngine;

public static class GladiatorAgentActionParser
{
    public static GladiatorAction Parse(ActionBuffers actions)
    {
        int command = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.CommandBranch,
            (int)GladiatorCommand.Move
        );
        command = Mathf.Clamp(command, 0, GladiatorActionSchema.CommandBranchSize - 1);

        int strategy = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.StrategyBranch,
            (int)GladiatorStrategy.Neutral
        );
        strategy = Mathf.Clamp(strategy, 0, GladiatorActionSchema.StrategyBranchSize - 1);

        int anchorAction = ReadDiscrete(actions.DiscreteActions, GladiatorActionSchema.AnchorBranch, 0);
        if (!GladiatorActionSchema.TryDecodeEnemyAnchorAction(anchorAction, out int anchorSlot))
        {
            anchorSlot = 0;
        }

        return new GladiatorAction(
            ReadMove(actions.ContinuousActions),
            (GladiatorStrategy)strategy,
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
        worldMove.Normalize();

        return worldMove;
    }

    private static int ReadDiscrete(ActionSegment<int> discreteActions, int branch, int fallback) =>
        discreteActions.Length > branch ? discreteActions[branch] : fallback;
}
