using System.Collections.Generic;

// 6. 파이어볼 (스태프) : 화염구 (단일 강한 데미지로 임시 구현)
public sealed class FireballSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Fireball;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };

    public bool CanActivate(BattleRuntimeUnit caster) =>
        caster.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(caster.State, caster.PlannedTargetEnemy);

    public void Apply(BattleRuntimeUnit caster, ISkillEffectApplier applier)
    {
        applier.ApplyDamage(caster.PlannedTargetEnemy, caster.Attack * 2.5f);
    }
}
