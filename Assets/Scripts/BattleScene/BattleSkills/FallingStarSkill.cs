using System.Collections.Generic;
using UnityEngine;

public sealed class FallingStarSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.FallingStar;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self; // 자체 로직 탐색
    public float CastRange => 100f; // 무제한
    public float AreaRadius => 3f; // 궤적 폭

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        effects.ScheduleEffect(
            0.8f,
            casterRuntime,
            casterRuntime,
            context,
            (ctx, sink) =>
            {
                if (casterState.IsCombatDisabled) return;

                BattleRuntimeUnit furthestEnemy = null;
                float maxDist = -1f;

                // 가장 먼 적 찾기
                foreach (var unit in ctx.Units)
                {
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, unit?.State)) continue;
                    float dist = Vector3.Distance(casterState.Position, unit.State.Position);
                    if (dist > maxDist)
                    {
                        maxDist = dist;
                        furthestEnemy = unit;
                    }
                }

                if (furthestEnemy == null) return;

                Vector3 startPos = casterState.Position;
                Vector3 endPos = furthestEnemy.State.Position;
                Vector3 dashDir = (endPos - startPos).normalized;
                float dashLength = Vector3.Distance(startPos, endPos);

                // 궤적 상의 적 판정 (점과 선분 사이의 거리 계산)
                foreach (var unit in ctx.Units)
                {
                    BattleUnitCombatState enemyState = unit?.State;
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, enemyState)) continue;

                    Vector3 point = enemyState.Position;
                    float t = Vector3.Dot(point - startPos, dashDir); // 선분 위로의 투영 위치

                    // 투영된 위치가 선분 안에 있고, 수직 거리가 AreaRadius 이내인지 확인
                    if (t >= 0f && t <= dashLength)
                    {
                        Vector3 projectedPoint = startPos + (dashDir * t);
                        if (Vector3.Distance(point, projectedPoint) <= AreaRadius)
                        {
                            sink.DealDamage(new BattleDamageRequest
                            {
                                Source = casterState, Target = enemyState, Amount = casterState.Attack * 2.5f,
                                SourceKind = BattleEffectSourceKind.Skill, DamageKind = BattleDamageKind.Direct
                            });

                            sink.AddKnockback(enemyState, dashDir, 10f); // 밀쳐냄
                        }
                    }
                }

                sink.Teleport(casterState, endPos);
                VFXManager.Instance.PlayEffect("VanishEffect", startPos);

                // 궤적을 따라 촘촘하게 CompactHit 이펙트 재생
                float effectInterval = 0.5f; // 이펙트가 생성될 간격 (필요에 따라 조절)
                int effectCount = Mathf.FloorToInt(dashLength / effectInterval);

                for (int i = 0; i <= effectCount; i++)
                {
                    Vector3 effectPos = startPos + (dashDir * (i * effectInterval));
                    VFXManager.Instance.PlayEffect("CompactHit", effectPos);
                }
            }
        );
    }
}
