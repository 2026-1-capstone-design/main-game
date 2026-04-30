using System.Collections.Generic;
using UnityEngine;

// 11. 방패 밀치기 (쉴드) : 강한 넉백 및 적 공격력 감소
public sealed class ShieldBashSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ShieldBash;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
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
        if (target == null)
            return;

        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = caster,
                Target = target,
                Amount = caster.Attack * 1.0f,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
        Vector3 pushDir = target.Position - caster.Position;
        effects.AddKnockback(target, pushDir, 120f); // 강력한 넉백

        effects.ApplyBuff(caster, target, BuffType.AttackDamage, -3, 6f);
    }
}
