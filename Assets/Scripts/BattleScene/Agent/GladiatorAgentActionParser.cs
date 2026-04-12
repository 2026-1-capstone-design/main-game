using Unity.MLAgents.Actuators;
using UnityEngine;

public static class GladiatorAgentActionParser
{
    public static GladiatorAgentAction Parse(ActionBuffers actions)
    {
        Vector2 localMove = ReadMove(actions.ContinuousActions);
        float turn = ReadTurn(actions.ContinuousActions);
        int command = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.CommandBranch,
            GladiatorActionSchema.CommandNone
        );
        int targetSlot = ReadDiscrete(actions.DiscreteActions, GladiatorActionSchema.TargetBranch, 0);
        int stance = ReadDiscrete(
            actions.DiscreteActions,
            GladiatorActionSchema.StanceBranch,
            GladiatorActionSchema.StanceNeutral
        );

        return new GladiatorAgentAction(localMove, turn, command, targetSlot, stance);
    }

    private static Vector2 ReadMove(ActionSegment<float> continuousActions)
    {
        if (continuousActions.Length < GladiatorActionSchema.ContinuousSize)
        {
            return Vector2.zero;
        }

        var localMove = new Vector2(
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousMoveX], -1f, 1f),
            Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousMoveZ], -1f, 1f)
        );
        if (localMove.sqrMagnitude > 1f)
        {
            localMove.Normalize();
        }

        return localMove;
    }

    private static float ReadTurn(ActionSegment<float> continuousActions) =>
        continuousActions.Length > GladiatorActionSchema.ContinuousTurn
            ? Mathf.Clamp(continuousActions[GladiatorActionSchema.ContinuousTurn], -1f, 1f)
            : 0f;

    private static int ReadDiscrete(ActionSegment<int> discreteActions, int branch, int fallback) =>
        discreteActions.Length > branch ? discreteActions[branch] : fallback;
}
