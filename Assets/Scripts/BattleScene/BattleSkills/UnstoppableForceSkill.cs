using System.Collections.Generic;
using UnityEngine;

public sealed class UnstoppableForceSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.UnstoppableForce;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 25f;
    public float AreaRadius => 8f;

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

        effects.Teleport(casterState, targetState.Position);
        VFXManager.Instance.PlayEffect("GroundSmash", targetState.Position);

        foreach (BattleRuntimeUnit unitView in context.Units)
        {
            BattleUnitCombatState unitState = unitView?.State;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(casterState, unitState))
                continue;

            if (Vector3.Distance(targetState.Position, unitState.Position) <= AreaRadius)
            {
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = casterState,
                        Target = unitState,
                        Amount = casterState.Attack * 2.5f,
                        SourceKind = BattleEffectSourceKind.Skill,
                        DamageKind = BattleDamageKind.Area,
                        SkillId = SkillId,
                        IsSkill = true,
                        IsArea = true,
                    }
                );
                effects.ApplyStatus(
                    new BattleStatusRequest
                    {
                        Source = casterState,
                        Target = unitState,
                        Type = BattleStatusType.Stun,
                        Level = 1,
                        Duration = 1.5f,
                        IsDebuff = true,
                        IsDispelAllowed = true,
                    }
                );
                effects.AddKnockback(unitState, (unitState.Position - targetState.Position).normalized, 10f);
            }
        }
    }
}
