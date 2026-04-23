using UnityEngine;
using System.Collections.Generic;

// 13. 나선 베기 (한손검) : 자신 기준 광역 데미지 + 넉백
public sealed class SpiralSlashSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.SpiralSlash;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => true;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        var sim = applier as BattleSimulationManager;
        if (sim != null)
        {
            foreach (var unit in sim.RuntimeUnits)
            {
                if (unit != null && !unit.IsCombatDisabled && unit.IsEnemy != caster.IsEnemy)
                {
                    if (Vector3.Distance(caster.Position, unit.Position) <= 45f) // 광역 반경 45
                    {
                        applier.ApplyDamage(unit.State, caster.Attack * 1.2f);
                        Vector3 pushDir = unit.Position - caster.Position;
                        applier.AddKnockback(unit.State, pushDir, 80f);
                    }
                }
            }
        }
    }
}
