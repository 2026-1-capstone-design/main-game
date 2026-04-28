using System.Collections.Generic;

public sealed class BattleCombatSystem
{
    private readonly BattleSkillRegistry _skillRegistry;
    private readonly SkillEffectApplier _skillEffectApplier;

    public BattleCombatSystem(SkillEffectApplier skillEffectApplier)
    {
        _skillEffectApplier = skillEffectApplier ?? new SkillEffectApplier();
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
        BattleCombatResultBuffer results
    )
    {
        if (units == null || results == null)
            return;

        results.Clear();
        ExecuteAttackPhase(units, runtimeUnitByState, results);
        ExecuteSkillPhase(units, runtimeUnitByState, results);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer results
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;
            if (attacker.UsesExternalAgentControl && !attacker.HasExternalAttackCommand)
                continue;

            BattleUnitCombatState target = attacker.PlannedTargetEnemy;
            if (!BattleFieldSnapshot.IsValidEnemyTarget(attacker.State, target))
                continue;
            if (!BattleFieldSnapshot.IsWithinEffectiveAttackDistance(attacker.State, target))
                continue;
            if (attacker.AttackCooldownRemaining > 0f)
                continue;

            attacker.State.SetAttackState(true);

            float damage = attacker.Attack;
            float actualDamage = target.ApplyDamage(damage);
            BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(runtimeUnitByState, target);
            attacker.RaiseAttackLanded(targetRuntime, actualDamage, target.IsCombatDisabled);
            attacker.State.ResetAttackCooldown();
            attacker.ClearExternalAttackCommand();

            results.Add(
                new BattleCombatResult(attacker, targetRuntime, actualDamage, target.IsCombatDisabled, wasSkill: false)
            );
        }
    }

    private void ExecuteSkillPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer results
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;
            bool externalSkillCommand = unit.UsesExternalAgentControl && unit.HasExternalSkillCommand;
            if (unit.UsesExternalAgentControl && !externalSkillCommand)
                continue;
            if (unit.SkillCooldownRemaining > 0f)
            {
                if (externalSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    unit.ClearExternalSkillCommand();
                }

                continue;
            }

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            if (skill == null || !skill.CanActivate(unit))
            {
                if (externalSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    unit.ClearExternalSkillCommand();
                }

                continue;
            }

            _skillEffectApplier.Configure(unit, runtimeUnitByState, results);
            skill.Apply(unit, _skillEffectApplier);

            unit.SetSkillState(unit.GetSkillAnimationDuration());
            unit.State.ResetSkillCooldown();
            unit.RaiseSkillActivated();
            unit.ClearExternalSkillCommand();
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
