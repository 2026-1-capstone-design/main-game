using System.Collections.Generic;
using UnityEngine;

// [흉성] 단검: 받는 피해 증가(디버프), 이동 속도 증가(버프), 공격력 증가(버프)를 자신에게 부여한다.
public sealed class OminousStarSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.OminousStar;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        if (caster == null || caster.IsCombatDisabled) return;

        effects.ApplyStatus(new BattleStatusRequest { Source = caster, Target = caster, Type = BattleStatusType.DamageTakenPercent, Level = 20, Duration = 10f, IsDebuff = true, IsDispelAllowed = true });
        effects.ApplyStatus(new BattleStatusRequest { Source = caster, Target = caster, Type = BattleStatusType.MoveSpeed, Level = 30, Duration = 10f, IsDebuff = false, IsDispelAllowed = true });
        effects.ApplyStatus(new BattleStatusRequest { Source = caster, Target = caster, Type = BattleStatusType.AttackDamage, Level = 30, Duration = 10f, IsDebuff = false, IsDispelAllowed = true });

        VFXManager.Instance.PlayEffect("OminousStarBuff", caster.Position);
    }
}
