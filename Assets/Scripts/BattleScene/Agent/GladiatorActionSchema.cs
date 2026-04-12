public static class GladiatorActionSchema
{
    public const int Idle = 0;
    public const int Forward = 1;
    public const int AttackStart = 2;
    public const int AttackSlotCount = BattleTeamConstants.MaxUnitsPerTeam;
    public const int MoveAttackBranchSize = AttackStart + AttackSlotCount;
    public const int RotationBranchSize = 3;
}
