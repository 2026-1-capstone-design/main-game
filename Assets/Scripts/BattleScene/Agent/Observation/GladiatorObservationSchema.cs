// Observation (147 floats):
//   자신      (42):     월드 좌표축 기준 정규화된 경기장 중심 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비,
//                       정규화된 사거리/이동속도/공격 쿨타임, 최근접 적/자신 대상 피해비, 최근접 적 거리,
//                       공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박, role/commitment/anchor relation 요약,
//                       timeout까지 남은 시간 비율, 현재/직전 agent 월드 이동 입력, anchor kind/slot/path/role one-hot
//   내 팀 동료 (5 × 8): 월드 좌표축 기준 정규화된 상대좌표(x,z), 체력비, 최대 체력 로그비, 공격력 로그비, 사거리, 이동속도, 공격 쿨타임
//   상대팀    (6 × 9): 위 동일 + 자신을 Neutral/Pressure 태세로 노리고 있는지 여부
public static class GladiatorObservationSchema
{
    public const int SelfSize = 42;
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
    CommandMove = 21,
    CommandAttack = 22,
    CommandWithdraw = 23,
    CurrentAnchorSlot0 = 24,
    CurrentAnchorSlot1 = 25,
    CurrentAnchorSlot2 = 26,
    CurrentAnchorSlot3 = 27,
    CurrentAnchorSlot4 = 28,
    CurrentAnchorSlot5 = 29,
    StrategyNeutral = 30,
    StrategyPressure = 31,
    StrategyKeepRange = 32,
    StrategyRetreat = 33,
    AnchorCommitmentRatio = 34,
    StrategyCommitmentRatio = 35,
    AnchorAllySupportPressure = 36,
    AnchorEnemyFocusPressure = 37,
    AnchorEnemyIsolation = 38,
    AnchorEnemyRetreatSignal = 39,
    PersonalityCollectivism = 40,
    PersonalityPassiveness = 41,
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
