using System.Collections.Generic;
using UnityEngine;

public sealed class BattleCombatSystem
{
    private readonly BattleSkillRegistry _skillRegistry;
    private readonly BattleEffectSystem _effects;
    private readonly BattleSkillChannelSystem _channelSystem;
    private readonly BattleArtifactSystem _artifactSystem;
    private readonly BattleRosterMutationSystem _rosterMutationSystem;
    private readonly bool _skillsEnabled;

    public BattleCombatSystem(
        BattleEffectSystem effects,
        BattleSkillChannelSystem channelSystem = null,
        BattleArtifactSystem artifactSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null,
        bool skillsEnabled = true
    )
    {
        _effects = effects;
        _channelSystem = channelSystem;
        _artifactSystem = artifactSystem;
        _rosterMutationSystem = rosterMutationSystem;
        _skillsEnabled = skillsEnabled;
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
                new HighHealSkill(),
                new DarkShroudSkill(),
                new OminousStarSkill(),
                new ConsecrationSkill(),
                new WarCommanderSkill(),
                new HuntStartSkill(),
                new LeapOfFaithSkill(),
                new HookThrowSkill(),
                new ContinuousSlashSkill(),
                new ParryingSkill(),
                new ManaCollapseSkill(),
                new HolyBarrierSkill(),
                new FearSkill(),
                new MagicExplosionSkill(),
                new RetributionSkill(),
                new SubmersionSkill(),
                new CurseSkill(),
                new GlyphOfCounterattackSkill(),
                new NobleSacrificeSkill(),
                new DuelSkill(),
                //new FreezeSkill(),
                //new MindControlSkill(),
                new OblivionSkill(),
                //new RespiteSkill(),
                new MoonlightDanceSkill(),
                //new FanaticalObsessionSkill()
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
        bool clearResults = true,
        bool projectilesEnabled = true
    )
    {
        if (units == null || results == null || _effects == null)
            return;

        if (clearResults)
            results.Clear();
        _effects.Configure(results, runtimeUnitByState);
        ExecuteAttackPhase(
            units,
            runtimeUnitByState,
            snapshot,
            _effects,
            _channelSystem,
            _artifactSystem,
            controlPlans,
            projectilesEnabled
        );
        if (_skillsEnabled)
            ExecuteSkillPhase(units, runtimeUnitByState, snapshot, battleTime, battleTick, controlPlans);
    }

    private static void ExecuteAttackPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        IBattleEffectSink effects,
        BattleSkillChannelSystem channelSystem,
        BattleArtifactSystem artifactSystem,
        BattleControlPlan[] controlPlans,
        bool projectilesEnabled
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit attacker = units[i];
            if (attacker == null || attacker.IsCombatDisabled || attacker.State.IsStunned)
                continue;

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;
            if (plan.CombatIntent != BattleCombatIntent.Attack)
                continue;

            if (channelSystem != null && channelSystem.IsBasicAttackBlocked(attacker))
                continue;

            BattleUnitCombatState target = plan.TargetEnemy;
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
            if (attacker.IsAttacking)
                continue;
            if (attacker.AttackCooldownRemaining > 0f)
                continue;

            attacker.State.SetAttackState(true);

            float healthBeforeDamage = target.CurrentHealth;
            BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(runtimeUnitByState, target);

            BattleDamageRequest damageRequest = new BattleDamageRequest
            {
                Source = attacker.State,
                Target = target,
                Amount = attacker.Attack,
                SourceKind = BattleEffectSourceKind.BasicAttack,
                DamageKind = BattleDamageKind.Direct,
                SkillId = WeaponSkillId.None,
                ArtifactId = ArtifactId.None,
                IsBasicAttack = true,
            };

            bool didLaunchProjectile = false;
            bool shouldUseProjectile =
                projectilesEnabled && attacker.Snapshot != null && attacker.Snapshot.UseProjectile;
            if (shouldUseProjectile)
            {
                Vector3 startPos = attacker.Position + Vector3.up;
                Vector3 fireDirection = target.Position - startPos;
                fireDirection.y = 0f;

                float windUpDelay = attacker.Snapshot.WeaponType == WeaponType.bow ? 0.3f : 0.3f;

                didLaunchProjectile =
                    BattleSimulationManager.Instance != null
                    && BattleSimulationManager.Instance.TryLaunchBasicProjectile(
                        damageRequest,
                        startPos,
                        fireDirection,
                        attacker.Snapshot.WeaponType,
                        windUpDelay
                    );
            }

            if (!didLaunchProjectile)
            {
                effects.DealDamage(damageRequest);
                float actualDamage = Mathf.Max(0f, healthBeforeDamage - target.CurrentHealth);
                attacker.RaiseAttackLanded(targetRuntime, actualDamage, target.IsCombatDisabled);
            }

            attacker.State.ResetAttackCooldown();
        }
    }

    private void ExecuteSkillPhase(
        IReadOnlyList<BattleRuntimeUnit> units,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleFieldSnapshot snapshot,
        float battleTime,
        int battleTick,
        BattleControlPlan[] controlPlans
    )
    {
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled || unit.State.IsStunned || unit.IsAttacking)
                continue;
            if (_channelSystem != null && _channelSystem.IsChanneling(unit))
                continue;

            BattleControlPlan plan = controlPlans != null && i < controlPlans.Length ? controlPlans[i] : default;
            if (plan.CombatIntent != BattleCombatIntent.Skill)
                continue;
            if (
                unit.State.IsSkillDisabled
                || (_rosterMutationSystem != null && _rosterMutationSystem.IsSkillDisabled(unit))
            )
                continue;
            if (unit.SkillCooldownRemaining > 0f)
            {
                unit.RaiseSkillFailed();
                continue;
            }

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            BattleUnitCombatState targetState = plan.TargetEnemy;
            BattleRuntimeUnit primaryTarget = ResolveRuntimeUnit(runtimeUnitByState, targetState);
            BattleEffectContext context = new BattleEffectContext(
                unit,
                primaryTarget,
                snapshot,
                units,
                battleTime,
                battleTick
            );

            if (skill == null || !skill.CanActivate(context))
            {
                unit.RaiseSkillFailed();
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
