using System.Collections.Generic;

// 3. 워크라이 (두손검) : 아군 전체 공격력 증가
public sealed class WarcrySkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Warcry;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };

    public bool CanActivate(BattleRuntimeUnit caster) => true;

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        foreach (var unit in applier.AllUnits)
        {
            if (unit != null && !unit.IsCombatDisabled && unit.IsEnemy == caster.IsEnemy)
            {
                applier.ApplyBuff(unit, BuffType.AttackDamage, 3, 10f); // 공격력 +30
            }
        }
    }
}
