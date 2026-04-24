using System.Collections.Generic;

// 5. 녹슨 칼날 (단검) : 출혈 부여
public sealed class RustyBladeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.RustyBlade;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };

    public bool CanActivate(BattleRuntimeUnit caster) =>
        caster.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(caster.State, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        applier.ApplyDamage(caster.PlannedTargetEnemy, caster.Attack);
        applier.ApplyBuff(caster.PlannedTargetEnemy, BuffType.BleedDamage, 1, 5f);
    }
}
