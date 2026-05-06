public interface IGladiatorRoleRewardRule
{
    float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features
    );
}
