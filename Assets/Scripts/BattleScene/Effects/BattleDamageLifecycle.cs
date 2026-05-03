using System.Collections.Generic;

// 피해 적용 전 훅이 이후 파이프라인에 요구하는 처리 방향이다.
public enum BattleDamageResolution
{
    Continue,
    Ignore,
    Redirect,
    ClampToMinimumHealth,
}

// 피해가 HP에 적용되기 전에 무시, 리다이렉트, 하한 클램프 여부를 결정하는 훅이다.
public interface IDamageRedirectEffect
{
    BattleDamageResolution BeforeDamage(ref BattleDamageRequest request, IBattleEffectSink effects);
}

// 치명 피해가 전투불능 처리로 이어지기 직전에 개입하는 훅이다.
public interface ILethalDamageInterceptor
{
    bool TryPreventLethalDamage(BattleRuntimeUnit owner, ref BattleDamageRequest request, IBattleEffectSink effects);
}

// 최종 피해 결과를 장기 실행 효과의 누적값으로 기록하는 훅이다.
public interface IDamageAccumulatorEffect
{
    void Accumulate(BattleRuntimeUnit owner, in BattleDamageResult result);
}

// 피해 적용 전후의 장기 실행 효과 훅을 한 지점에 모은다.
// 실제 효과 구현은 후속 플랜에서 등록하고, 이 클래스는 빈 등록 상태에서 기존 피해 경로를 보존한다.
public sealed class BattleDamageLifecycle
{
    private readonly List<IDamageRedirectEffect> _redirects = new List<IDamageRedirectEffect>();
    private readonly List<BattleRuntimeUnit> _lethalOwners = new List<BattleRuntimeUnit>();
    private readonly List<ILethalDamageInterceptor> _lethalInterceptors = new List<ILethalDamageInterceptor>();
    private readonly List<BattleRuntimeUnit> _accumulatorOwners = new List<BattleRuntimeUnit>();
    private readonly List<IDamageAccumulatorEffect> _accumulators = new List<IDamageAccumulatorEffect>();

    public void Clear()
    {
        _redirects.Clear();
        _lethalOwners.Clear();
        _lethalInterceptors.Clear();
        _accumulatorOwners.Clear();
        _accumulators.Clear();
    }

    public void RegisterRedirect(IDamageRedirectEffect effect)
    {
        if (effect != null && !_redirects.Contains(effect))
            _redirects.Add(effect);
    }

    public void RegisterLethalInterceptor(BattleRuntimeUnit owner, ILethalDamageInterceptor effect)
    {
        if (owner == null || effect == null)
            return;

        _lethalOwners.Add(owner);
        _lethalInterceptors.Add(effect);
    }

    public void RegisterAccumulator(BattleRuntimeUnit owner, IDamageAccumulatorEffect effect)
    {
        if (owner == null || effect == null)
            return;

        _accumulatorOwners.Add(owner);
        _accumulators.Add(effect);
    }

    public BattleDamageResolution BeforeDamage(ref BattleDamageRequest request, IBattleEffectSink effects)
    {
        // 이미 리다이렉트된 피해는 훅을 다시 통과하지 않는다.
        if (request.IsRedirected)
            return BattleDamageResolution.Continue;

        for (int i = 0; i < _redirects.Count; i++)
        {
            BattleDamageResolution resolution = _redirects[i].BeforeDamage(ref request, effects);
            if (resolution != BattleDamageResolution.Continue)
                return resolution;
        }

        return BattleDamageResolution.Continue;
    }

    public bool TryPreventLethalDamage(
        BattleRuntimeUnit target,
        ref BattleDamageRequest request,
        IBattleEffectSink effects
    )
    {
        if (target == null || request.Target == null)
            return false;

        for (int i = 0; i < _lethalInterceptors.Count; i++)
        {
            if (_lethalOwners[i] != target)
                continue;

            if (_lethalInterceptors[i].TryPreventLethalDamage(target, ref request, effects))
                return true;
        }

        return false;
    }

    public void Accumulate(in BattleDamageResult result)
    {
        for (int i = 0; i < _accumulators.Count; i++)
        {
            _accumulators[i].Accumulate(_accumulatorOwners[i], result);
        }
    }
}
