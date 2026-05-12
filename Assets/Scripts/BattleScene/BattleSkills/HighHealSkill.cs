using System.Collections.Generic;
using UnityEngine;

// [하이 힐] 스태프: 아군을 지정하여 대상 최대 체력의 10%만큼 치유한다.
public sealed class HighHealSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HighHeal;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedAlly;
    public float CastRange => 15f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetAlly != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        BattleUnitCombatState target = context.PrimaryTarget?.State;

        if (caster == null || target == null || target.IsCombatDisabled)
            return;

        float healAmount = target.MaxHealth * 0.10f;

        effects.Heal(
            new BattleHealRequest
            {
                Source = caster,
                Target = target,
                Amount = healAmount,
                SourceKind = BattleEffectSourceKind.Skill,
            }
        );

        VFXManager.Instance.PlayEffect("HighHealEffect", target.Position);
    }
}
