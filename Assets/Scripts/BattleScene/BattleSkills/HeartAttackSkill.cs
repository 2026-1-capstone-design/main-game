using System.Collections.Generic;
using UnityEngine;

// HeartAttack: 플랜된 적 대상에게 20 데미지 + 50 노크백. 한손검/양손검 계열.
public sealed class HeartAttackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HeartAttack;
    public skillType SkillCategory => skillType.attack;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand, WeaponType.twoHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        if (caster == null)
            return false;

        BattleUnitCombatState target = caster.PlannedTargetEnemy;
        return BattleFieldSnapshot.IsValidEnemyTarget(caster, target)
            && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(caster, target);
    }

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        BattleUnitCombatState target = context.PrimaryTarget != null ? context.PrimaryTarget.State : null;
        if (target == null)
            return;
        Vector3 pushDir = target.Position - caster.Position;
        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = caster,
                Target = target,
                Amount = 20f,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
        effects.AddKnockback(target, pushDir, 50f);
    }
}
