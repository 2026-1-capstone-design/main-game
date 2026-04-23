using UnityEngine;
using System.Collections.Generic;

// 7. 라이트닝 (스태프) : 타겟 주변 적군 전체 데미지
public sealed class LightningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Lightning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => caster.PlannedTargetEnemy != null;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        BattleRuntimeUnit target = caster.PlannedTargetEnemy;
        if (target == null) return;

        var sim = applier as BattleSimulationManager;
        if (sim != null)
        {
            foreach (var unit in sim.RuntimeUnits)
            {
                if (unit != null && !unit.IsCombatDisabled && unit.IsEnemy != caster.IsEnemy)
                {
                    if (Vector3.Distance(target.Position, unit.Position) <= 40f) // 광역 반경 40
                    {
                        applier.ApplyDamage(unit.State, caster.Attack * 1.5f);
                    }
                }
            }
        }
    }
}
