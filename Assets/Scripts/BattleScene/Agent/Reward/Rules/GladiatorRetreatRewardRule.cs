public sealed class GladiatorRetreatRewardRule : IGladiatorFightModeRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorRetreatRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        if (!context.HasValidTarget || context.TargetDistance >= float.MaxValue)
        {
            return 0f;
        }

        float separationDelta = context.TargetDistance - context.PreviousTargetDistance;
        float reward = separationDelta > 0f ? separationDelta * _config.retreatSeparationReward : 0f;
        if (
            context.PreviousTargetDistance <= context.TargetEffectiveRange
            && context.TargetDistance > context.TargetEffectiveRange
        )
        {
            reward += _config.retreatEscapeRangeReward;
        }

        return reward;
    }
}
