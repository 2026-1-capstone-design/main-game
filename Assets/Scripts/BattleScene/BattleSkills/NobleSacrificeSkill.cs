using System.Collections.Generic;
using UnityEngine;

public sealed class NobleSacrificeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.NobleSacrifice;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.twoHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedAlly;
    public float CastRange => 20f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.PrimaryTarget != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit ally = context.PrimaryTarget;
        if (caster == null || ally == null)
            return;

        effects.GrantTemporaryArtifact(ally, new SacrificeArtifact(caster.State), 10f, context);
        GameObject activeVfx1 = VFXManager.Instance.PlayEffect("ShinyStance", caster.transform);
        GameObject activeVfx2 = VFXManager.Instance.PlayEffect("ShinyStance", ally.transform);

        effects.ScheduleEffect(
            10f,
            caster,
            ally,
            context,
            (ctx, sink) =>
            {
                if (activeVfx1 != null && activeVfx2 != null)
                {
                    VFXManager.Instance.StopEffect(activeVfx1);
                    VFXManager.Instance.StopEffect(activeVfx2);
                }
            }
        );
    }

    private class SacrificeArtifact : IDamageModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleUnitCombatState _protector;

        public SacrificeArtifact(BattleUnitCombatState protector)
        {
            _protector = protector;
        }

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Target == owner && !_protector.IsCombatDisabled)
            {
                request.Target = _protector;
                request.IsRedirected = true;
            }
        }
    }
}
