using System.Collections.Generic;
using UnityEngine;

public sealed class ManaCollapseSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.ManaCollapse;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 20f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        effects.GrantTemporaryArtifact(caster, new ManaCollapseArtifact(caster, AreaRadius), 10f, context);

        GameObject activeVfx = VFXManager.Instance.PlayEffect("ManaCollapseAura", caster.Position);

        effects.ScheduleEffect(
            10f,
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

    private class ManaCollapseArtifact : IDamageReactionArtifact, ISkillCastReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleRuntimeUnit _ownerView;
        private readonly float _radius;

        public ManaCollapseArtifact(BattleRuntimeUnit ownerView, float radius)
        {
            _ownerView = ownerView;
            _radius = radius;
        }

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Source == null || result.Source.TeamId == owner.TeamId || result.Source == owner)
                return;

            BattleRuntimeUnit attacker = null;
            // 실제 구현에서는 Source(State)로 View를 찾는 헬퍼를 쓰거나 거리 체크 로직을 조정해야 함.
            // 여기서는 단순화를 위해 공격자의 위치가 캐싱되었다고 가정.

            float dmg = result.Source.MaxHealth * 0.01f;
            effects.DealDamage(
                new BattleDamageRequest
                {
                    Source = owner,
                    Target = result.Source,
                    Amount = dmg,
                    SourceKind = BattleEffectSourceKind.Skill,
                    DamageKind = BattleDamageKind.Direct,
                    IsSkill = true,
                }
            );
        }

        public void OnSkillCast(
            BattleUnitCombatState owner,
            in BattleSkillCastEvent skillCastEvent,
            IBattleEffectSink effects
        )
        {
            if (skillCastEvent.Caster == null || skillCastEvent.Caster.TeamId == owner.TeamId)
                return;
            if (
                skillCastEvent.CasterView != null
                && Vector3.Distance(_ownerView.Position, skillCastEvent.CasterView.Position) <= _radius
            )
            {
                float dmg = skillCastEvent.Caster.MaxHealth * 0.10f;
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = owner,
                        Target = skillCastEvent.Caster,
                        Amount = dmg,
                        SourceKind = BattleEffectSourceKind.Skill,
                        DamageKind = BattleDamageKind.Direct,
                        IsSkill = true,
                    }
                );
            }
        }
    }
}
