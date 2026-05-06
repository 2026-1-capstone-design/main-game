using System.Collections.Generic;
using UnityEngine;

public sealed class CurseSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Curse;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 20f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.PrimaryTarget != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        if (caster == null || target == null) return;

        effects.DealDamage(new BattleDamageRequest { Source = caster.State, Target = target.State, Amount = caster.State.Attack * 0.5f, IsSkill = true });

        effects.GrantTemporaryArtifact(target, new CurseArtifact(), 10f, context);
        GameObject activeVfx = VFXManager.Instance.PlayEffect("CurseEffect", target.Position);

        effects.ScheduleEffect(10f, caster, target, context, (ctx, sink) =>
        {
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }

    private class CurseArtifact : IDamageModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Target == owner) request.Amount *= 1.30f;
        }
    }
}
