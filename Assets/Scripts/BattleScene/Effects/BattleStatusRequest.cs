using System;

// 전투 중 지속시간을 갖는 범용 상태 타입이다.
// 기존 BuffType은 이 타입으로 변환되어 같은 저장/해제/갱신 경로를 사용한다.
public enum BattleStatusType
{
    AttackDamage,
    AttackSpeed,
    MoveSpeed,
    AttackRange,
    DamageTakenPercent,
    SkillDisabled,
    Stun,
    Bleed,
    Taunt,
    Slow,
    DamageReductionPercent,
}

// 상태 적용 요청 DTO다.
// 효과 구현체는 State를 직접 수정하지 않고 sink를 통해 이 요청을 전달한다.
public struct BattleStatusRequest
{
    public BattleUnitCombatState Source;
    public BattleUnitCombatState Target;
    public BattleStatusType Type;
    public int Level;
    public float Duration;
    public bool IsDebuff;
    public bool IsDispelAllowed;
}

// 디스펠 대상 범위를 지정하는 필터다.
// RemoveDebuffs/RemoveBuffs로 긍정/부정 상태를 고르고, DispelOnlyAllowed로 해제 가능 상태만 제한한다.
public struct BattleDispelFilter
{
    public bool RemoveDebuffs;
    public bool RemoveBuffs;
    public bool DispelOnlyAllowed;
}

// 상태 지속시간 갱신 대상 범위를 지정하는 필터다.
// Type이 null이면 버프/디버프 포함 조건에 맞는 모든 상태가 대상이 된다.
public struct BattleStatusFilter
{
    public bool IncludeDebuffs;
    public bool IncludeBuffs;
    public BattleStatusType? Type;
}

// BattleUnitCombatState 내부에 저장되는 실제 상태 인스턴스다.
// 같은 타입의 상태가 여러 개 쌓일 수 있으므로 Level과 RemainingDuration을 인스턴스 단위로 보관한다.
[Serializable]
public struct BattleStatusInstance
{
    // BattleUnitCombatState._statuses → Source 순환 참조로 Unity 직렬화 깊이 초과 경고가 발생해 제외한다.
    [System.NonSerialized]
    public BattleUnitCombatState Source;
    public BattleStatusType Type;
    public int Level;
    public float RemainingDuration;
    public bool IsDebuff;
    public bool IsDispelAllowed;
}
