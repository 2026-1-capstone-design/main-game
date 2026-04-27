using System.Collections.Generic;
using UnityEngine;

public sealed class SkillEffectApplier : ISkillEffectApplier
{
    private BattleRuntimeUnit _caster;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;
    private BattleCombatResultBuffer _combatResults;

    public IEnumerable<BattleUnitCombatState> AllUnits => _runtimeUnitByState.Keys;

    public void Configure(
        BattleRuntimeUnit caster,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        BattleCombatResultBuffer combatResults
    )
    {
        _caster = caster;
        _runtimeUnitByState = runtimeUnitByState;
        _combatResults = combatResults;
    }

    public void ApplyDamage(BattleUnitCombatState target, float amount)
    {
        if (target == null)
            return;

        target.ApplyDamage(amount);
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);

        if (_combatResults != null)
        {
            _combatResults.Add(
                new BattleCombatResult(_caster, targetRuntime, amount, target.IsCombatDisabled, wasSkill: true)
            );
        }
    }

    public void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
    {
        if (target == null)
            return;

        target.AddKnockback(direction, force);
    }

    public void ApplyHeal(BattleUnitCombatState caster, float amount)
    {
        if (caster == null)
            return;

        caster.ApplyHeal(amount);
    }

    public void ApplyBuff(BattleUnitCombatState caster, BuffType type, int level, float duration)
    {
        if (caster == null)
            return;

        caster.BuffApply(type, level, duration);
    }

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
