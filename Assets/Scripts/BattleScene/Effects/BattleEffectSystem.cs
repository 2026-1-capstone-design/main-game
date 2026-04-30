using UnityEngine;

// 전투 효과의 실제 적용 지점이다.
// 피해 보정 장신구를 먼저 통과시킨 뒤 HP/치유/버프/넉백과 전투 결과 기록을 수행한다.
public sealed class BattleEffectSystem : IBattleEffectSink
{
    private readonly BattleArtifactSystem _artifacts;
    private BattleCombatResultBuffer _combatResults;

    public BattleEffectSystem(BattleArtifactSystem artifacts)
    {
        _artifacts = artifacts;
    }

    public void Configure(BattleCombatResultBuffer combatResults)
    {
        _combatResults = combatResults;
    }

    public void DealDamage(BattleDamageRequest request)
    {
        // 피해량을 바꾸는 장신구는 ApplyDamage 직전에만 개입한다.
        _artifacts?.ModifyDamage(ref request);

        BattleRuntimeUnit target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        float requestedAmount = request.Amount;
        float finalAmount = Mathf.Max(0f, requestedAmount);
        target.State.ApplyDamage(finalAmount);

        _combatResults?.Add(
            new BattleCombatResult(request.Source, target, finalAmount, target.IsCombatDisabled, request.IsSkill)
        );
    }

    public void Heal(BattleHealRequest request)
    {
        BattleRuntimeUnit target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        target.State.ApplyHeal(Mathf.Max(0f, request.Amount));
    }

    public void ApplyBuff(BattleRuntimeUnit source, BattleRuntimeUnit target, BuffType type, int level, float duration)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.State.BuffApply(type, level, duration);
    }

    public void AddKnockback(BattleRuntimeUnit target, Vector3 direction, float force)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.State.AddKnockback(direction, force);
    }
}
