using System.Collections.Generic;
using UnityEngine;

public sealed class DimensionalRiftSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.DimensionalRift;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 10f; // 폭발 스턴 반경

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        BattleRuntimeUnit lowestAlly = null;
        float minHp = float.MaxValue;

        // 가장 체력이 낮은 아군 탐색 (본인 제외)
        foreach (var unit in context.Units)
        {
            BattleUnitCombatState unitState = unit?.State;
            if (unitState == null || unitState.IsCombatDisabled) continue;
            if (unitState.TeamId == casterState.TeamId && unit != casterRuntime)
            {
                if (unitState.CurrentHealth < minHp)
                {
                    minHp = unitState.CurrentHealth;
                    lowestAlly = unit;
                }
            }
        }

        // 맞바꿀 아군이 있다면 위치 교환
        if (lowestAlly != null)
        {
            Vector3 casterPos = casterState.Position;
            Vector3 allyPos = lowestAlly.State.Position;
            effects.Teleport(casterState, allyPos);
            effects.Teleport(lowestAlly.State, casterPos);
        }

        // 최대 체력 30% 피해 (자해)
        effects.DealDamage(new BattleDamageRequest
        {
            Source = casterState, Target = casterState,
            Amount = casterState.MaxHealth * 0.3f,
            SourceKind = BattleEffectSourceKind.Skill, DamageKind = BattleDamageKind.Direct
        });

        VFXManager.Instance.PlayEffect("DarkHit", casterState.Position + Vector3.up);

        // 도착한 위치 주변 적에게 큰 피해와 스턴
        foreach (var unit in context.Units)
        {
            BattleUnitCombatState enemyState = unit?.State;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, enemyState)) continue;


            VFXManager.Instance.PlayEffect("DarkHealEffect", casterState.Position + Vector3.up);
            if (Vector3.Distance(casterState.Position, enemyState.Position) <= AreaRadius)
            {
                effects.DealDamage(new BattleDamageRequest
                {
                    Source = casterState, Target = enemyState,
                    Amount = casterState.Attack * 3.0f,
                    SourceKind = BattleEffectSourceKind.Skill, DamageKind = BattleDamageKind.Area
                });

                effects.ApplyStatus(new BattleStatusRequest
                {
                    Source = casterState, Target = enemyState,
                    Type = BattleStatusType.Stun, Level = 1, Duration = 2.0f, IsDebuff = true, IsDispelAllowed = true
                });
            }
        }
    }
}
