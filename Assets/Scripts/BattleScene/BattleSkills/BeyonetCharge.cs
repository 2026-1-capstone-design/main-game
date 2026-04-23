using System.Collections.Generic;

// 10. 총검술 (라이플) : 공속/공격/이속 상승 & 사거리 감소
public sealed class BayonetChargeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.BayonetCharge;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.rifle };

    public bool CanActivate(BattleRuntimeUnit caster) => true;

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        applier.ApplyBuff(caster.State, BuffType.AttackSpeed, 3, 10f);
        applier.ApplyBuff(caster.State, BuffType.AttackDamage, 2, 10f);
        applier.ApplyBuff(caster.State, BuffType.MoveSpeed, 3, 10f);
        applier.ApplyBuff(caster.State, BuffType.AttackRange, -20, 10f); // 음수를 넣어 사거리 대폭 감소!
    }
}
