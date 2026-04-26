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
        int battleTick
    )
    {
        if (units == null || results == null || _effects == null)
            return;

        results.Clear();
        _effects.Configure(results, runtimeUnitByState);
        ExecuteAttackPhase(units, runtimeUnitByState, snapshot, _effects, _channelSystem, _artifactSystem);
        ExecuteSkillPhase(units, runtimeUnitByState, snapshot, battleTime, battleTick);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        IBattleEffectSink effects,
        BattleSkillChannelSystem channelSystem,
        BattleArtifactSystem artifactSystem
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;
            if (channelSystem != null && channelSystem.IsBasicAttackBlocked(attacker))
                continue;

            BattleUnitCombatState target = attacker.PlannedTargetEnemy;
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
            if (_channelSystem != null && _channelSystem.IsChanneling(unit))
                continue;
            if (unit.UsesExternalAgentControl)
                continue;
            if (
                unit.State.IsSkillDisabled
                || (_rosterMutationSystem != null && _rosterMutationSystem.IsSkillDisabled(unit))
            )
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
