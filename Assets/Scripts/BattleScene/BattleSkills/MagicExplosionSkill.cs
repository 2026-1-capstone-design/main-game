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

        GameObject activeVfx = VFXManager.Instance.PlayEffect("BlueEnhance", caster.Position);

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

        // [추가됨] 무한 루프(연쇄 폭발)를 막기 위한 안전장치
        private bool _isProcessing = false;

        public MagicExplosionArtifact(float radius, IReadOnlyList<BattleRuntimeUnit> units)
        {
            _radius = radius;
            _units = units;
        }

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            // [핵심] 현재 폭발 데미지를 처리 중이라면, 또 다른 폭발을 일으키지 않고 무시합니다.
            if (_isProcessing) return;

            if (result.Source == owner && result.Target != null)
            {
                // 폭발 처리 시작! (이 아래에서 발생하는 데미지는 AfterDamage를 트리거하지 않음)
                _isProcessing = true;

                Vector3 targetPos = result.Target.Position;

                foreach (BattleRuntimeUnit unitView in _units)
                {
                    if (
                        unitView.State == result.Target
                        || !BattleFieldSnapshot.IsValidEnemyTarget(owner, unitView.State)
                    )
                        continue;

                    if (Vector3.Distance(targetPos, unitView.State.Position) <= _radius)
                    {
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

                        VFXManager.Instance.PlayEffect("LightHit", result.Target.Position + Vector3.up);
                    }
                }

                // 폭발 처리가 무사히 끝났으므로 다음 평타를 위해 안전장치 해제
                _isProcessing = false;
            }
        }
    }
}
