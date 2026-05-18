public sealed class GladiatorTacticalRewardShaper
{
    private readonly IGladiatorStrategyRewardRule[] _strategyRules;

    public GladiatorTacticalRewardShaper(GladiatorRewardConfig config)
    {
        _strategyRules = new IGladiatorStrategyRewardRule[GladiatorActionSchema.StrategyBranchSize];
        _strategyRules[(int)GladiatorStrategy.Pressure] = new GladiatorPressureRewardRule(config);
        _strategyRules[(int)GladiatorStrategy.KeepRange] = new GladiatorKeepRangeRewardRule(config);
        _strategyRules[(int)GladiatorStrategy.Retreat] = new GladiatorRetreatRewardRule(config);
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        int strategyIndex = (int)action.Strategy;
        if (strategyIndex < 0 || strategyIndex >= _strategyRules.Length)
        {
            return 0f;
        }

        IGladiatorStrategyRewardRule strategyRule = _strategyRules[strategyIndex];
        return strategyRule != null ? strategyRule.Evaluate(context, action, features) : 0f;
    }
}
