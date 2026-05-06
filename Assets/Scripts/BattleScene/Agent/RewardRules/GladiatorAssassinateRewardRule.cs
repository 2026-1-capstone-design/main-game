public sealed class GladiatorAssassinateRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorAssassinateRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

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
            reward += approachDelta * _config.assassinateApproachReward;
        }

        reward += features.AnchorEnemyIsolation * _config.assassinateIsolationReward;
        reward += features.AnchorEnemyRetreatSignal * _config.assassinateRetreatReward;
        if (features.AnchorInSelfRange > 0.5f)
        {
            reward += _config.assassinateFinishReward;
        }

        return reward;
    }
}
