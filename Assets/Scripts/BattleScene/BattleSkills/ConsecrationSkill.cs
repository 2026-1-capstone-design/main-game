using System.Collections.Generic;
using UnityEngine;

// [축성] 한손검: 자신에게 걸린 모든 디버프를 즉시 해제하고, 소량의 힐, 모든 버프의 지속시간을 10초로 갱신한다.
public sealed class ConsecrationSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Consecration;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        if (caster == null || caster.IsCombatDisabled)
            return;

        effects.Dispel(
            caster,
            new BattleDispelFilter
            {
                RemoveDebuffs = true,
                RemoveBuffs = false,
                DispelOnlyAllowed = true,
            }
        );

        effects.Heal(
            new BattleHealRequest
            {
                Source = caster,
                Target = caster,
                Amount = caster.MaxHealth * 0.05f,
                SourceKind = BattleEffectSourceKind.Skill,
            }
        );

        effects.RefreshStatuses(
            caster,
            new BattleStatusFilter
            {
                IncludeBuffs = true,
                IncludeDebuffs = false,
                Type = null,
            },
            10f
        );

        VFXManager.Instance.PlayEffect("ConsecrationEffect", caster.Position);
    }
}
