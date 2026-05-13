using System.Collections.Generic;
using UnityEngine;

public sealed class RespiteSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Respite;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        GameObject activeZoneVfx = VFXManager.Instance.PlayEffect("KindredUltZone", caster.Position);

        foreach (var unit in context.Units)
        {
            if (unit == null || unit.State.IsCombatDisabled)
                continue;
            if (Vector3.Distance(caster.Position, unit.Position) <= AreaRadius)
            {
                effects.GrantTemporaryArtifact(unit, new RespiteArtifact(), 5f, context);

                GameObject unitVfx = VFXManager.Instance.PlayEffect("RespiteSave", unit.Position);
                effects.ScheduleEffect(
                    5f,
                    caster,
                    unit,
                    context,
                    (ctx, sink) =>
                    {
                        if (unitVfx != null)
                            VFXManager.Instance.StopEffect(unitVfx);
                    }
                );
            }
        }

        effects.ScheduleEffect(
            5f,
            caster,
            caster,
            context,
            (ctx, sink) =>
            {
                if (activeZoneVfx != null)
                    VFXManager.Instance.StopEffect(activeZoneVfx);
            }
        );
    }

    private class RespiteArtifact : IDamageModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Target == owner)
            {
                // 들어올 데미지가 현재 체력 - 1 보다 크다면 (즉, 체력이 1 미만으로 떨어지려 한다면)
                if (request.Amount >= owner.CurrentHealth - 1f)
                {
                    // 데미지를 딱 '체력이 1 남을 만큼'으로 줄여버립니다.
                    request.Amount = Mathf.Max(0f, owner.CurrentHealth - 1f);
                }
            }
        }
    }
}
