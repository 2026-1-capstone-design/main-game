public sealed class GladiatorAssassinateRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorAssassinateRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        // TODO: Assassinate 전용 shaping은 추후 별도 설계한다. v1은 기본 교전 보상에 수렴시킨다.
        if (!context.HasValidTarget)
        {
            return 0f;
        }

        float approachDelta = context.PreviousTargetDistance - context.TargetDistance;
        return approachDelta > 0f ? approachDelta * _config.engageApproachReward : 0f;
    }
}
