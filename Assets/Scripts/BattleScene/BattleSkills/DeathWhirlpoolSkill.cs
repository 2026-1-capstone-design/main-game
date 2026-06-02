using System.Collections.Generic;
using UnityEngine;

public sealed class DeathWhirlpoolSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.DeathWhirlpool;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } =
        new[] { WeaponType.handGun, WeaponType.dualGun, WeaponType.rifle };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.AreaAroundSelf;
    public float CastRange => 0f;
    public float AreaRadius => 15f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime?.State;
        if (casterState == null || casterState.IsCombatDisabled)
            return;

        effects.RosterMutations.DisableCommandAndSkill(casterRuntime, 3.0f);

        for (int i = 0; i < 15; i++)
        {
            float delay = i * 0.1f;
            effects.ScheduleEffect(
                delay,
                casterRuntime,
                casterRuntime,
                context,
                (ctx, sink) =>
                {
                    if (casterState.IsCombatDisabled)
                        return;

                    List<BattleUnitCombatState> validEnemies = new List<BattleUnitCombatState>();
                    foreach (BattleRuntimeUnit unitView in ctx.Units)
                    {
                        BattleUnitCombatState target = unitView?.State;
                        if (
                            BattleFieldSnapshot.IsValidEnemyTarget(casterState, target)
                            && Vector3.Distance(casterState.Position, target.Position) <= AreaRadius
                        )
                        {
                            validEnemies.Add(target);
                        }
                    }

                    if (validEnemies.Count > 0)
                    {
                        BattleUnitCombatState randomTarget = validEnemies[Random.Range(0, validEnemies.Count)];
                        sink.DealDamage(
                            new BattleDamageRequest
                            {
                                Source = casterState,
                                Target = randomTarget,
                                Amount = casterState.Attack * 0.2f,
                                SourceKind = BattleEffectSourceKind.Skill,
                                DamageKind = BattleDamageKind.Direct,
                                SkillId = SkillId,
                                IsSkill = true,
                            }
                        );
                        VFXManager.Instance.PlayEffect("CompactHit", randomTarget.Position + Vector3.up);
                    }
                }
            );
        }
    }
}
