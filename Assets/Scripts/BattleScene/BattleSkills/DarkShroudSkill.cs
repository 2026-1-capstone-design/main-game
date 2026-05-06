using System.Collections.Generic;
using UnityEngine;

// [어둠 장막] 스태프: 시전 즉시 자신의 반경 15 안의 모든 적들에게 스킬 사용을 7초간 금지하는 디버프 부여.
public sealed class DarkShroudSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.DarkShroud;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleUnitCombatState caster = context.Actor?.State;
        if (caster == null) return;

        VFXManager.Instance.PlayEffect("DarkShroudEffect", caster.Position);

        foreach (BattleRuntimeUnit unitView in context.Units)
        {
            BattleUnitCombatState target = unitView?.State;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(caster, target)) continue;

            if (Vector3.Distance(caster.Position, target.Position) <= AreaRadius)
            {
                effects.ApplyStatus(new BattleStatusRequest
                {
                    Source = caster,
                    Target = target,
                    Type = BattleStatusType.SkillDisabled,
                    Level = 1,
                    Duration = 7f,
                    IsDebuff = true,
                    IsDispelAllowed = true
                });
            }
        }
    }
}
