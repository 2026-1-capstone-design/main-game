using System.Collections.Generic;
using UnityEngine;

public sealed class RetributionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Retribution;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null || caster.State.IsCombatDisabled)
            return;

        effects.GrantTemporaryArtifact(caster, new RetributionArtifact(), 5f, context);

        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = caster.State,
                Target = caster.State,
                Type = BattleStatusType.DamageReductionPercent,
                Level = 50,
                Duration = 5f,
            }
        );
        GameObject activeVfx = VFXManager.Instance.PlayEffect("LightStance", caster.transform);

        effects.ScheduleEffect(
            5f,
            caster,
            caster,
            context,
            (ctx, sink) =>
            {
                if (activeVfx != null)
                    VFXManager.Instance.StopEffect(activeVfx);
            }
        );
    }

    private class RetributionArtifact : IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Target == owner && result.Source != null && result.Source != owner)
            {
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = owner,
                        Target = result.Source,
                        Amount = result.FinalAmount * 0.5f,
                        SourceKind = BattleEffectSourceKind.Skill,
                        DamageKind = BattleDamageKind.Direct,
                        IsSkill = true,
                    }
                );

                VFXManager.Instance.PlayEffect("LightHit", result.Source.Position);
            }
        }
    }
}
