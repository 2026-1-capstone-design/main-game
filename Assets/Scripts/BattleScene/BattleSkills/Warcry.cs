using UnityEngine;
using System.Collections.Generic;

// 3. 워크라이 (두손검) : 아군 전체 공격력 증가
public sealed class WarcrySkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Warcry;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };
    public bool CanActivate(BattleRuntimeUnit caster, BattleFieldView field) => true;

    public void Apply(BattleRuntimeUnit caster, BattleFieldView field, ISkillEffectApplier applier)
    {
        var sim = applier as BattleSimulationManager;
        if (sim != null)
        {
            foreach (var unit in sim.RuntimeUnits)
            {
                if (unit != null && !unit.IsCombatDisabled && unit.IsEnemy == caster.IsEnemy)
                {
                    applier.ApplyBuff(unit.State, BuffType.AttackDamage, 3, 10f); // 공격력 +30
                }
            }
        }
    }
}
