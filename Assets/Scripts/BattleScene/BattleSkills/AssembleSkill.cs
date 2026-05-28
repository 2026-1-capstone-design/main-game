using System.Collections.Generic;
using UnityEngine;

public sealed class AssembleSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Assemble;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 100f; // 전장 전체

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled)
            return;

        VFXManager.Instance.PlayEffect("BlessEnhance", casterState.Position + Vector3.up);

        effects.ScheduleEffect(
            2.0f,
            casterRuntime,
            casterRuntime,
            context,
            (ctx, sink) =>
            {
                if (casterState.IsCombatDisabled)
                    return;

                foreach (var unit in ctx.Units)
                {
                    BattleUnitCombatState allyState = unit?.State;
                    if (allyState == null || allyState.IsCombatDisabled || allyState.TeamId != casterState.TeamId)
                        continue;

                    // 본인이 아닌 아군들을 본인 반경 1.0f 위치까지만 끌어당김
                    if (allyState != casterState)
                    {
                        sink.PullTo(casterState, allyState, 1.0f);
                    }

                    // 모든 아군(본인 포함)에게 받는 피해 15% 감소 5초 적용
                    sink.ApplyStatus(
                        new BattleStatusRequest
                        {
                            Source = casterState,
                            Target = allyState,
                            Type = BattleStatusType.DamageReductionPercent, // 1당 1% 감소로 동작함
                            Level = 15,
                            Duration = 5.0f,
                            IsDebuff = false,
                            IsDispelAllowed = true,
                        }
                    );
                }
            }
        );
    }
}
