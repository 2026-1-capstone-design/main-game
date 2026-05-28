using System.Collections.Generic;
using UnityEngine;

public sealed class UnstoppableForceSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.UnstoppableForce;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 25f;
    public float AreaRadius => 8f;

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

        float impactDelay = 1.5f;

        // 2. 지정된 시간(impactDelay) 이후에 람다식 내부의 효과들(이펙트, 데미지, 에어본)이 실행됩니다.
        effects.ScheduleEffect(
            impactDelay,
            casterRuntime,
            targetRuntime,
            context,
            (ctx, sink) =>
            {
                effects.Teleport(casterState, targetState.Position);
                // 그 사이 시전자나 타겟의 위치가 변했을 수 있으므로, 시전자의 현재 위치를 폭발 중심으로 잡습니다.
                Vector3 impactPosition = casterState.Position;

                VFXManager.Instance.PlayEffect("GroundHit", impactPosition);

                foreach (BattleRuntimeUnit unitView in ctx.Units)
                {
                    BattleUnitCombatState unitState = unitView?.State;
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, unitState))
                        continue;

                    // 범위 체크
                    if (Vector3.Distance(impactPosition, unitState.Position) <= AreaRadius)
                    {
                        sink.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = casterState,
                                Target = unitState,
                                Amount = casterState.Attack * 2.5f,
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
                                Target = unitState,
                                Type = BattleStatusType.Stun,
                                Level = 1,
                                Duration = 1.5f,
                                IsDebuff = true,
                                IsDispelAllowed = true,
                            }
                        );

                        // 이전에 수정한 위로 띄우는 에어본 적용
                        sink.AddKnockback(unitState, Vector3.up, 20f);
                    }
                }
            }
        );
    }
}
