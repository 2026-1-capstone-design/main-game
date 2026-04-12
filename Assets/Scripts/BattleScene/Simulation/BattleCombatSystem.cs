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
        BattleCombatResultBuffer results,
        BattleControlPlan[] controlPlans,
        BattleControlSourceRegistry controlSources
    )
    {
        if (units == null || results == null)
            return;

        results.Clear();
        ExecuteAttackPhase(units, runtimeUnitByState, results, controlPlans, controlSources);
        ExecuteSkillPhase(units, runtimeUnitByState, results, controlPlans, controlSources);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer results,
        BattleControlPlan[] controlPlans,
        BattleControlSourceRegistry controlSources
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;
            if (plan.UsesExplicitCombatCommands && plan.Command != BattleCombatCommand.BasicAttack)
                continue;

            BattleUnitCombatState target = plan.TargetEnemy != null ? plan.TargetEnemy : attacker.PlannedTargetEnemy;
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
            ConsumeCommand(controlSources, attacker.State, BattleCombatCommand.BasicAttack);

            results.Add(
                new BattleCombatResult(attacker, targetRuntime, actualDamage, target.IsCombatDisabled, wasSkill: false)
            );
        }
    }

    private void ExecuteSkillPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer results,
        BattleControlPlan[] controlPlans,
        BattleControlSourceRegistry controlSources
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;
            bool explicitSkillCommand = plan.UsesExplicitCombatCommands && plan.Command == BattleCombatCommand.Skill;
            if (plan.UsesExplicitCombatCommands && !explicitSkillCommand)
                continue;
            if (unit.SkillCooldownRemaining > 0f)
            {
                if (explicitSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
                }

                continue;
            }

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            if (skill == null || !skill.CanActivate(unit))
            {
                if (explicitSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
                }

                continue;
            }

            _skillEffectApplier.Configure(unit, runtimeUnitByState, results);
            skill.Apply(unit, _skillEffectApplier);

            unit.SetSkillState(unit.GetSkillAnimationDuration());
            unit.State.ResetSkillCooldown();
            unit.RaiseSkillActivated();
            ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
        }
    }

    private static void ConsumeCommand(
        BattleControlSourceRegistry controlSources,
        BattleUnitCombatState state,
        BattleCombatCommand command
    )
    {
        if (controlSources != null && controlSources.TryGet(state, out IBattleUnitControlSource source))
        {
            source.ConsumeCommand(state, command);
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
