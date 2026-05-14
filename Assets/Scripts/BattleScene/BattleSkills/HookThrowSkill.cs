using System.Collections.Generic;
using UnityEngine;

// [갈고리 투척] 한손검: 타겟팅 된 적(없다면 가장 가까운 적)을 자신에게 끌어당긴다.
public sealed class HookThrowSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HookThrow;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 20f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        BattleUnitCombatState target = context.PrimaryTarget?.State;

        if (caster == null || target == null || target.IsCombatDisabled)
            return;

        VFXManager.Instance.PlayEffect("CompactHit", target.Position);

        effects.PullTo(caster, target, 1.5f);
        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = caster,
                Target = target,
                Type = BattleStatusType.Slow,
                Level = 50,
                Duration = 2f,
                IsDebuff = true,
                IsDispelAllowed = true,
            }
        );

    }
}
