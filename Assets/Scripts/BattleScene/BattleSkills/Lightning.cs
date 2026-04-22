using System.Collections.Generic;
using UnityEngine;

// 7. 라이트닝 (스태프) : 타겟 주변 적군 전체 데미지
public sealed class LightningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Lightning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };

    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => caster.PlannedTargetEnemy != null;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        var target = caster.PlannedTargetEnemy;
        if (target == null)
            return;

        foreach (var unit in applier.AllUnits)
        {
            if (unit.IsCombatDisabled || unit.IsEnemy == caster.IsEnemy)
                continue;

            if (Vector3.Distance(target.Position, unit.Position) <= 40f)
                applier.ApplyDamage(unit, caster.Attack * 1.5f);
        }
    }
}
