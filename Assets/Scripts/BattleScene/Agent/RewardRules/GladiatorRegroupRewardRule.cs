using UnityEngine;

public sealed class GladiatorRegroupRewardRule : IGladiatorRoleRewardRule
{
    private readonly GladiatorRewardConfig _config;

    public GladiatorRegroupRewardRule(GladiatorRewardConfig config)
    {
        _config = config;
    }

    public float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features
    )
    {
        float reward = Mathf.Clamp01(1f - features.EnemyClusterPressure) * _config.regroupSafetyReward;
        if (context.RoleCommitmentSteps > _config.regroupWindowSteps)
        {
            reward += _config.regroupOverstayPenalty;
        }

        return reward;
    }
}
