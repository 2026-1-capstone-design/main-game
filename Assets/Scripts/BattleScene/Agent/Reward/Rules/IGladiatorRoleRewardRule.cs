public interface IGladiatorRoleRewardRule
{
    float Evaluate(GladiatorTacticalContext context, GladiatorAction action, GladiatorCombatSignalFeatures features);
}
