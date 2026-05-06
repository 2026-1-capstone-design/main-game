public sealed class GladiatorTacticalRewardShaper
{
    private readonly IGladiatorRoleRewardRule[] _roleRules;

    public GladiatorTacticalRewardShaper(GladiatorRewardConfig config)
    {
        _roleRules = new IGladiatorRoleRewardRule[GladiatorActionSchema.RoleBranchSize];
        _roleRules[GladiatorActionSchema.RoleEngage] = new GladiatorEngageRewardRule(config);
        _roleRules[GladiatorActionSchema.RolePeel] = new GladiatorPeelRewardRule(config);
        _roleRules[GladiatorActionSchema.RoleAssassinate] = new GladiatorAssassinateRewardRule(config);
        _roleRules[GladiatorActionSchema.RoleRegroup] = new GladiatorRegroupRewardRule(config);
    }

    public float Evaluate(
        GladiatorAgentTacticalContext context,
        GladiatorPolicyAction action,
        GladiatorTacticalFeatures features
    )
    {
        if (action.Role < 0 || action.Role >= _roleRules.Length)
        {
            return 0f;
        }

        IGladiatorRoleRewardRule rule = _roleRules[action.Role];
        return rule != null ? rule.Evaluate(context, action, features) : 0f;
    }
}
