public static class GladiatorObservationSchema
{
    public const int CurrentTargetSlotObservationSize = BattleTeamConstants.MaxUnitsPerTeam;
    public const int CurrentStanceObservationSize = GladiatorActionSchema.StanceBranchSize;
    public const int SelfSize = 31;
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
    DamageToNearestEnemyMaxHealthRatio = 8,
    NearestEnemyDamageToSelfMaxHealthRatio = 9,
    NearestOpponentDistanceRatio = 10,
    CanHitNearestOpponent = 11,
    InNearestOpponentRange = 12,
    NearbyOpponentRatio = 13,
    NearbyTeammateRatio = 14,
    BoundaryPressure = 15,
    BattleTimeoutRemainingRatio = 16,
    AgentSmoothedWorldMoveX = 17,
    AgentSmoothedWorldMoveZ = 18,
    AgentPreviousRawWorldMoveX = 19,
    AgentPreviousRawWorldMoveZ = 20,
    HasTarget = 21,
    CurrentTargetSlot0 = 22,
    CurrentTargetSlot1 = 23,
    CurrentTargetSlot2 = 24,
    CurrentTargetSlot3 = 25,
    CurrentTargetSlot4 = 26,
    CurrentTargetSlot5 = 27,
    CurrentStanceNeutral = 28,
    CurrentStancePressure = 29,
    CurrentStanceKeepRange = 30,
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
