using UnityEngine;

public sealed class GladiatorRetreatRewardRule : IGladiatorStrategyRewardRule
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
        if (
            !context.HasValidTarget
            || context.TargetDistance >= float.MaxValue
            || context.PreviousTargetDistance >= float.MaxValue
            || context.IsAttackBlocked
        )
        {
            return 0f;
        }

        float reward = action.RelativeMove.y < 0f ? _config.retreatSeparationReward : 0f;
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
