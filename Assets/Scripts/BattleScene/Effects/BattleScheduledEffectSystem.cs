using System;
using System.Collections.Generic;

// 예약 효과 하나의 실행 시각, 주체, 대상, 실행 콜백을 담는 값이다.
public readonly struct BattleScheduledEffect
{
    public readonly int EffectId;
    public readonly float ExecuteAtBattleTime;
    public readonly BattleRuntimeUnit Source;
    public readonly BattleRuntimeUnit Target;
    public readonly Action<BattleEffectContext, IBattleEffectSink> Execute;

    public BattleScheduledEffect(
        int effectId,
        float executeAtBattleTime,
        BattleRuntimeUnit source,
        BattleRuntimeUnit target,
        Action<BattleEffectContext, IBattleEffectSink> execute
    )
    {
        EffectId = effectId;
        ExecuteAtBattleTime = executeAtBattleTime;
        Source = source;
        Target = target;
        Execute = execute;
    }
}

// 미래 battle time에 실행할 효과를 관리한다.
// 시뮬레이션 배속과 무관하게 BattleEffectContext.BattleTime 기준으로 실행된다.
public sealed class BattleScheduledEffectSystem
{
    private readonly List<BattleScheduledEffect> _scheduled = new List<BattleScheduledEffect>();
    private int _nextEffectId = 1;

    public void Clear()
    {
        _scheduled.Clear();
        _nextEffectId = 1;
    }

    public int Schedule(float delay, BattleScheduledEffect effect)
    {
        float executeAt = effect.ExecuteAtBattleTime;
        if (delay > 0f)
            executeAt += delay;

        int effectId = effect.EffectId != 0 ? effect.EffectId : _nextEffectId++;
        _scheduled.Add(new BattleScheduledEffect(effectId, executeAt, effect.Source, effect.Target, effect.Execute));
        return effectId;
    }

    public int Schedule(
        float delay,
        BattleRuntimeUnit source,
        BattleRuntimeUnit target,
        in BattleEffectContext context,
        Action<BattleEffectContext, IBattleEffectSink> execute
    )
    {
        return Schedule(delay, new BattleScheduledEffect(0, context.BattleTime, source, target, execute));
    }

    public void Tick(in BattleEffectContext context, IBattleEffectSink effects)
    {
        if (_scheduled.Count == 0)
            return;

        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            BattleScheduledEffect scheduled = _scheduled[i];
            if (scheduled.ExecuteAtBattleTime > context.BattleTime)
                continue;

            _scheduled.RemoveAt(i);
            if (scheduled.Execute == null)
                continue;

            BattleEffectContext executionContext = new BattleEffectContext(
                scheduled.Source,
                scheduled.Target,
                context.Snapshot,
                context.Units,
                context.BattleTime,
                context.BattleTick
            );
            scheduled.Execute(executionContext, effects);
        }
    }

    public void CancelForOwner(BattleRuntimeUnit owner)
    {
        if (owner == null)
            return;

        for (int i = _scheduled.Count - 1; i >= 0; i--)
        {
            BattleScheduledEffect effect = _scheduled[i];
            if (effect.Source == owner || effect.Target == owner)
                _scheduled.RemoveAt(i);
        }
    }
}
