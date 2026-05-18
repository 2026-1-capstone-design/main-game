public sealed class GladiatorEngageRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorEngageRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
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
            reward += approachDelta * _config.engageApproachReward;
        }

        if (context.PreviousTargetDistance >= float.MaxValue * 0.5f && context.TargetDistance < float.MaxValue * 0.5f)
        {
            reward += _config.engageReacquireReward;
        }

        return reward;
    }
}
