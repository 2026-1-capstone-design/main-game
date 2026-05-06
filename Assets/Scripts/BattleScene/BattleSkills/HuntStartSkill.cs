using System.Collections.Generic;
using UnityEngine;

// [사냥 개시] 활: 8초간 타겟 상대가 받는 피해량을 증가시킨다 (단순 % 추가 데미지 방식으로 구현).
public sealed class HuntStartSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HuntStart;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.bow };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 25f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        BattleUnitCombatState target = context.PrimaryTarget?.State;

        if (caster == null || target == null || target.IsCombatDisabled) return;

        effects.ApplyStatus(new BattleStatusRequest
        {
            Source = caster,
            Target = target,
            Type = BattleStatusType.DamageTakenPercent,
            Level = 25,
            Duration = 8f,
            IsDebuff = true,
            IsDispelAllowed = true
        });

        VFXManager.Instance.PlayEffect("MarkOfHunt", target.Position + Vector3.up * 2f);
    }
}
