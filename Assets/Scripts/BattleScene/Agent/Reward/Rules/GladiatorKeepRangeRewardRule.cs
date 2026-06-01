public sealed class GladiatorKeepRangeRewardRule : IGladiatorStrategyRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorKeepRangeRewardRule(GladiatorRewardConfig config)
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

        float ratio =
            context.TargetEffectiveRange > _config.minimumEffectiveRange
                ? context.TargetDistance / context.TargetEffectiveRange
                : 0f;

        float reward = 0f;
        if (ratio >= _config.keepRangeBandMin && ratio <= _config.keepRangeBandMax)
        {
            reward += _config.keepRangeBandReward;
        }

        float separationDelta = context.TargetDistance - context.PreviousTargetDistance;
        if (action.Command == GladiatorCommand.Move && ratio < _config.keepRangeBandMin && separationDelta > 0f)
        {
            reward += separationDelta * _config.keepRangeRecoverReward;
        }

        return reward;
    }
}
