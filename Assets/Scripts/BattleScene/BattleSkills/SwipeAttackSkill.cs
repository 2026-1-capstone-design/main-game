using System.Collections.Generic;
using UnityEngine;

// [선풍베기] 한손검 전용. 시전자 주변 원형 범위의 적들에게 광역 피해를 줍니다.
public sealed class SwipeAttackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.SwipeAttack;
    public skillType SkillCategory => skillType.attack;

    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };

    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;

    //원형 공격의 범위 (반경)
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null) return;

        VFXManager.Instance.PlayEffect("SwipeAttackEffect", caster.Position + Vector3.up * 0.5f);

        foreach (var unit in context.Units)
        {
            if (unit == null || unit.State.IsCombatDisabled) continue;

            if (BattleFieldSnapshot.IsValidEnemyTarget(caster.State, unit.State))
            {
                if (Vector3.Distance(caster.Position, unit.Position) <= AreaRadius)
                {
                    effects.DealDamage(new BattleDamageRequest
                    {
                        Source = caster.State,
                        Target = unit.State,
                        Amount = caster.State.Attack * 1.2f,
                        SourceKind = BattleEffectSourceKind.Skill,
                        DamageKind = BattleDamageKind.Area,
                        IsSkill = true,
                        IsArea = true
                    });
                }
            }
        }
    }
}
