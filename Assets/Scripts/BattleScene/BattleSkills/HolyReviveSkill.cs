using System.Collections.Generic;
using UnityEngine;

public sealed class HolyReviveSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HolyRevive;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.None;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context)
    {
        if (context.Actor == null)
            return false;
        foreach (var unit in context.Units)
        {
            if (unit != null && unit.State.IsCombatDisabled && unit.TeamId == context.Actor.TeamId)
                return true;
        }
        return false;
    }

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState casterState = context.Actor?.State;
        if (casterState == null)
            return;

        BattleRuntimeUnit deadAlly = null;
        foreach (var unit in context.Units)
        {
            if (unit != null && unit.State.IsCombatDisabled && unit.TeamId == casterState.TeamId)
            {
                deadAlly = unit;
                break;
            }
        }

        if (deadAlly != null && deadAlly.State != null)
        {
            VFXManager.Instance.PlayEffect("HealEffect", deadAlly.Position + Vector3.up * 0.1f);
            effects.Revive(deadAlly.State, deadAlly.MaxHealth * 0.2f);
        }
    }
}
