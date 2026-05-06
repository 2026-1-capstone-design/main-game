public static class GladiatorObservationSchema
{
    public const int SelfSize = 41;
    public const int TeammateSlotSize = 8;
    public const int OpponentSlotSize = 9; // +1 for IsTargetingMeAggressively
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1;
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam;
    public const int TotalSize = SelfSize + (TeammateSlots * TeammateSlotSize) + (OpponentSlots * OpponentSlotSize);
}

public enum GladiatorSelfObservationIndex
{
    ArenaCenterWorldDeltaX = 0,
    ArenaCenterWorldDeltaZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,
    AnchorDistanceRatio = 8,
    AnchorVisibility = 9,
    AnchorThreatToSelfRatio = 10,
    SelfThreatToAnchorRatio = 11,
    AnchorInSelfRange = 12,
    SelfInAnchorRange = 13,
    LeftLaneFreeRatio = 14,
    RightLaneFreeRatio = 15,
    AllyUnderFocusRatio = 16,
    EnemyClusterPressure = 17,
    BoundaryPressure = 18,
    BattleTimeoutRemainingRatio = 19,
    AgentSmoothedWorldMoveX = 20,
    AgentSmoothedWorldMoveZ = 21,
    AgentPreviousRawWorldMoveX = 22,
    AgentPreviousRawWorldMoveZ = 23,
    AnchorKindEnemy = 24,
    AnchorKindAlly = 25,
    AnchorKindTeamCenter = 26,
    PathModeDirect = 27,
    PathModeFlankLeft = 28,
    PathModeFlankRight = 29,
    PathModeRegroup = 30,
    RoleEngage = 31,
    RolePeel = 32,
    RoleAssassinate = 33,
    RoleRegroup = 34,
    AnchorCommitmentRatio = 35,
    RoleCommitmentRatio = 36,
    AnchorAllySupportPressure = 37,
    AnchorEnemyFocusPressure = 38,
    AnchorEnemyIsolation = 39,
    AnchorEnemyRetreatSignal = 40,
}

public enum GladiatorUnitObservationIndex
{
    WorldRelativePositionX = 0,
    WorldRelativePositionZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,

    // 적군 슬롯(OpponentSlotSize)에만 존재. 이 유닛이 자신을 타겟으로 하고 Neutral/Pressure 태세일 때 1.
    IsTargetingMeAggressively = 8,
}
