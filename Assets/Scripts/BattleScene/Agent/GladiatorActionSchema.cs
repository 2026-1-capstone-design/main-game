public static class GladiatorActionSchema
{
    public const int IntentMove = 0;
    public const int IntentAttack = 1;
    public const int IntentHold = 2;
    public const int IntentBranchSize = 3;

    public const int MoveNone = 0;
    public const int MoveForward = 1;
    public const int MoveBackward = 2;
    public const int MoveStrafeLeft = 3;
    public const int MoveStrafeRight = 4;
    public const int MoveKeepRange = 5;
    public const int MoveBranchSize = 6;

    public const int TargetBranchSize = BattleTeamConstants.MaxUnitsPerTeam;

    public const int RotateNone = 0;
    public const int RotateLeft = 1;
    public const int RotateRight = 2;
    public const int RotationBranchSize = 3;
}
