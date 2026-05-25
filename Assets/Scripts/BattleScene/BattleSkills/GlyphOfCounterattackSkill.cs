using System.Collections.Generic;
using UnityEngine;

public sealed class GlyphOfCounterattackSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.GlyphOfCounterattack;
    public skillType SkillCategory => skillType.support;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
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

        // 쉴드 계산 로직을 제거하고 아티팩트 부여 시 시전자 정보만 넘깁니다.
        effects.GrantTemporaryArtifact(ally, new CounterattackArtifact(caster.State), 10f, context);

        GameObject activeVfx = VFXManager.Instance.PlayEffect("LightStance", ally.transform);

        effects.ScheduleEffect(
            10f,
            caster,
            ally,
            context,
            (ctx, sink) =>
            {
                if (activeVfx != null)
                    VFXManager.Instance.StopEffect(activeVfx);
            }
        );
    }

    private class CounterattackArtifact : IDamageModifierArtifact, IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleUnitCombatState _originalCaster;

        public CounterattackArtifact(BattleUnitCombatState originalCaster)
        {
            _originalCaster = originalCaster;
        }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        //쉴드 삭감 로직 대신, 받는 데미지를 50%로 줄입니다.
        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Target == owner)
            {
                // 들어오는 피해량을 절반(50%)으로 깎습니다.
                request.Amount *= 0.5f;
            }
        }

        // 데미지 반사 (기존 유지)
        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Target == owner && result.Source != null && result.Source != owner)
            {
                effects.DealDamage(
                    new BattleDamageRequest
                    {
                        Source = _originalCaster,
                        Target = result.Source,
                        Amount = _originalCaster.Attack * 0.8f,
                        IsSkill = true,
                    }
                );
            }
        }
    }
}
