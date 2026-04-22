using System.Collections.Generic;
using UnityEngine;

// HeartAttack: 플랜된 적 대상에게 20 데미지 + 50 노크백. 한손검/양손검 계열.
public sealed class HeartAttackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HeartAttack;
    public skillType SkillCategory => skillType.attack;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand, WeaponType.twoHand };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field)
    {
        BattleUnitCombatState target = caster.PlannedTargetEnemy;
        return field.IsValidEnemyTarget(caster.State, target)
            && field.IsWithinEffectiveAttackDistance(caster.State, target);
    }

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        BattleUnitCombatState target = caster.PlannedTargetEnemy;
        if (target == null)
            return;
        Vector3 pushDir = target.Position - caster.Position;
        applier.ApplyDamage(target, 20f);
        applier.AddKnockback(target, pushDir, 50f);
    }
}
