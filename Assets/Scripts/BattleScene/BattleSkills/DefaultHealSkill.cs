using System.Collections.Generic;

// DefaultHealSkill: 스킬 없음(WeaponSkillId.None) 또는 미구현 스킬의 fallback.
// 자신에게 힐 10 적용.
public sealed class DefaultHealSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.None;
    public skillType SkillCategory => skillType.support;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new WeaponType[0];

    public bool CanActivate(BattleRuntimeUnit caster) => true;

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        applier.ApplyHeal(caster.State, 10f);
    }
}
