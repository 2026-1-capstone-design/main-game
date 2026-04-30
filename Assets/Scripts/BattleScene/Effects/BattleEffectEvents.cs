// 치유 적용 후 장신구 반응 훅에 전달되는 결과 값이다.
// 요청량과 최종 적용량을 분리해 후속 치유 보정/로그 확장에 사용할 수 있게 한다.
public readonly struct BattleHealResult
{
    public BattleUnitCombatState Source { get; }
    public BattleUnitCombatState Target { get; }
    public float RequestedAmount { get; }
    public float FinalAmount { get; }

    public BattleHealResult(
        BattleUnitCombatState source,
        BattleUnitCombatState target,
        float requestedAmount,
        float finalAmount
    )
    {
        Source = source;
        Target = target;
        RequestedAmount = requestedAmount;
        FinalAmount = finalAmount;
    }
}

// 유닛 사망이 확정된 뒤 장신구 반응 훅에 전달되는 이벤트다.
// Killer는 출처가 없는 피해나 환경 피해일 경우 null일 수 있다.
public readonly struct BattleKillEvent
{
    public BattleUnitCombatState Killer { get; }
    public BattleUnitCombatState Victim { get; }

    public BattleKillEvent(BattleUnitCombatState killer, BattleUnitCombatState victim)
    {
        Killer = killer;
        Victim = victim;
    }
}

// 스킬 Activate가 성공한 뒤 장신구 반응 훅에 전달되는 이벤트다.
// 런타임 뷰와 스냅샷을 함께 담아 후속 효과가 대상/주변 유닛을 조회할 수 있게 한다.
public readonly struct BattleSkillCastEvent
{
    public BattleUnitCombatState Caster { get; }
    public BattleRuntimeUnit CasterView { get; }
    public WeaponSkillId SkillId { get; }
    public BattleRuntimeUnit PrimaryTarget { get; }
    public BattleFieldSnapshot Snapshot { get; }

    public BattleSkillCastEvent(
        BattleUnitCombatState caster,
        BattleRuntimeUnit casterView,
        WeaponSkillId skillId,
        BattleRuntimeUnit primaryTarget,
        BattleFieldSnapshot snapshot
    )
    {
        Caster = caster;
        CasterView = casterView;
        SkillId = skillId;
        PrimaryTarget = primaryTarget;
        Snapshot = snapshot;
    }
}
