using System.Collections.Generic;
using UnityEngine;

public sealed class SurgeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Surge;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 20f;
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

        effects.Teleport(casterState, targetState.Position);
        effects.RosterMutations.DisableCommandAndSkill(casterRuntime, 1.0f);

        for (int i = 0; i < 3; i++)
        {
            float delay = i * 0.3f;
            bool isLastHit = (i == 2);

            effects.ScheduleEffect(
                delay,
                casterRuntime,
                targetRuntime,
                context,
                (ctx, sink) =>
                {
                    if (casterState.IsCombatDisabled || targetState.IsCombatDisabled)
                        return;

                    sink.DealDamage(
                        new BattleDamageRequest
                        {
                            Source = casterState,
                            Target = targetState,
                            Amount = casterState.Attack * 0.8f,
                            SourceKind = BattleEffectSourceKind.Skill,
                            DamageKind = BattleDamageKind.Direct,
                            SkillId = SkillId,
                            IsSkill = true,
                        }
                    );
                    VFXManager.Instance.PlayEffect("CompactHit", targetState.Position);

                    if (isLastHit)
                    {
                        sink.ApplyStatus(
                            new BattleStatusRequest
                            {
                                Source = casterState,
                                Target = targetState,
                                Type = BattleStatusType.Stun,
                                Level = 1,
                                Duration = 2f,
                                IsDebuff = true,
                                IsDispelAllowed = true,
                            }
                        );
                        VFXManager.Instance.PlayEffect("SurgeStun", targetState.Position);
                    }
                }
            );
        }
    }
}
