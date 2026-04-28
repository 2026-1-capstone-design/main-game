public static class GladiatorObservationSchema
{
    public const int SelfSize = 24;
    public const int UnitSlotSize = 7;
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1;
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam;
    public const int TotalSize = SelfSize + (TeammateSlots * UnitSlotSize) + (OpponentSlots * UnitSlotSize);
}
