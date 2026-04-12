public static class GladiatorObservationSchema
{
    public const int SelfSize = 43;
    public const int TeammateSlotSize = 9;
    public const int OpponentSlotSize = 10; // +1 for IsTargetingMeAggressively
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1;
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam;
    public const int TotalSize = SelfSize + (TeammateSlots * TeammateSlotSize) + (OpponentSlots * OpponentSlotSize);
}

public enum GladiatorSelfObservationIndex
{
    ArenaCenterAnchorRelativeX = 0,
    ArenaCenterAnchorRelativeZ = 1,
    HealthRatio = 2,
    MaxHealthLogRatio = 3,
    AttackLogRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,
    AnchorThreatToSelfRatio = 8,
    SelfThreatToAnchorRatio = 9,
    AnchorInSelfRange = 10,
    SelfInAnchorRange = 11,
    LeftLaneFreeRatio = 12,
    RightLaneFreeRatio = 13,
    EnemyClusterPressure = 14,
    BoundaryPressure = 15,
    BattleTimeoutRemainingRatio = 16,
    AgentSmoothedMoveX = 17,
    AgentSmoothedMoveZ = 18,
    AgentPreviousRawMoveX = 19,
    AgentPreviousRawMoveZ = 20,
    AnchorKindEnemy = 21,
    AnchorKindAlly = 22,
    AnchorKindTeamCenter = 23,
    CurrentAnchorSlot0 = 24,
    CurrentAnchorSlot1 = 25,
    CurrentAnchorSlot2 = 26,
    CurrentAnchorSlot3 = 27,
    CurrentAnchorSlot4 = 28,
    CurrentAnchorSlot5 = 29,
    FightModeNeutral = 30,
    FightModePressure = 31,
    FightModeKeepRange = 32,
    FightModeRetreat = 33,
    RoleEngage = 34,
    RoleAssassinate = 35,
    RoleRegroup = 36,
    AnchorCommitmentRatio = 37,
    RoleCommitmentRatio = 38,
    AnchorAllySupportPressure = 39,
    AnchorEnemyFocusPressure = 40,
    AnchorEnemyIsolation = 41,
    AnchorEnemyRetreatSignal = 42,
}

public enum GladiatorUnitObservationIndex
{
    AnchorRelativePositionX = 0,
    AnchorRelativePositionZ = 1,
    DistanceToSelfRatio = 2,
    HealthRatio = 3,
    MaxHealthLogRatio = 4,
    AttackLogRatio = 5,
    AttackRangeRatio = 6,
    MoveSpeedRatio = 7,
    AttackCooldownRatio = 8,

    // 적군 슬롯(OpponentSlotSize)에만 존재. 이 유닛이 자신을 타겟으로 하고 KeepRange가 아닐 때 1.
    IsTargetingMeAggressively = 9,
}
