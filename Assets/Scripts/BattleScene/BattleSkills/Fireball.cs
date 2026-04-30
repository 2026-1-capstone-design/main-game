using System.Collections.Generic;

// 6. 파이어볼 (스태프) : 화염구 (단일 강한 데미지로 임시 구현)
public sealed class FireballSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Fireball;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null
        && context.Actor.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(context.Actor.State, context.Actor.PlannedTargetEnemy);

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = caster,
                Target = context.PrimaryTarget != null ? context.PrimaryTarget.State : null,
                Amount = caster.Attack * 2.5f,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
    }
}
