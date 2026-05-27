using System.Collections.Generic;
using UnityEngine;

public sealed class ScatteredStarlightSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ScatteredStarlight;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.spear };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled)
            return;

        // 타격 대상을 순서대로 저장할 리스트 (최대 5명)
        List<BattleRuntimeUnit> targetChain = new List<BattleRuntimeUnit>();
        Vector3 currentPos = casterState.Position;
        BattleRuntimeUnit lastTarget = null;

        for (int i = 0; i < 5; i++)
        {
            BattleRuntimeUnit nextTarget = null;
            float maxDist = -1f;

            foreach (var unit in context.Units)
            {
                // 방금 타격한 대상은 제외 (연속 타격 금지)
                if (unit == lastTarget || !BattleFieldSnapshot.IsValidEnemyTarget(casterState, unit?.State))
                    continue;

                float dist = Vector3.Distance(currentPos, unit.State.Position);
                // 탐색 반경 내에서 가장 먼 대상 찾기
                if (dist <= AreaRadius && dist > maxDist)
                {
                    maxDist = dist;
                    nextTarget = unit;
                }
            }

            if (nextTarget == null)
                break; // 더 이상 범위 내에 칠 수 있는 적이 없으면 연쇄 종료

            targetChain.Add(nextTarget);
            currentPos = nextTarget.State.Position;
            lastTarget = nextTarget;
        }

        // 결정된 타겟 순서대로 0.2초 간격으로 순간이동하며 공격 예약
        for (int i = 0; i < targetChain.Count; i++)
        {
            BattleRuntimeUnit target = targetChain[i];
            effects.ScheduleEffect(
                i * 0.2f,
                casterRuntime,
                target,
                context,
                (ctx, sink) =>
                {
                    if (casterState.IsCombatDisabled)
                        return; // 시전 중 사망 시 취소

                    VFXManager.Instance.PlayEffect("VanishEffect", casterState.Position + Vector3.up);
                    sink.Teleport(casterState, target.State.Position);
                    VFXManager.Instance.PlayEffect("CompactHit", target.State.Position + Vector3.up);

                    sink.DealDamage(
                        new BattleDamageRequest
                        {
                            Source = casterState,
                            Target = target.State,
                            Amount = casterState.Attack * 1.8f,
                            SourceKind = BattleEffectSourceKind.Skill,
                            DamageKind = BattleDamageKind.Direct,
                        }
                    );
                }
            );
        }
    }
}
