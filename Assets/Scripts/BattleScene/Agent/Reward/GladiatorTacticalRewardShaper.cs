public sealed class GladiatorTacticalRewardShaper
{
    private readonly IGladiatorRoleRewardRule[] _roleRules;
    private readonly IGladiatorFightModeRewardRule[] _fightModeRules;

    public GladiatorTacticalRewardShaper(GladiatorRewardConfig config)
    {
        _roleRules = new IGladiatorRoleRewardRule[GladiatorActionSchema.RoleBranchSize];
        _fightModeRules = new IGladiatorFightModeRewardRule[GladiatorActionSchema.FightModeBranchSize];
        _roleRules[(int)GladiatorActionRole.Engage] = new GladiatorEngageRewardRule(config);
        _roleRules[(int)GladiatorActionRole.Assassinate] = new GladiatorAssassinateRewardRule(config);
        _roleRules[(int)GladiatorActionRole.Regroup] = new GladiatorRegroupRewardRule(config);
        _fightModeRules[(int)GladiatorFightMode.Pressure] = new GladiatorPressureRewardRule(config);
        _fightModeRules[(int)GladiatorFightMode.KeepRange] = new GladiatorKeepRangeRewardRule(config);
        _fightModeRules[(int)GladiatorFightMode.Retreat] = new GladiatorRetreatRewardRule(config);
    }

    public float Evaluate(
        GladiatorTacticalContext context,
        GladiatorAction action,
        GladiatorCombatSignalFeatures features
    )
    {
        int roleIndex = (int)action.Role;
        if (roleIndex < 0 || roleIndex >= _roleRules.Length)
        {
            return 0f;
        }

        float reward = 0f;
        IGladiatorRoleRewardRule roleRule = _roleRules[roleIndex];
        if (roleRule != null)
        {
            reward += roleRule.Evaluate(context, action, features);
        }

        int fightModeIndex = (int)action.FightMode;
        if (fightModeIndex >= 0 && fightModeIndex < _fightModeRules.Length)
        {
            IGladiatorFightModeRewardRule fightModeRule = _fightModeRules[fightModeIndex];
            if (fightModeRule != null)
            {
                reward += fightModeRule.Evaluate(context, action, features);
            }
        }

        return reward;
    }
}
