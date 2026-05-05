public static class GladiatorActionSchema
{
    public const int ContractVersion = 9;

    public const int ContinuousAnchorStrafe = 0;
    public const int ContinuousAnchorForward = 1;
    public const int ContinuousSize = 2;

    public const int CommandBranch = 0;
    public const int StanceBranch = 1;
    public const int PathModeBranch = 2;
    public const int AnchorKindBranch = 3;
    public const int AnchorSlotBranch = 4;
    public const int DiscreteBranchCount = 5;

    public const int CommandNone = 0;
    public const int CommandBasicAttack = 1;
    public const int CommandBranchSize = 2;

    public const int StanceNeutral = 0;
    public const int StancePressure = 1;
    public const int StanceKeepRange = 2;
    public const int StanceBranchSize = 3;

    public const int PathModeDirect = 0;
    public const int PathModeFlankLeft = 1;
    public const int PathModeFlankRight = 2;
    public const int PathModeRegroup = 3;
    public const int PathModeBranchSize = 4;

    public const int AnchorKindEnemy = 0;
    public const int AnchorKindAlly = 1;
    public const int AnchorKindTeamCenter = 2;
    public const int AnchorKindBranchSize = 3;

    public const int AnchorSlotBranchSize = BattleTeamConstants.MaxUnitsPerTeam;
}
