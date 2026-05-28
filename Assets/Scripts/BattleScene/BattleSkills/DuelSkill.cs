using System.Collections.Generic;
using UnityEngine;

public sealed class DuelSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Duel;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dualGun };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 15f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.PrimaryTarget != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;
        if (caster == null || target == null)
            return;

        effects.GrantTemporaryArtifact(caster, new DuelArtifact(target.State), 10f, context);
        GameObject activeVfx = VFXManager.Instance.PlayEffect("BrokenHeart", target.Position + Vector3.up);

        effects.ScheduleEffect(
            10f,
            caster,
            target,
            context,
            (ctx, sink) =>
            {
                if (activeVfx != null)
                    VFXManager.Instance.StopEffect(activeVfx);
            }
        );
    }

    private class DuelArtifact : IDamageModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleUnitCombatState _duelTarget;

        public DuelArtifact(BattleUnitCombatState duelTarget)
        {
            _duelTarget = duelTarget;
        }

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Source == owner && request.Target == _duelTarget)
                request.Amount *= 1.50f;
            if (request.Target == owner && request.Source != _duelTarget)
                request.Amount *= 0.50f;
        }
    }
}
