using System.Collections.Generic;

public sealed class LongGripSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.LongGrip;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        effects.ApplyBuff(caster, caster, BuffType.AttackRange, 5, 10f); // 사거리 +2.5
    }
}
