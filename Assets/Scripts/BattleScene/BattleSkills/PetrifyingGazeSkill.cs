using System.Collections.Generic;
using UnityEngine;

public sealed class PetrifyingGazeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.PetrifyingGaze;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        VFXManager.Instance.PlayEffect("GazeEffect", casterState.Position + Vector3.up * 2f);

        foreach (var unit in context.Units)
        {
            BattleUnitCombatState enemyState = unit?.State;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, enemyState)) continue;

            if (Vector3.Distance(casterState.Position, enemyState.Position) <= AreaRadius)
            {
                // 적이 시전자를 바라보고 있는지 벡터 내적(Dot Product)으로 검사
                Vector3 dirToCaster = (casterState.Position - enemyState.Position).normalized;

                // 유닛의 transform.forward와 시전자 방향이 90도 이내(> 0)인지 확인
                if (Vector3.Dot(enemyState.transform.forward, dirToCaster) > 0f)
                {
                    effects.DealDamage(new BattleDamageRequest
                    {
                        Source = casterState, Target = enemyState, Amount = casterState.Attack * 1.5f,
                        SourceKind = BattleEffectSourceKind.Skill, DamageKind = BattleDamageKind.Area
                    });

                    effects.ApplyStatus(new BattleStatusRequest
                    {
                        Source = casterState, Target = enemyState,
                        Type = BattleStatusType.Stun, Level = 1, Duration = 3.0f, IsDebuff = true, IsDispelAllowed = true
                    });
                }
            }
        }
    }
}
