using System.Collections.Generic;
using UnityEngine;

public sealed class MindControlSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.MindControl;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 20f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.PrimaryTarget != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        if (caster == null || target == null) return;

        // 직접 TeamId를 변경 (가정 제거)
        BattleTeamId originalTeamId = target.TeamId;
        target.State.TeamId = caster.TeamId;

        GameObject activeVfx = VFXManager.Instance.PlayEffect("MindControlOn", target.Position);

        effects.ScheduleEffect(5f, caster, target, context, (ctx, sink) =>
        {
            if (target != null && !target.IsCombatDisabled)
            {
                target.State.TeamId = originalTeamId; // 원래 팀으로 복구
            }
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }
}
