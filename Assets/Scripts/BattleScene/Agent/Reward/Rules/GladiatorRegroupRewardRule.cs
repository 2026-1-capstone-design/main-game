using UnityEngine;

public sealed class GladiatorRegroupRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorRegroupRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        float reward = Mathf.Clamp01(1f - features.EnemyClusterPressure) * _config.regroupSafetyReward;
        if (action.AnchorKind != GladiatorAnchorKind.TeamCenter)
        {
            reward += Mathf.Clamp01(1f - features.AnchorDistanceRatio) * _config.regroupCohesionReward;
        }
        if (context.RoleCommitmentSteps > _config.regroupWindowSteps)
        {
            reward += _config.regroupOverstayPenalty;
        }

        return reward;
    }
}
