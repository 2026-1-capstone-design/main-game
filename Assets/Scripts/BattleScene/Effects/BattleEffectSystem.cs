using System.Collections.Generic;
using UnityEngine;

// 전투 효과의 실제 적용 지점이다.
// 피해 보정 장신구를 먼저 통과시킨 뒤 HP/치유/버프/넉백과 전투 결과 기록을 수행한다.
public sealed class BattleEffectSystem : IBattleEffectSink
{
    private readonly BattleArtifactSystem _artifacts;
    private BattleCombatResultBuffer _combatResults;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;

    public BattleEffectSystem(BattleArtifactSystem artifacts)
    {
        _artifacts = artifacts;
    }

    public void Configure(
        BattleCombatResultBuffer combatResults,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState
    )
    {
        _combatResults = combatResults;
        _runtimeUnitByState = runtimeUnitByState;
    }

    public void DealDamage(BattleDamageRequest request)
    {
        // 피해량을 바꾸는 장신구는 ApplyDamage 직전에만 개입한다.
        _artifacts?.ModifyDamage(ref request);

        BattleUnitCombatState target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        float requestedAmount = request.Amount;
        float finalAmount = Mathf.Max(0f, requestedAmount);
        target.ApplyDamage(finalAmount);

        BattleRuntimeUnit sourceRuntime = ResolveRuntimeUnit(request.Source);
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);

        _combatResults?.Add(
            new BattleCombatResult(sourceRuntime, targetRuntime, finalAmount, target.IsCombatDisabled, request.IsSkill)
        );
    }

    public void Heal(BattleHealRequest request)
    {
        BattleUnitCombatState target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        target.ApplyHeal(Mathf.Max(0f, request.Amount));
    }

    public void ApplyBuff(
        BattleUnitCombatState source,
        BattleUnitCombatState target,
        BuffType type,
        int level,
        float duration
    )
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.BuffApply(type, level, duration);
    }

    public void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.AddKnockback(direction, force);
    }

    public void PlayVisual(BattleVisualEffectRequest request)
    {
        // TODO
        // Presentation Layer 연결 지점.
        // 실제 파티클/사운드 재생기는 후속 PresentationSystem에서 구독한다.
    }

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
