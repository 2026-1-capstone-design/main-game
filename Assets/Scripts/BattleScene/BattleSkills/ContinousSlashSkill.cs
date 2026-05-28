using System.Collections.Generic;
using UnityEngine;

// [계속베기] 두손검: 일정 시간 동안 제자리에 서서 연속 피해를 입히고 적의 이속을 크게 감소시킨다.
public sealed class ContinuousSlashSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ContinuousSlash;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 5f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        BattleRuntimeUnit targetRuntime = context.PrimaryTarget;
        BattleUnitCombatState targetState = targetRuntime?.State;

        if (casterState == null || targetState == null || targetState.IsCombatDisabled)
            return;

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

        for (int i = 0; i < 4; i++)
        {
            float delay = i * 0.5f;
            effects.ScheduleEffect(
                casterState.SkillDuration * 0.4f + delay,
                casterRuntime,
                targetRuntime,
                context,
                (ctx, sink) =>
                {
                    if (targetState.IsCombatDisabled || casterState.IsCombatDisabled)
                        return;

                    sink.DealDamage(
                        new BattleDamageRequest
                        {
                            Source = casterState,
                            Target = targetState,
                            Amount = casterState.Attack * 1.5f,
                            SourceKind = BattleEffectSourceKind.Skill,
                            DamageKind = BattleDamageKind.Direct,
                            SkillId = SkillId,
                            IsSkill = true,
                        }
                    );

                    sink.ApplyStatus(
                        new BattleStatusRequest
                        {
                            Source = casterState,
                            Target = targetState,
                            Type = BattleStatusType.Slow,
                            Level = -40,
                            Duration = 1.5f,
                            IsDebuff = true,
                            IsDispelAllowed = true,
                        }
                    );
                    VFXManager.Instance.PlayEffect("CriticalHit", targetState.Position + Vector3.up);
                }
            );
        }
    }
}
