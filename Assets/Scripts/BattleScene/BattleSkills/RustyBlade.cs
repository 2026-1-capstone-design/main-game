using System.Collections.Generic;
using UnityEngine;

// 5. 녹슨 칼날 (단검) : 출혈 부여
public sealed class RustyBladeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.RustyBlade;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) =>
        caster.PlannedTargetEnemy != null && field.IsWithinEffectiveAttackDistance(caster, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        applier.ApplyDamage(caster.PlannedTargetEnemy.State, caster.Attack);
        applier.ApplyBuff(caster.PlannedTargetEnemy.State, BuffType.BleedDamage, 1, 5f);
    }
}
