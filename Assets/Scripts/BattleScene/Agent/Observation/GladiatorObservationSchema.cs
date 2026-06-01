// Observation (135 floats):
//   자신      (41):     월드 좌표축 기준 정규화된 경기장 중심 상대좌표(x,z), 전체 유닛 최대 체력 기준 현재 체력/공격력 비율,
//                       정규화된 사거리/이동속도/공격 쿨타임, 최근접 적/자신 대상 피해비, 최근접 적 거리,
//                       공격 가능 여부, 피격 위험 여부, 근처 적/아군 비율, 경계 압박, role/commitment/anchor relation 요약,
//                       timeout까지 남은 시간 비율, 현재/직전 agent 월드 이동 입력, anchor kind/slot/path/role one-hot
//   내 팀 동료 (5 × 8): 월드 좌표축 기준 정규화된 상대좌표(x,z), 거리, 전체 유닛 최대 체력 기준 현재 체력/공격력 비율, 사거리, 이동속도, 공격 쿨타임
//   상대팀    (6 × 9): 위 동일 + 자신을 Neutral/Pressure 태세로 노리고 있는지 여부
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
    ArenaCenterAnchorRelativeX = 0,
    ArenaCenterAnchorRelativeZ = 1,
    CurrentHealthToRosterMaxHealthRatio = 2,
    AttackToRosterMaxHealthRatio = 3,
    AttackRangeRatio = 4,
    MoveSpeedRatio = 5,
    AttackCooldownRatio = 6,
    AnchorThreatToSelfRatio = 7,
    SelfThreatToAnchorRatio = 8,
    AnchorInSelfRange = 9,
    SelfInAnchorRange = 10,
    LeftLaneFreeRatio = 11,
    RightLaneFreeRatio = 12,
    EnemyClusterPressure = 13,
    BoundaryPressure = 14,
    BattleTimeoutRemainingRatio = 15,
    AgentSmoothedMoveX = 16,
    AgentSmoothedMoveZ = 17,
    AgentPreviousRawMoveX = 18,
    AgentPreviousRawMoveZ = 19,
    CommandMove = 20,
    CommandAttack = 21,
    CommandWithdraw = 22,
    CurrentAnchorSlot0 = 23,
    CurrentAnchorSlot1 = 24,
    CurrentAnchorSlot2 = 25,
    CurrentAnchorSlot3 = 26,
    CurrentAnchorSlot4 = 27,
    CurrentAnchorSlot5 = 28,
    StrategyNeutral = 29,
    StrategyPressure = 30,
    StrategyKeepRange = 31,
    StrategyRetreat = 32,
    AnchorCommitmentRatio = 33,
    StrategyCommitmentRatio = 34,
    AnchorAllySupportPressure = 35,
    AnchorEnemyFocusPressure = 36,
    AnchorEnemyIsolation = 37,
    AnchorEnemyRetreatSignal = 38,
    PersonalityCollectivism = 39,
    PersonalityPassiveness = 40,
}

public enum GladiatorUnitObservationIndex
{
    AnchorRelativePositionX = 0,
    AnchorRelativePositionZ = 1,
    DistanceToSelfRatio = 2,
    CurrentHealthToRosterMaxHealthRatio = 3,
    AttackToRosterMaxHealthRatio = 4,
    AttackRangeRatio = 5,
    MoveSpeedRatio = 6,
    AttackCooldownRatio = 7,

    // 적군 슬롯(OpponentSlotSize)에만 존재. 이 유닛이 자신을 타겟으로 하고 KeepRange가 아닐 때 1.
    IsTargetingMeAggressively = 8,
}
