using UnityEngine;
using System.Collections.Generic;

// 6. 파이어볼 (스태프) : 화염구 (단일 강한 데미지로 임시 구현)
public sealed class FireballSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Fireball;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) =>
        caster.PlannedTargetEnemy != null && field.IsWithinEffectiveAttackDistance(caster, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        applier.ApplyDamage(caster.PlannedTargetEnemy.State, caster.Attack * 2.5f);
    }
}
