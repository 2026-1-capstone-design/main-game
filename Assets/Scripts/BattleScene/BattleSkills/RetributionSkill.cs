using System.Collections.Generic;
using UnityEngine;

public sealed class RetributionSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Retribution;
    public skillType SkillCategory => skillType.enhance;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.shield };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.Self;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        if (caster == null || caster.State.IsCombatDisabled)
            return;

        // 1. 데미지를 축적할 장신구를 만들고, 5초간 부여
        RetributionArtifact artifact = new RetributionArtifact();
        effects.GrantTemporaryArtifact(caster, artifact, 5f, context);

        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = caster.State,
                Target = caster.State,
                Type = BattleStatusType.DamageReductionPercent,
                Level = 50,
                Duration = 5f,
            }
        );
        GameObject activeVfx = VFXManager.Instance.PlayEffect("RetributionStance", caster.Position);

        // 2. 5초 뒤에 예약된 행동: 방금 만든 그 장신구에서 모아둔 데미지를 읽어와서 폭발!
        effects.ScheduleEffect(
            5f,
            caster,
            caster,
            context,
            (ctx, sink) =>
            {
                if (caster.State.IsCombatDisabled)
                    return;

                if (activeVfx != null)
                    VFXManager.Instance.StopEffect(activeVfx);

                VFXManager.Instance.PlayEffect("RetributionExplosion", caster.Position);
                float explosionDamage = artifact.AccumulatedDamage * 0.5f;

                foreach (BattleRuntimeUnit unitView in ctx.Units)
                {
                    if (
                        BattleFieldSnapshot.IsValidEnemyTarget(caster.State, unitView.State)
                        && Vector3.Distance(caster.Position, unitView.Position) <= AreaRadius
                    )
                    {
                        sink.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = caster.State,
                                Target = unitView.State,
                                Amount = explosionDamage,
                                SourceKind = BattleEffectSourceKind.Skill,
                                DamageKind = BattleDamageKind.Area,
                                IsSkill = true,
                                IsArea = true,
                            }
                        );
                    }
                }
            }
        );
    }

    private class RetributionArtifact : IDamageReactionArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        public float AccumulatedDamage { get; private set; } = 0f;

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
        {
            if (result.Target == owner)
            {
                AccumulatedDamage += result.FinalAmount; // 내가 맞은 최종 피해 누적
            }
        }
    }
}
