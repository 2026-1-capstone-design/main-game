using System.Collections.Generic;
using UnityEngine;

public sealed class FreezeSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Freeze;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.handGun, WeaponType.rifle };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 20f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink contextEffects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        contextEffects.GrantTemporaryArtifact(caster, new FreezeAuraArtifact(AreaRadius), 8f, context);
        GameObject activeVfx = VFXManager.Instance.PlayEffect("FreezeZoneAura", caster.Position);

        contextEffects.ScheduleEffect(
            8f,
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

    private class FreezeAuraArtifact : IPositionHistoryArtifact, ISkillCastReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly float _radius;
        private readonly Dictionary<BattleRuntimeUnit, Vector3> _lastPositions =
            new Dictionary<BattleRuntimeUnit, Vector3>();
        private readonly Dictionary<BattleRuntimeUnit, float> _cooldowns = new Dictionary<BattleRuntimeUnit, float>();

        public FreezeAuraArtifact(float radius)
        {
            _radius = radius;
        }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void TickWithPositionHistory(
            BattleRuntimeUnit owner,
            BattlePositionHistory history,
            in BattleEffectContext context,
            IBattleEffectSink effects
        )
        {
            foreach (var unit in context.Units)
            {
                if (unit == null || unit == owner || !BattleFieldSnapshot.IsValidEnemyTarget(owner.State, unit.State))
                    continue;
                if (Vector3.Distance(owner.Position, unit.Position) > _radius)
                    continue;

                if (_cooldowns.TryGetValue(unit, out float cd) && cd > context.BattleTime)
                    continue;

                if (_lastPositions.TryGetValue(unit, out Vector3 lastPos))
                {
                    if (Vector3.Distance(unit.Position, lastPos) > 0.1f)
                    {
                        effects.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = owner.State,
                                Target = unit.State,
                                Amount = owner.State.Attack * 1.5f,
                                IsSkill = true,
                            }
                        );
                        VFXManager.Instance.PlayEffect("SniperShot", unit.Position);
                        _cooldowns[unit] = context.BattleTime + 0.5f;
                    }
                }
                _lastPositions[unit] = unit.Position;
            }
        }

        public void OnSkillCast(BattleUnitCombatState owner, in BattleSkillCastEvent evt, IBattleEffectSink effects)
        {
            if (
                evt.CasterView != null
                && BattleFieldSnapshot.IsValidEnemyTarget(owner, evt.Caster)
                && Vector3.Distance(owner.Position, evt.CasterView.Position) <= _radius
            )
            {
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = owner,
                        Target = evt.Caster,
                        Amount = owner.Attack * 2.0f,
                        IsSkill = true,
                    }
                );
                VFXManager.Instance.PlayEffect("SniperShot", evt.CasterView.Position);
            }
        }
    }
}
