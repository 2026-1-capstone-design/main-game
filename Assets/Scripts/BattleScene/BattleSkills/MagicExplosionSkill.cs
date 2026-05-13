using System.Collections.Generic;
using UnityEngine;

public sealed class MagicExplosionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.MagicExplosion;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 10f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        effects.GrantTemporaryArtifact(caster, new MagicExplosionArtifact(AreaRadius, context.Units), 10f, context);

        GameObject activeVfx = VFXManager.Instance.PlayEffect("MagicExplosionBuff", caster.Position);

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

    private class MagicExplosionArtifact : IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly float _radius;
        private readonly IReadOnlyList<BattleRuntimeUnit> _units;

        public MagicExplosionArtifact(float radius, IReadOnlyList<BattleRuntimeUnit> units)
        {
            _radius = radius;
            _units = units;
        }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Source == owner && result.Target != null)
            {
                // 공격한 타겟의 위치 주변으로 데미지를 뿌림 (실제 구현시 타겟의 위치 정보 필요)
                Vector3 targetPos = Vector3.zero; // Target State를 View로 변환하여 위치 획득

                foreach (BattleRuntimeUnit unitView in _units)
                {
                    if (
                        unitView.State == result.Target
                        || !BattleFieldSnapshot.IsValidEnemyTarget(owner, unitView.State)
                    )
                        continue;

                    // 타겟 주변 거리 계산 후 데미지
                    effects.DealDamage(
                        new BattleDamageRequest
                        {
                            Source = owner,
                            Target = unitView.State,
                            Amount = owner.Attack * 0.5f,
                            SourceKind = BattleEffectSourceKind.Skill,
                            DamageKind = BattleDamageKind.Area,
                            IsArea = true,
                            IsSkill = true,
                        }
                    );
                }
            }
        }
    }
}
