using System.Collections.Generic;
using UnityEngine;

public sealed class HarvestSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Harvest;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 10f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState casterState = context.Actor?.State;
        if (casterState == null || casterState.IsCombatDisabled) return;

        VFXManager.Instance.PlayEffect("HarvestSlash", casterState.Position);
        float totalTheoreticalDamage = 0f;

        foreach (BattleRuntimeUnit unitView in context.Units)
        {
            BattleUnitCombatState targetState = unitView?.State;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, targetState)) continue;

            if (Vector3.Distance(casterState.Position, targetState.Position) <= AreaRadius)
            {
                float damageAmount = casterState.Attack * 1.5f;
                totalTheoreticalDamage += damageAmount;

                effects.DealDamage(new BattleDamageRequest { Source = casterState, Target = targetState, Amount = damageAmount, SourceKind = BattleEffectSourceKind.Skill, DamageKind = BattleDamageKind.Area, SkillId = SkillId, IsSkill = true, IsArea = true });
            }
        }

        if (totalTheoreticalDamage > 0f)
        {
            effects.Heal(new BattleHealRequest { Source = casterState, Target = casterState, Amount = totalTheoreticalDamage * 0.3f, SourceKind = BattleEffectSourceKind.Skill });
        }
    }
}
