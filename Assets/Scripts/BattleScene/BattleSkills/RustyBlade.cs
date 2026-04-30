using System.Collections.Generic;

// 5. 녹슨 칼날 (단검) : 출혈 부여
public sealed class RustyBladeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.RustyBlade;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null
        && context.Actor.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(context.Actor.State, context.Actor.PlannedTargetEnemy);

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = caster,
                Target = target,
                Amount = caster.Attack,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
        effects.ApplyBuff(caster, target, BuffType.BleedDamage, 1, 5f);
    }
}
