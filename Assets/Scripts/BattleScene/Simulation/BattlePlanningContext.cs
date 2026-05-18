using System.Collections.Generic;

// planner가 runtime actor와 snapshot, tuning에 접근할 때 사용하는 읽기 전용 컨텍스트다.
public readonly struct BattlePlanningContext
{
    public IReadOnlyList<BattleRuntimeUnit> Units { get; }
    public BattleFieldSnapshot Snapshot { get; }
    public BattleAITuningSO AiTuning { get; }
    public float TickDeltaTime { get; }

    public BattlePlanningContext(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleFieldSnapshot snapshot,
        BattleAITuningSO aiTuning,
        float tickDeltaTime
    )
    {
        Units = units;
        Snapshot = snapshot;
        AiTuning = aiTuning;
        TickDeltaTime = tickDeltaTime;
    }
}
