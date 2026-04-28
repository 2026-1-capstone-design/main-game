public static class GladiatorActionSchema
{
    public const int ContractVersion = 2;

    public const int ContinuousMoveX = 0;
    public const int ContinuousMoveZ = 1;
    public const int ContinuousTurn = 2;
    public const int ContinuousSize = 3;

    public const int CommandBranch = 0;
    public const int TargetBranch = 1;
    public const int StanceBranch = 2;
    public const int DiscreteBranchCount = 3;

    public const int CommandNone = 0;
    public const int CommandBasicAttack = 1;
    public const int CommandSkill = 2;
    public const int CommandBranchSize = 3;

    public const int TargetBranchSize = BattleTeamConstants.MaxUnitsPerTeam;

    public const int StanceNeutral = 0;
    public const int StancePressure = 1;
    public const int StanceKeepRange = 2;
    public const int StanceBranchSize = 3;
}
