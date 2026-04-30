using System.Collections.Generic;

// 12. 머리치기 (창) : 적 기절
public sealed class HeadStrikeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HeadStrike;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };
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
                Amount = caster.Attack * 1.5f,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
        effects.ApplyBuff(caster, target, BuffType.Stun, 1, 3f);
    }
}
