using System.Collections.Generic;
using UnityEngine;

// 9. 리볼버 패닝 (권총) : 6번 연속 공격
public sealed class RevolverFanningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.RevolverFanning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.handGun };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) =>
        caster.PlannedTargetEnemy != null
        && field.IsWithinEffectiveAttackDistance(caster.State, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        var target = caster.PlannedTargetEnemy;
        if (target == null)
            return;

        // 6연발 데미지 적용
        for (int i = 0; i < 6; i++)
        {
            applier.ApplyDamage(target, caster.Attack * 0.5f);
        }
    }
}
