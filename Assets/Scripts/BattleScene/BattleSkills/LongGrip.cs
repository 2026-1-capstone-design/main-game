using System.Collections.Generic;

public sealed class LongGripSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.LongGrip;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };

    public bool CanActivate(BattleRuntimeUnit caster) => true;

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        applier.ApplyBuff(caster.State, BuffType.AttackRange, 5, 10f); // 사거리 +2.5
    }
}
