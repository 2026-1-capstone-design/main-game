using System.Collections.Generic;

// 10. 총검술 (라이플) : 공속/공격/이속 상승 & 사거리 감소
public sealed class BayonetChargeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.BayonetCharge;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.rifle };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        effects.ApplyBuff(caster, caster, BuffType.AttackSpeed, 3, 10f);
        effects.ApplyBuff(caster, caster, BuffType.AttackDamage, 2, 10f);
        effects.ApplyBuff(caster, caster, BuffType.MoveSpeed, 3, 10f);
        effects.ApplyBuff(caster, caster, BuffType.AttackRange, -20, 10f); // 음수를 넣어 사거리 대폭 감소!
    }
}
