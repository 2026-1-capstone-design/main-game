using System.Collections.Generic;
using UnityEngine;

public sealed class HolyBarrierSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.HolyBarrier;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null) return;

        effects.GrantTemporaryArtifact(caster, new HolyBarrierArtifact(caster), 10f, context);


        GameObject activeVfx = VFXManager.Instance.PlayEffect("HolyBarrierShield", caster.Position);

        // 3. 10초 뒤 이펙트 종료
        effects.ScheduleEffect(10f, caster, caster, context, (ctx, sink) =>
        {
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }

    private class HolyBarrierArtifact : IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleRuntimeUnit _ownerView;

        public HolyBarrierArtifact(BattleRuntimeUnit ownerView) { _ownerView = ownerView; }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Target == owner && result.Source != null && result.Source != owner)
            {
                // Source 쪽으로 방향 벡터 계산을 위해 위치 필요 (임의로 뒤로 밀쳐낸다고 가정)
                Vector3 pushDir = Vector3.forward; // 실제로는 Source의 위치 좌표를 가져와서 계산
                effects.AddKnockback(result.Source, pushDir, 20f);
            }
        }
    }
}
