using System.Collections.Generic;
using UnityEngine;

public sealed class SkillEffectApplier : IBattleEffectSink
{
    private BattleRuntimeUnit _caster;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;
    private BattleEffectSystem _effects;

    public IEnumerable<BattleUnitCombatState> AllUnits => _runtimeUnitByState.Keys;

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

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
