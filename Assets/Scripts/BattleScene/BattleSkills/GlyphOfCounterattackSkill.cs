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
        if (caster == null || ally == null) return;

        // 시전자 공격력의 3배만큼의 쉴드량을 가진 아티팩트 부여
        float shieldAmount = caster.State.Attack * 3.0f;
        effects.GrantTemporaryArtifact(ally, new CounterattackArtifact(caster.State, shieldAmount), 10f, context);

        GameObject activeVfx = VFXManager.Instance.PlayEffect("CounterattackShield", ally.Position);

        effects.ScheduleEffect(10f, caster, ally, context, (ctx, sink) =>
        {
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }

    private class CounterattackArtifact : IDamageModifierArtifact, IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleUnitCombatState _originalCaster;
        private float _shieldHp;

        public CounterattackArtifact(BattleUnitCombatState originalCaster, float shieldHp)
        {
            _originalCaster = originalCaster;
            _shieldHp = shieldHp;
        }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        // 데미지 방어 (쉴드 로직)
        public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
        {
            if (request.Target == owner && _shieldHp > 0)
            {
                if (_shieldHp >= request.Amount)
                {
                    _shieldHp -= request.Amount;
                    request.Amount = 0;
                }
                else
                {
                    request.Amount -= _shieldHp;
                    _shieldHp = 0;
                }
            }
        }

        // 데미지 반사
        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Target == owner && result.Source != null && result.Source != owner)
            {
                effects.DealDamage(new BattleDamageRequest { Source = _originalCaster, Target = result.Source, Amount = _originalCaster.Attack * 0.8f, IsSkill = true });
            }
        }
    }
}
