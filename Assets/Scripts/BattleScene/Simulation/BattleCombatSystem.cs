using System.Collections.Generic;

public sealed class BattleCombatSystem
{
    private readonly BattleSkillRegistry _skillRegistry;
    private readonly BattleEffectSystem _effects;

    public BattleCombatSystem(BattleEffectSystem effects)
    {
        _effects = effects;
        _skillRegistry = new BattleSkillRegistry(
            new IBattleSkill[]
            {
                new HeartAttackSkill(),
                new MadnessSkill(),
                new BayonetChargeSkill(),
                new FireballSkill(),
                new HeadStrikeSkill(),
                new LightningSkill(),
                new LongGripSkill(),
                new RevolverFanningSkill(),
                new RustyBladeSkill(),
                new ShieldBashSkill(),
                new SpiralSlashSkill(),
                new StimpackSkill(),
                new ThroatSlitSkill(),
                new WarcrySkill(),
            }
        );
    }

    public void Execute(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer results,
        BattleFieldSnapshot snapshot,
        float battleTime,
        int battleTick
    )
    {
        if (units == null || results == null || _effects == null)
            return;

        results.Clear();
        _effects.Configure(results);
        ExecuteAttackPhase(units, runtimeUnitByState, _effects);
        ExecuteSkillPhase(units, runtimeUnitByState, snapshot, battleTime, battleTick);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        IBattleEffectSink effects
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;

            BattleUnitCombatState target = attacker.PlannedTargetEnemy;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(attacker.State, target))
                continue;
            if (!BattleFieldSnapshot.IsWithinEffectiveAttackDistance(attacker.State, target))
                continue;
            if (attacker.AttackCooldownRemaining > 0f)
                continue;

            attacker.State.SetAttackState(true);

            BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(runtimeUnitByState, target);
            effects.DealDamage(
                new BattleDamageRequest
                {
                    Source = attacker,
                    Target = targetRuntime,
                    Amount = attacker.Attack,
                    SourceKind = BattleEffectSourceKind.BasicAttack,
                    DamageKind = BattleDamageKind.Direct,
                    SkillId = WeaponSkillId.None,
                    ArtifactId = ArtifactId.None,
                    IsBasicAttack = true,
                }
            );

            attacker.RaiseAttackLanded(targetRuntime, target.IsCombatDisabled);
            attacker.State.ResetAttackCooldown();
        }
    }

    private void ExecuteSkillPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        float battleTime,
        int battleTick
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;
            if (unit.IsExternallyControlled)
                continue;
            if (unit.SkillCooldownRemaining > 0f)
                continue;

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            BattleRuntimeUnit primaryTarget = ResolveRuntimeUnit(runtimeUnitByState, unit.PlannedTargetEnemy);
            BattleEffectContext context = new BattleEffectContext(
                unit,
                primaryTarget,
                snapshot,
                units,
                battleTime,
                battleTick
            );

            if (skill == null || !skill.CanActivate(context))
                continue;

            skill.Activate(context, _effects);

            unit.SetSkillState(unit.GetSkillAnimationDuration());
            unit.State.ResetSkillCooldown();
        }
    }

    private static BattleRuntimeUnit ResolveRuntimeUnit(
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleUnitCombatState state
    )
    {
        if (state == null || runtimeUnitByState == null)
            return null;

        return runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
