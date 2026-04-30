using System.Collections.Generic;
using UnityEngine;

// 7. 라이트닝 (스태프) : 타겟 주변 적군 전체 데미지
public sealed class LightningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Lightning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundTarget;
    public float CastRange => 0f;
    public float AreaRadius => 40f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        if (target == null)
            return;

        foreach (BattleRuntimeUnit unit in context.Units)
        {
            if (
                unit == null
                || unit.IsCombatDisabled
                || !BattleFieldSnapshot.IsValidEnemyTarget(caster.State, unit.State)
            )
                continue;

            if (Vector3.Distance(target.Position, unit.Position) <= 40f)
            {
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = caster,
                        Target = unit,
                        Amount = caster.Attack * 1.5f,
                        SourceKind = BattleEffectSourceKind.Skill,
                        DamageKind = BattleDamageKind.Area,
                        SkillId = SkillId,
                        IsSkill = true,
                        IsArea = true,
                    }
                );
            }
        }
    }
}
