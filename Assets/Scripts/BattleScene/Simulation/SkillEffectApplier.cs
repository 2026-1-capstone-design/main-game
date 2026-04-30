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

    public void ApplyBuff(BattleRuntimeUnit source, BattleRuntimeUnit target, BuffType type, int level, float duration)
    {
        _effects?.ApplyBuff(source, target, type, level, duration);
    }

    public void AddKnockback(BattleRuntimeUnit target, Vector3 direction, float force)
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
                Source = _caster,
                Target = targetRuntime,
                Amount = amount,
                SourceKind = BattleEffectSourceKind.Skill,
                DamageKind = BattleDamageKind.Direct,
                SkillId = _caster != null ? _caster.State.GetSkill() : WeaponSkillId.None,
                IsSkill = true,
            }
        );
    }

    public void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        if (targetRuntime == null)
            return;

        AddKnockback(targetRuntime, direction, force);
    }

    public void ApplyHeal(BattleUnitCombatState caster, float amount)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(caster);
        if (targetRuntime == null)
            return;

        Heal(
            new BattleHealRequest
            {
                Source = _caster,
                Target = targetRuntime,
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

        ApplyBuff(_caster, targetRuntime, type, level, duration);
    }

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
