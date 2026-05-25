using System.Collections.Generic;
using UnityEngine;

public sealed class EvasiveManeuverSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.EvasiveManeuver; // Enum에 추가 필요
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.bow };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 6f; // 반응할 적 접근 반경

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        float optimalDistance = Mathf.Max(2f, casterState.AttackRange * 0.95f); // 사거리보다 아주 조금 짧은 카이팅 거리

        // 10초 동안 1초 간격으로 10번의 위치 조정 검사를 예약합니다.
        for (int i = 0; i <= 10; i++)
        {
            effects.ScheduleEffect(i, casterRuntime, casterRuntime, context, (ctx, sink) =>
            {
                if (casterState.IsCombatDisabled) return;

                BattleRuntimeUnit nearestEnemy = null;
                float minDistance = float.MaxValue;

                // 가장 가까운 적 찾기
                foreach (var unitView in ctx.Units)
                {
                    if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, unitView?.State)) continue;

                    float dist = Vector3.Distance(casterState.Position, unitView.State.Position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestEnemy = unitView;
                    }
                }

                // 적이 반응 반경(AreaRadius) 안에 들어왔다면 대쉬
                if (nearestEnemy != null && minDistance < AreaRadius)
                {
                    Vector3 awayDirection = (casterState.Position - nearestEnemy.State.Position).normalized;
                    awayDirection.y = 0f;

                    // 확보해야 할 추가 거리 계산
                    float dashDistance = Mathf.Max(0f, optimalDistance - minDistance);

                    if (dashDistance > 0.5f)
                    {
                        VFXManager.Instance.PlayEffect("FlashEffect", casterState.Position + Vector3.up);
                        // 넉백 시스템을 이용해 순간적인 대쉬 처리
                        sink.AddKnockback(casterState, awayDirection, dashDistance * 5f);
                    }
                }
            });
        }
    }
}
