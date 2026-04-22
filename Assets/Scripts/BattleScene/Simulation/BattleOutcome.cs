using System.Collections.Generic;

public enum BattleTeam
{
    None = 0,
    Ally = 1,
    Enemy = 2,
}

public readonly struct BattleOutcome
{
    public BattleTeam Winner { get; }
    public int EndTick { get; }
    public IReadOnlyList<BattleRuntimeUnit> Survivors { get; }
    public BattleResolution Resolution { get; }

    public BattleOutcome(
        BattleTeam winner,
        int endTick,
        IReadOnlyList<BattleRuntimeUnit> survivors,
        BattleResolution resolution
    )
    {
        Winner = winner;
        EndTick = endTick;
        Survivors = survivors;
        Resolution = resolution;
    }
}
