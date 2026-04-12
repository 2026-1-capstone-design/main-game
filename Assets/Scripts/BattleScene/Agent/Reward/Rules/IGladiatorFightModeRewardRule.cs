public interface IGladiatorFightModeRewardRule
{
    float Evaluate(GladiatorTacticalContext context, GladiatorAction action, GladiatorCombatSignalFeatures features);
}
