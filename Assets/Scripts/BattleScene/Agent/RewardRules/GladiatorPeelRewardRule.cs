public sealed class GladiatorPeelRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorPeelRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features
    )
    {
        return (features.AnchorEnemyFocusPressure * _config.peelFocusReward)
            + (features.AnchorAllySupportPressure * _config.peelSupportReward);
    }
}
