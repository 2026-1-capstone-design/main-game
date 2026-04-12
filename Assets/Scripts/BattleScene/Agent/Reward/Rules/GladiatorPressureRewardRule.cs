public sealed class GladiatorPressureRewardRule : IGladiatorFightModeRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorPressureRewardRule(GladiatorRewardConfig config)
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
            reward += approachDelta * _config.pressureApproachReward;
        }

        if (action.Command == GladiatorCommand.Attack && IsFavorableAttackRange(context))
        {
            reward += _config.pressureFavorableRangeReward;
        }

        if (action.Command == GladiatorCommand.Move && context.IsTargetOutOfAttackRange && approachDelta > 0f)
        {
            reward += approachDelta * _config.pressureMoveIntoRangeReward;
        }

        return reward;
    }

    private static bool IsFavorableAttackRange(GladiatorTacticalContext context) =>
        !context.IsTargetOutOfAttackRange && context.SelfThreatToTargetRatio >= context.TargetThreatToSelfRatio;
}
