using System.Collections.Generic;
using UnityEngine;

public sealed class FanaticalObsessionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.FanaticalObsession;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.oneHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        effects.GrantTemporaryArtifact(caster, new ObsessionArtifact(), 15f, context);
        GameObject activeVfx = VFXManager.Instance.PlayEffect("RedEnhance", caster.Position + Vector3.up);

        effects.ScheduleEffect(
            15f,
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

    private class ObsessionArtifact : IPositionHistoryArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private float _lastDashTime = 0f;

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void TickWithPositionHistory(
            BattleRuntimeUnit owner,
            BattlePositionHistory history,
            in BattleEffectContext context,
            IBattleEffectSink effects
        )
        {
            BattleUnitCombatState currentTarget = context.Actor?.PlannedTargetEnemy;

            if (currentTarget != null && !currentTarget.IsCombatDisabled)
            {
                float dist = Vector3.Distance(owner.Position, currentTarget.Position);

                if (dist > owner.State.AttackRange && (context.BattleTime - _lastDashTime > 0.5f))
                {
                    VFXManager.Instance.PlayEffect("VanishEffect", owner.Position);
                    effects.Teleport(owner.State, currentTarget.Position);
                    VFXManager.Instance.PlayEffect("VanishEffect", owner.Position);
                    effects.DealDamage(
                        new BattleDamageRequest
                        {
                            Source = owner.State,
                            Target = currentTarget,
                            Amount = owner.State.Attack,
                            IsSkill = true,
                        }
                    );
                    VFXManager.Instance.PlayEffect("CriticalHit", owner.Position);


                    _lastDashTime = context.BattleTime;
                }
            }
        }
    }
}
