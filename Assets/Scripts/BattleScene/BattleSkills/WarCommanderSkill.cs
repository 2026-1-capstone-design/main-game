using System.Collections.Generic;
using UnityEngine;

// [워 커맨더] 쉴드: 자신 주변 20 반경 안의 적들에게 이속/공격력 감소, 아군은 증가시킨다.
public sealed class WarCommanderSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.WarCommander;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 20f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        if (caster == null)
            return;

        VFXManager.Instance.PlayEffect("WarCommanderAura", caster.Position);

        foreach (BattleRuntimeUnit unitView in context.Units)
        {
            BattleUnitCombatState target = unitView?.State;
            if (target == null || target.IsCombatDisabled)
                continue;

            if (Vector3.Distance(caster.Position, target.Position) <= AreaRadius)
            {
                bool isEnemy = target.TeamId != caster.TeamId;

                effects.ApplyStatus(
                    new BattleStatusRequest
                    {
                        Source = caster,
                        Target = target,
                        Type = BattleStatusType.MoveSpeed,
                        Level = 15,
                        Duration = 8f,
                        IsDebuff = isEnemy,
                        IsDispelAllowed = true,
                    }
                );
                effects.ApplyStatus(
                    new BattleStatusRequest
                    {
                        Source = caster,
                        Target = target,
                        Type = BattleStatusType.AttackDamage,
                        Level = 15,
                        Duration = 8f,
                        IsDebuff = isEnemy,
                        IsDispelAllowed = true,
                    }
                );
            }
        }
    }
}
