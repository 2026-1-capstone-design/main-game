// 관측 벡터 차원 정의.
//
// 설계 원칙: Stage 1~4까지 사용할 모든 차원을 미리 박아두고, 현재 단계에서 사용하지 않는
// 항목은 builder에서 0으로 채운다. ML-Agents의 BehaviorParameters.VectorObservationSize는
// 학습 도중 변경할 수 없고 weight 재사용도 막히므로, 단계 진행 시 차원을 늘리지 않는다.
//
// PPT 명세 매핑:
//   - 정량적 환경 데이터 → SelfSegment + UnitSlots
//   - 자신의 바라보는 방향 → SelfYawSinCos
//   - 공격/스킬 쿨타임, 스킬 유형/사거리 → SelfCooldowns + SelfSkillRange + SkillTypeOneHot
//   - 무기 종류 → WeaponOneHot
//   - 성격 특성 6종 → PersonalitySize (Stage 2+)
//   - 플레이어 명령 파라미터 → OrderSize (Stage 3+)
//
// PPT에 명시되지 않은 보강 항목:
//   - LastActionEncoding (직전 (Intent, Move, Rotate)): 지그재그 방지 (POMDP 대응)
//   - Bias unit: 정책 학습 안정화
public static class GladiatorObservationSchema
{
    // ─── Self segment (43) ──────────────────────────────────────
    public const int SelfYawSinCos = 2;
    public const int SelfHealthNormalized = 2; // health ratio, 1-ratio
    public const int SelfStats = 5; // hp, maxHp, atk, attackRange, moveSpeed
    public const int SelfCooldowns = 2; // attack cooldown, skill cooldown
    public const int SelfSkillRange = 1;
    public const int WeaponOneHot = 12; // WeaponType enum 12종 (None=0 포함)
    public const int SkillTypeOneHot = 4; // attack/tank/support/enhance (None=zeros)
    public const int BiasUnit = 1;
    public const int LastIntentOneHot = 5;
    public const int LastMoveOneHot = 6;
    public const int LastRotateOneHot = 3;
    public const int LastActionEncoding = LastIntentOneHot + LastMoveOneHot + LastRotateOneHot;

    public const int SelfSize =
        SelfYawSinCos
        + SelfHealthNormalized
        + SelfStats
        + SelfCooldowns
        + SelfSkillRange
        + WeaponOneHot
        + SkillTypeOneHot
        + BiasUnit
        + LastActionEncoding;

    // ─── Self situational (8) ──────────────────────────────────
    public const int ArenaRelativePos = 2;
    public const int NearestEnemyDistance = 1;
    public const int InRangeEnemyExists = 1;
    public const int CrowdRatios = 2; // enemy near ratio, ally near ratio
    public const int BoundaryPressure = 1;
    public const int TeamSizeNormalized = 1;
    public const int SelfSituationalSize =
        ArenaRelativePos
        + NearestEnemyDistance
        + InRangeEnemyExists
        + CrowdRatios
        + BoundaryPressure
        + TeamSizeNormalized;

    // ─── Per unit slot (9) ─────────────────────────────────────
    // x, z, hp ratio, maxHp normalized, atk, atkRange, moveSpeed, ranged binary, alive flag
    public const int UnitSlotSize = 9;
    public const int TeammateSlots = BattleTeamConstants.MaxUnitsPerTeam - 1; // 5
    public const int OpponentSlots = BattleTeamConstants.MaxUnitsPerTeam; // 6

    // ─── Personality (6) — Stage 2+ ────────────────────────────
    // [공격성, 포위 압박감, 이타심, 약자멸시, 고립 민감도, 피해 민감도]
    public const int PersonalitySize = 6;

    // ─── Order parameters (11) — Stage 3+ ──────────────────────
    public const int OrderStrategyOneHot = 5; // 공격/지원/도주/대기/암살
    public const int OrderTargetSlot = 1; // 정규화된 타겟 슬롯 인덱스
    public const int OrderPersonalityDelta = 5; // 5개 성격 변화율(이타심 제외)
    public const int OrderSize = OrderStrategyOneHot + OrderTargetSlot + OrderPersonalityDelta;

    public const int TotalSize =
        SelfSize
        + SelfSituationalSize
        + (TeammateSlots * UnitSlotSize)
        + (OpponentSlots * UnitSlotSize)
        + PersonalitySize
        + OrderSize;
    // 43 + 8 + 45 + 54 + 6 + 11 = 167
}
