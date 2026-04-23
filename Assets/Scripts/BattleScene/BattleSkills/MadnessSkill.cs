using System.Collections.Generic;

// Madness: 자신에게 AttackSpeed 버프 적용. 모든 무기에 호환 (enhance 타입).
public sealed class MadnessSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Madness;
    public skillType SkillCategory => skillType.enhance;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new WeaponType[0];

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => true;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        applier.ApplyBuff(caster.State, BuffType.AttackSpeed, 2, 20f);
    }
}
