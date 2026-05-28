using System.Collections.Generic;
using UnityEngine;

// 13. 나선 베기 (한손검) : 자신 기준 광역 데미지 + 넉백
public sealed class SpiralSlashSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.SpiralSlash;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 45f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor != null ? context.Actor.State : null;

        effects.ScheduleEffect(
            caster.SkillDuration,
            context.Actor,
            context.Actor,
            context,
            (ctx, sink) =>
            {
                VFXManager.Instance.PlayEffect("SwipeAttackEffect", caster.Position + Vector3.up * 1f);

                foreach (BattleRuntimeUnit unitView in ctx.Units)
                {
                    BattleUnitCombatState unit = unitView != null ? unitView.State : null;
                    if (BattleFieldSnapshot.IsValidEnemyTarget(caster, unit))
                    {
                        if (Vector3.Distance(caster.Position, unit.Position) <= 20f) // 광역 반경 20
                        {
                            VFXManager.Instance.PlayEffect("PlainSwordHit", unit.Position + Vector3.up * 1f);
                            effects.DealDamage(
                                new BattleDamageRequest
                                {
                                    Source = caster,
                                    Target = unit,
                                    Amount = caster.Attack * 1.2f,
                                    SourceKind = BattleEffectSourceKind.Skill,
                                    DamageKind = BattleDamageKind.Area,
                                    SkillId = SkillId,
                                    IsSkill = true,
                                    IsArea = true,
                                }
                            );
                            Vector3 pushDir = unit.Position - caster.Position;
                            effects.AddKnockback(unit, pushDir, 80f);
                        }
                    }
                }
            }
        );
    }
}
