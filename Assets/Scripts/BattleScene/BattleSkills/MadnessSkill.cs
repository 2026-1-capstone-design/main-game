using System.Collections.Generic;

// Madness: 자신에게 AttackSpeed 버프 적용. 모든 무기에 호환 (enhance 타입).
public sealed class MadnessSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Madness;
    public skillType SkillCategory => skillType.enhance;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new WeaponType[0];
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        effects.ApplyBuff(caster, caster, BuffType.AttackSpeed, 2, 20f);
    }
}
