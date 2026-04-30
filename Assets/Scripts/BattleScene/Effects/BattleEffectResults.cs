// 피해 처리 결과를 보존하기 위한 값 타입이다.
// 현재 전투 로그는 BattleCombatResult를 쓰지만, 요청량/최종량 분리가 필요한 후속 훅에서 사용한다.
public readonly struct BattleDamageResult
{
    public BattleRuntimeUnit Source { get; }
    public BattleRuntimeUnit Target { get; }
    public float RequestedAmount { get; }
    public float FinalAmount { get; }
    public bool TargetDied { get; }

    public BattleDamageResult(
        BattleRuntimeUnit source,
        BattleRuntimeUnit target,
        float requestedAmount,
        float finalAmount,
        bool targetDied
    )
    {
        Source = source;
        Target = target;
        RequestedAmount = requestedAmount;
        FinalAmount = finalAmount;
        TargetDied = targetDied;
    }
}
