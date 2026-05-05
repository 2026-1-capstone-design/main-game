using UnityEngine;

public sealed class GladiatorTacticalRewardShaper
{
    private const float AnchorApproachReward = 0.01f;
    private const float AnchorVisibilityReward = 0.002f;
    private const float ReacquireReward = 0.01f;
    private const float FlankReward = 0.004f;
    private const float PeelReward = 0.004f;

    public float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features
    )
    {
        if (!context.HasValidTarget)
        {
            return 0f;
        }

        float reward = 0f;
        float approachDelta = context.PreviousTargetDistance - context.TargetDistance;
        if (approachDelta > 0f)
        {
            reward += approachDelta * AnchorApproachReward;
        }

        if (features.AnchorVisibility > 0.5f)
        {
            reward += AnchorVisibilityReward;
        }

        if (context.PreviousTargetDistance >= float.MaxValue * 0.5f && context.TargetDistance < float.MaxValue * 0.5f)
        {
            reward += ReacquireReward;
        }

        if (
            (action.PathMode == GladiatorActionSchema.PathModeFlankLeft
                || action.PathMode == GladiatorActionSchema.PathModeFlankRight)
            && Mathf.Abs(action.RelativeMove.x) > 0.1f
        )
        {
            reward += FlankReward * Mathf.Clamp01(1f - features.EnemyClusterPressure);
        }

        if (action.AnchorKind == GladiatorActionSchema.AnchorKindAlly && features.AllyUnderFocusRatio > 0f)
        {
            reward += PeelReward * features.AllyUnderFocusRatio;
        }

        return reward;
    }
}
