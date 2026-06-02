using System.Collections.Generic;
using UnityEngine;

// [패링] 쉴드: 2초간 정지 및 피해량 대폭 감소 상태가 된 후, 주변 적에게 광역 기절과 피해를 입혀 반격을 구현한다.
public sealed class ParryingSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Parrying;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 5f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled)
            return;

        effects.RosterMutations.DisableCommandAndSkill(casterRuntime, 2.0f);
        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = casterState,
                Target = casterState,
                Type = BattleStatusType.DamageReductionPercent,
                Level = 90,
                Duration = 2f,
                IsDebuff = false,
                IsDispelAllowed = false,
            }
        );
        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = casterState,
                Target = casterState,
                Type = BattleStatusType.Taunt,
                Level = 1,
                Duration = 2f,
                IsDebuff = false,
                IsDispelAllowed = false,
            }
        );
        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = casterState,
                Target = casterState,
                Type = BattleStatusType.Stun,
                Level = 90,
                Duration = 2f,
                IsDebuff = true,
                IsDispelAllowed = false,
            }
        );

        GameObject ActiveVFX = VFXManager.Instance.PlayEffect("LightStance", casterState.Position + Vector3.up);

        effects.ScheduleEffect(
            2.0f,
            casterRuntime,
            casterRuntime,
            context,
            (ctx, sink) =>
            {
                VFXManager.Instance.StopEffect(ActiveVFX);

                if (casterState.IsCombatDisabled)
                    return;

                foreach (BattleRuntimeUnit unitView in ctx.Units)
                {
                    BattleUnitCombatState target = unitView?.State;
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, target))
                        continue;

                    if (Vector3.Distance(casterState.Position, target.Position) <= AreaRadius)
                    {
                        VFXManager.Instance.PlayEffect("LightHit", target.Position + Vector3.up);

                        sink.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = casterState,
                                Target = target,
                                Amount = casterState.Attack * 0.8f,
                                SourceKind = BattleEffectSourceKind.Skill,
                                DamageKind = BattleDamageKind.Area,
                                SkillId = SkillId,
                                IsSkill = true,
                                IsArea = true,
                            }
                        );
                        sink.ApplyStatus(
                            new BattleStatusRequest
                            {
                                Source = casterState,
                                Target = target,
                                Type = BattleStatusType.Stun,
                                Level = 3,
                                Duration = 3.0f,
                                IsDebuff = true,
                                IsDispelAllowed = true,
                            }
                        );
                    }
                }
            }
        );
    }
}
