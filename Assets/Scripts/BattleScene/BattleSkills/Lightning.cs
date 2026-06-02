using System.Collections.Generic;
using UnityEngine;

// 7. 라이트닝 (스태프) : 타겟 주변 적군 전체 데미지
public sealed class LightningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Lightning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundTarget;
    public float CastRange => 0f;
    public float AreaRadius => 40f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime != null ? casterRuntime.State : null;
        BattleUnitCombatState targetState = context.PrimaryTarget != null ? context.PrimaryTarget.State : null;

        if (casterRuntime == null || targetState == null)
            return;

        foreach (BattleRuntimeUnit unitView in context.Units)
        {
            BattleUnitCombatState unitState = unitView != null ? unitView.State : null;

            if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, unitState))
                continue;

            if (Vector3.Distance(targetState.Position, unitState.Position) <= 40f)
            {
                effects.ScheduleEffect(
                    0.5f,
                    casterRuntime,
                    unitView,
                    context,
                    (delayedContext, delayedSink) =>
                    {
                        if (unitState == null || unitState.IsCombatDisabled)
                            return;

                        delayedSink?.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = casterState,
                                Target = unitState,
                                Amount = casterState.Attack * 0.8f,
                                SourceKind = BattleEffectSourceKind.Skill,
                                DamageKind = BattleDamageKind.Area,
                                SkillId = SkillId,
                                IsSkill = true,
                                IsArea = true,
                            }
                        );

                        VFXManager.Instance.PlayEffect("LightningHit", unitState.Position + (Vector3.up * 1.5f));
                    }
                );
            }
        }
    }
}
