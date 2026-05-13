using System.Collections.Generic;
using UnityEngine;

public sealed class JuggernautSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Juggernaut;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 30f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        BattleRuntimeUnit targetRuntime = context.PrimaryTarget;
        BattleUnitCombatState targetState = targetRuntime?.State;

        if (casterState == null || targetState == null || targetState.IsCombatDisabled)
            return;

        float distance = Vector3.Distance(casterState.Position, targetState.Position);
        float damageMultiplier = 1f + (distance * 0.1f);

        effects.Teleport(casterState, targetState.Position);
        VFXManager.Instance.PlayEffect("JuggernautImpact", targetState.Position);

        effects.DealDamage(
            new BattleDamageRequest
            {
                Source = casterState,
                Target = targetState,
                Amount = casterState.Attack * damageMultiplier,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = SkillId,
                IsSkill = true,
            }
        );
    }
}
