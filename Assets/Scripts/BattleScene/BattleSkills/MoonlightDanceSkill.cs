using System.Collections.Generic;
using UnityEngine;

public sealed class MoonlightDanceSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.MoonlightDance;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.dualHand };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 30f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null)
            return;

        List<BattleRuntimeUnit> validTargets = new List<BattleRuntimeUnit>();
        foreach (var unit in context.Units)
        {
            if (BattleFieldSnapshot.IsValidEnemyTarget(caster.State, unit?.State))
                validTargets.Add(unit);
        }
        if (validTargets.Count == 0)
            return;

        // 1.5초 동안 절대 타겟으로 잡히지 않는 아티팩트 부여 (유체화 로직 대체)
        effects.GrantTemporaryArtifact(caster, new UntargetableDanceArtifact(), 1.5f, context);
        GameObject activeVfx = VFXManager.Instance.PlayEffect("VanishEffect", caster.Position + Vector3.up);

        for (int i = 0; i < 3; i++)
        {
            float delay = i * 0.3f;
            effects.ScheduleEffect(
                delay,
                caster,
                caster,
                context,
                (ctx, sink) =>
                {
                    if (caster.State.IsCombatDisabled)
                        return;

                    BattleRuntimeUnit tgt = validTargets[Random.Range(0, validTargets.Count)];
                    if (tgt != null && !tgt.State.IsCombatDisabled)
                    {
                        sink.Teleport(caster.State, tgt.Position);
                        sink.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = caster.State,
                                Target = tgt.State,
                                Amount = caster.State.Attack * 1.5f,
                                IsSkill = true,
                            }
                        );
                        VFXManager.Instance.PlayEffect("CompactHit", tgt.Position + Vector3.up);

                    }
                }
            );
        }

        effects.ScheduleEffect(
            1.5f,
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

    private class UntargetableDanceArtifact : ITargetingModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;

        public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

        public void ModifyTargetScore(BattleUnitCombatState owner, ref BattleTargetScore score) { }

        public bool CanBeTargeted(
            BattleUnitCombatState owner,
            BattleRuntimeUnit requester,
            BattleTargetingReason reason
        ) => false;
    }
}
