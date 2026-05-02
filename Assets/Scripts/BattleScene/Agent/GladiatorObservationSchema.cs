public static class GladiatorObservationSchema
{
    public const int SelfSize = 25;
    public const int UnitSlotSize = 8;
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1;
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam;
    public const int TotalSize = SelfSize + (TeammateSlots * UnitSlotSize) + (OpponentSlots * UnitSlotSize);
}

public enum GladiatorSelfObservationIndex
{
    ArenaCenterLocalX = 0,
    ArenaCenterLocalZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,
    DamageToNearestEnemyMaxHealthRatio = 8,
    NearestEnemyDamageToSelfMaxHealthRatio = 9,
    NearestOpponentDistanceRatio = 10,
    CanHitNearestOpponent = 11,
    InNearestOpponentRange = 12,
    NearbyOpponentRatio = 13,
    NearbyTeammateRatio = 14,
    BoundaryPressure = 15,
    BattleTimeoutRemainingRatio = 16,
    AgentSmoothedLocalMoveX = 17,
    AgentSmoothedLocalMoveZ = 18,
    AgentSmoothedTurn = 19,
    AgentPreviousRawLocalMoveX = 20,
    AgentPreviousRawLocalMoveZ = 21,
    AgentPreviousRawTurn = 22,
    HasReadySkill = 23,
    HasTarget = 24,
}

public enum GladiatorUnitObservationIndex
{
    LocalPositionX = 0,
    LocalPositionZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,
}
