using System.Collections.Generic;
using UnityEngine;

// 9. 리볼버 패닝 (권총) : 6번 연속 공격
public sealed class RevolverFanningSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.RevolverFanning;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.handGun };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null
        && context.Actor.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(context.Actor.State, context.Actor.PlannedTargetEnemy);

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;
        BattleUnitCombatState target = context.PrimaryTarget != null ? context.PrimaryTarget.State : null;
        if (target == null)
            return;

        for (int i = 0; i < 6; i++)
        {
            float newX = Random.Range(-1f, 1f);
            float newY = Random.Range(0f, 1f);
            float newZ = Random.Range(-1f, 1f);
            VFXManager.Instance.PlayEffect("CompactHit", target.Position + new Vector3(newX, newY, newZ));
            effects.DealDamage(
                new BattleDamageRequest
                {
                    Source = caster,
                    Target = target,
                    Amount = caster.Attack * 0.5f,
                    SourceKind = BattleEffectSourceKind.Skill,
                    DamageKind = BattleDamageKind.Direct,
                    SkillId = SkillId,
                    IsSkill = true,
                }
            );
        }
    }
}
