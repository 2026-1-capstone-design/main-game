using System.Collections.Generic;
using UnityEngine;

// [침잠] 단검: 8초 동안 아군과 적군에게 타겟으로 지정되지 않음(광역 피해는 입음), 이동속도 30 증가
public sealed class SubmersionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Submersion;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dagger };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null || caster.State.IsCombatDisabled) return;

        // 1. 8초간 타겟팅을 원천 차단하는 임시 장신구 장착
        effects.GrantTemporaryArtifact(caster, new UntargetableArtifact(), 8f, context);

        // 2. 이동 속도 증가는 기존 Status 시스템을 그대로 활용
        effects.ApplyStatus(new BattleStatusRequest
        {
            Source = caster.State,
            Target = caster.State,
            Type = BattleStatusType.MoveSpeed,
            Level = 30,
            Duration = 8f,
            IsDebuff = false,
            IsDispelAllowed = true
        });

        GameObject activeVfx = VFXManager.Instance.PlayEffect("SubmersionEffect", caster.Position);

        effects.ScheduleEffect(8f, caster, caster, context, (ctx, sink) =>
        {
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }

    // 침잠 전용 '타겟팅 불가' 장신구 구현체
    private class UntargetableArtifact : ITargetingModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void ModifyTargetScore(BattleUnitCombatState owner, ref BattleTargetScore score)
        {
            // 타겟팅 불가이므로 점수 보정은 무시합니다.
        }

        // 🌟 핵심: 시스템이 이 유닛을 타겟으로 잡아도 되냐고 물어볼 때 무조건 "안 돼(false)"라고 대답함
        public bool CanBeTargeted(BattleUnitCombatState owner, BattleRuntimeUnit requester, BattleTargetingReason reason)
        {
            return false;
        }
    }
}
