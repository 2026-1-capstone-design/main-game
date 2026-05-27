using System.Collections.Generic;
using UnityEngine;

public sealed class BanishmentSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Banishment;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 100f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled)
            return;

        effects.ScheduleEffect(
            1.0f,
            casterRuntime,
            casterRuntime,
            context,
            (ctx, sink) =>
            {
                if (casterState.IsCombatDisabled)
                    return;

                BattleRuntimeUnit highestAtkEnemy = null;
                float maxAtk = -1f;

                // 공격력이 가장 높은 적 서치
                foreach (var unit in ctx.Units)
                {
                    BattleUnitCombatState enemyState = unit?.State;
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, enemyState))
                        continue;

                    if (enemyState.Attack > maxAtk)
                    {
                        maxAtk = enemyState.Attack;
                        highestAtkEnemy = unit;
                    }
                }

                if (highestAtkEnemy != null)
                {
                    VFXManager.Instance.PlayEffect("DarkHit", highestAtkEnemy.State.Position);
                    // BattleEffectSystem의 기존 함수를 호출하여 가장자리로 즉시 밀어버리고 5초 둔화(Slow) 적용
                    sink.PushToArenaEdge(casterState, highestAtkEnemy.State, 5.0f);
                    VFXManager.Instance.PlayEffect("DarkHit", highestAtkEnemy.State.Position);
                }
            }
        );
    }
}
