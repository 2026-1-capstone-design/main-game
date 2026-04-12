public sealed class GladiatorPeelRewardRule : IGladiatorRoleRewardRule
{
    public GladiatorPeelRewardRule(GladiatorRewardConfig config) { }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        return 0f;
    }
}
