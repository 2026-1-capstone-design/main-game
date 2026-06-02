using System;
using System.Collections.Generic;
using UnityEngine;

// HeartAttack: "heartAttackArrow" 투사체를 발사하여 적중 시 공격력의 120% 데미지 + 50 넉백 + 크리티컬 이펙트를 발생시킵니다.
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
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime != null ? casterRuntime.State : null;
        BattleUnitCombatState targetState = context.PrimaryTarget != null ? context.PrimaryTarget.State : null;

        if (casterRuntime == null || targetState == null)
            return;

        Vector3 startPos = casterRuntime.Position + Vector3.up;
        Vector3 direction = targetState.Position - startPos;
        direction.y = 0f;

        Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitEffect = (hitTarget, hitPos, sink) =>
        {
            if (hitTarget == null || hitTarget.IsCombatDisabled)
                return;

            Vector3 pushDir = hitTarget.Position - casterState.Position;
            pushDir.y = 0f;

            sink?.DealDamage(
                new BattleDamageRequest
                {
                    Source = casterState,
                    Target = hitTarget,
                    Amount = casterState.Attack * 1.2f,
                    SourceKind = BattleEffectSourceKind.Skill,
                    DamageKind = BattleDamageKind.Direct,
                    SkillId = SkillId,
                    IsSkill = true,
                }
            );

            if (pushDir.sqrMagnitude > 0.0001f)
            {
                sink?.AddKnockback(hitTarget, pushDir.normalized, 50f);
            }

            VFXManager.Instance.PlayEffect("Critical Effect", hitPos);
        };

        float windUpDelay = casterRuntime.GetSkillAnimationDuration() * 0.5f;

        context.SimulationManager?.LaunchCustomProjectile(
            new BattleDamageRequest { Source = casterState, Target = targetState },
            startPos,
            direction,
            15f,
            "heartAttackArrow",
            windUpDelay,
            onHitEffect
        );
    }
}
