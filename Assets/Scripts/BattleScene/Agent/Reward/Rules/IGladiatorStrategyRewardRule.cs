public interface IGladiatorStrategyRewardRule
{
    float Evaluate(GladiatorTacticalContext context, GladiatorAction action, GladiatorCombatSignalFeatures features);
}
