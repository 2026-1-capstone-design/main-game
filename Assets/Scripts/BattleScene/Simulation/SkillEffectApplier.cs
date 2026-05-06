using System.Collections.Generic;
using UnityEngine;

public sealed class SkillEffectApplier : IBattleEffectSink
{
    private BattleRuntimeUnit _caster;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;
    private BattleEffectSystem _effects;

    public IEnumerable<BattleUnitCombatState> AllUnits => _runtimeUnitByState.Keys;
    public IBattleRosterMutationSink RosterMutations => _effects != null ? _effects.RosterMutations : null;

    public void Configure(
        BattleRuntimeUnit caster,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleEffectSystem effects
    )
    {
        _caster = caster;
        _runtimeUnitByState = runtimeUnitByState;
        _effects = effects;
    }

    public void DealDamage(BattleDamageRequest request)
    {
        _effects?.DealDamage(request);
    }

    public void Heal(BattleHealRequest request)
    {
        _effects?.Heal(request);
    }

    public void ApplyStatus(BattleStatusRequest request)
    {
        _effects?.ApplyStatus(request);
    }

    public void ApplyBuff(
        BattleUnitCombatState source,
        BattleUnitCombatState target,
        BuffType type,
        int level,
        float duration
    )
    {
        _effects?.ApplyBuff(source, target, type, level, duration);
    }

    public void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
    {
        _effects?.AddKnockback(target, direction, force);
    }

    public void Dispel(BattleUnitCombatState target, BattleDispelFilter filter)
    {
        _effects?.Dispel(target, filter);
    }

    public void RefreshStatuses(BattleUnitCombatState target, BattleStatusFilter filter, float duration)
    {
        _effects?.RefreshStatuses(target, filter, duration);
    }

    public void Revive(BattleUnitCombatState target, float health)
    {
        _effects?.Revive(target, health);
    }

    public void Teleport(BattleUnitCombatState target, Vector3 destination)
    {
        _effects?.Teleport(target, destination);
    }

    public void PullTo(BattleUnitCombatState source, BattleUnitCombatState target, float stopDistance)
    {
        _effects?.PullTo(source, target, stopDistance);
    }

    public void PushToArenaEdge(BattleUnitCombatState source, BattleUnitCombatState target, float slowDuration)
    {
        _effects?.PushToArenaEdge(source, target, slowDuration);
    }

    public void ApplyDamage(BattleUnitCombatState target, float amount)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        if (targetRuntime == null)
            return;

        DealDamage(
            new BattleDamageRequest
            {
                Source = _caster != null ? _caster.State : null,
                Target = target,
                Amount = amount,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = _caster != null ? _caster.State.GetSkill() : WeaponSkillId.None,
                IsSkill = true,
            }
        );
    }

    public void ApplyHeal(BattleUnitCombatState caster, float amount)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(caster);
        if (targetRuntime == null)
            return;

        Heal(
            new BattleHealRequest
            {
                Source = _caster != null ? _caster.State : null,
                Target = caster,
                Amount = amount,
                SourceKind = BattleEffectSourceKind.Skill,
            }
        );
    }

    public void ApplyBuff(BattleUnitCombatState caster, BuffType type, int level, float duration)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(caster);
        if (targetRuntime == null)
            return;

        ApplyBuff(_caster != null ? _caster.State : null, caster, type, level, duration);
    }

    public void PlayVisual(BattleVisualEffectRequest request)
    {
        _effects?.PlayVisual(request);
    }

    public int ScheduleEffect(
        float delay,
        BattleRuntimeUnit source,
        BattleRuntimeUnit target,
        in BattleEffectContext context,
        System.Action<BattleEffectContext, IBattleEffectSink> execute
    )
    {
        return _effects != null ? _effects.ScheduleEffect(delay, source, target, context, execute) : 0;
    }

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
