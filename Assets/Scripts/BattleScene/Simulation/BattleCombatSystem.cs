using System.Collections.Generic;

public sealed class BattleCombatSystem
{
    private readonly BattleSkillRegistry _skillRegistry;
    private readonly BattleEffectSystem _effects;
    private readonly BattleSkillChannelSystem _channelSystem;
    private readonly BattleArtifactSystem _artifactSystem;
    private readonly BattleRosterMutationSystem _rosterMutationSystem;

    public BattleCombatSystem(
        BattleEffectSystem effects,
        BattleSkillChannelSystem channelSystem = null,
        BattleArtifactSystem artifactSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        _effects = effects;
        _channelSystem = channelSystem;
        _artifactSystem = artifactSystem;
        _rosterMutationSystem = rosterMutationSystem;
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
        int battleTick,
        BattleControlPlan[] controlPlans = null,
        BattleControlSourceRegistry controlSources = null
    )
    {
        if (units == null || results == null || _effects == null)
            return;

        results.Clear();
        _effects.Configure(results, runtimeUnitByState);
        ExecuteAttackPhase(units, runtimeUnitByState, snapshot, _effects, _channelSystem, _artifactSystem, controlPlans, controlSources);
        ExecuteSkillPhase(units, runtimeUnitByState, snapshot, battleTime, battleTick, controlPlans, controlSources);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        IBattleEffectSink effects,
        BattleSkillChannelSystem channelSystem,
        BattleArtifactSystem artifactSystem,
        BattleControlPlan[] controlPlans,
        BattleControlSourceRegistry controlSources
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;
            if (channelSystem != null && channelSystem.IsBasicAttackBlocked(attacker))
                continue;

            BattleControlPlan plan = GetPlan(controlPlans, i);
            if (plan.UsesExplicitCombatCommands && plan.Command != BattleCombatCommand.BasicAttack)
                continue;

            BattleUnitCombatState target = plan.TargetEnemy ?? attacker.PlannedTargetEnemy;
            if (
                artifactSystem != null
                && artifactSystem.TryOverrideBasicAttackTarget(attacker, snapshot, out BattleRuntimeUnit overrideTarget)
            )
            {
                target = overrideTarget != null ? overrideTarget.State : null;
            }

            if (snapshot != null)
            {
                if (!snapshot.CanTarget(attacker.State, target, BattleTargetingReason.BasicAttack))
                    continue;
            }
            else if (!BattleFieldSnapshot.IsValidEnemyTarget(attacker.State, target))
            {
                continue;
            }
            if (!BattleFieldSnapshot.IsWithinEffectiveAttackDistance(attacker.State, target))
                continue;
            if (attacker.AttackCooldownRemaining > 0f)
                continue;

            attacker.State.SetAttackState(true);

            float healthBeforeDamage = target.CurrentHealth;
            bool wasDisabledBeforeDamage = target.IsCombatDisabled;
            BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(runtimeUnitByState, target);
            effects.DealDamage(
                new BattleDamageRequest
                {
                    Source = attacker.State,
                    Target = target,
                    Amount = attacker.Attack,
                    SourceKind = BattleEffectSourceKind.BasicAttack,
                    DamageKind = BattleDamageKind.Direct,
                    SkillId = WeaponSkillId.None,
                    ArtifactId = ArtifactId.None,
                    IsBasicAttack = true,
                }
            );

            float actualDamage = UnityEngine.Mathf.Max(0f, healthBeforeDamage - target.CurrentHealth);
            bool wasKill = !wasDisabledBeforeDamage && target.IsCombatDisabled;
            attacker.RaiseAttackLanded(targetRuntime, actualDamage, wasKill);
            attacker.State.ResetAttackCooldown();
            ConsumeCommand(controlSources, attacker.State, BattleCombatCommand.BasicAttack);
        }
    }

    private void ExecuteSkillPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        float battleTime,
        int battleTick,
        BattleControlPlan[] controlPlans,
        BattleControlSourceRegistry controlSources
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned)
                continue;
            if (_channelSystem != null && _channelSystem.IsChanneling(unit))
                continue;

            BattleControlPlan plan = GetPlan(controlPlans, i);
            bool explicitSkillCommand = plan.UsesExplicitCombatCommands && plan.Command == BattleCombatCommand.Skill;
            if (plan.UsesExplicitCombatCommands && !explicitSkillCommand)
                continue;
            if (
                unit.State.IsSkillDisabled
                || (_rosterMutationSystem != null && _rosterMutationSystem.IsSkillDisabled(unit))
            )
            {
                if (explicitSkillCommand)
                    ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
                continue;
            }
            if (unit.SkillCooldownRemaining > 0f)
            {
                if (explicitSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
                }

                continue;
            }

            BattleRuntimeUnit primaryTarget = ResolveRuntimeUnit(runtimeUnitByState, plan.TargetEnemy ?? unit.PlannedTargetEnemy);
            BattleEffectContext context = new BattleEffectContext(
                unit,
                primaryTarget,
                snapshot,
                units,
                battleTime,
                battleTick
            );

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            if (skill == null || !skill.CanActivate(context))
            {
                if (explicitSkillCommand)
                {
                    unit.RaiseSkillFailed();
                    ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
                }

                continue;
            }

            if (skill is IChanneledBattleSkill channeledSkill)
            {
                _channelSystem?.StartChannel(unit, channeledSkill, context, _effects);
            }
            else
            {
                skill.Activate(context, _effects);
            }

            _effects.NotifySkillCast(
                new BattleSkillCastEvent(unit.State, unit, unit.State.GetSkill(), primaryTarget, snapshot)
            );

            unit.SetSkillState(unit.GetSkillAnimationDuration());
            unit.State.ResetSkillCooldown();
            unit.RaiseSkillActivated();
            ConsumeCommand(controlSources, unit.State, BattleCombatCommand.Skill);
        }
    }

    private static BattleControlPlan GetPlan(BattleControlPlan[] controlPlans, int index)
    {
        return controlPlans != null && index >= 0 && index < controlPlans.Length ? controlPlans[index] : default;
    }

    private static void ConsumeCommand(
        BattleControlSourceRegistry controlSources,
        BattleUnitCombatState state,
        BattleCombatCommand command
    )
    {
        if (controlSources != null && controlSources.TryGet(state, out IBattleUnitControlSource source))
            source.ConsumeCommand(state, command);
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
