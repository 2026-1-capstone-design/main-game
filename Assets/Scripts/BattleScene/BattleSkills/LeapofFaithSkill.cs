using System.Collections.Generic;
using UnityEngine;

// [신뢰의 도약] 쉴드: 체력이 가장 낮은 아군 옆으로 텔레포트하고 도발 및 피해 감소 버프를 건다.
public sealed class LeapOfFaithSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.LeapOfFaith;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.None;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        if (caster == null || caster.IsCombatDisabled) return;

        BattleRuntimeUnit lowestHpAlly = null;
        float minHpRatio = float.MaxValue;

        foreach (BattleRuntimeUnit unit in context.Units)
        {
            if (unit == null || unit.State.IsCombatDisabled || unit.TeamId != caster.TeamId || unit == context.Actor) continue;

            float hpRatio = unit.CurrentHealth / unit.MaxHealth;
            if (hpRatio < minHpRatio)
            {
                minHpRatio = hpRatio;
                lowestHpAlly = unit;
            }
        }

        if (lowestHpAlly != null)
        {
            Vector3 jumpDestination = lowestHpAlly.Position + (caster.Position - lowestHpAlly.Position).normalized * 2f;
            effects.Teleport(caster, jumpDestination);

            effects.ApplyStatus(new BattleStatusRequest { Source = caster, Target = caster, Type = BattleStatusType.Taunt, Level = 1, Duration = 5f, IsDebuff = false, IsDispelAllowed = true });
            effects.ApplyStatus(new BattleStatusRequest { Source = caster, Target = caster, Type = BattleStatusType.DamageReductionPercent, Level = 30, Duration = 5f, IsDebuff = false, IsDispelAllowed = true });

            VFXManager.Instance.PlayEffect("ShieldLanding", jumpDestination);
        }
    }
}
