using System.Collections.Generic;
using UnityEngine;

// 12. 머리치기 (창) : 적 기절
public sealed class HeadStrikeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HeadStrike;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) =>
        caster.PlannedTargetEnemy != null
        && field.IsWithinEffectiveAttackDistance(caster.State, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        applier.ApplyDamage(caster.PlannedTargetEnemy, caster.Attack * 1.5f);
        applier.ApplyBuff(caster.PlannedTargetEnemy, BuffType.Stun, 1, 3f);
    }
}
