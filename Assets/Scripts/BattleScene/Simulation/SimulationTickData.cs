public sealed class SimulationTickData
{
    public int Tick { get; }
    public int[] UnitNumbers { get; }
    public BattleParameterSet[] RawParameters { get; }
    public BattleParameterSet[] ModifiedParameters { get; }
    public bool[] ModifierOverflowFlags { get; }
    public BattleActionType[] Decisions { get; }
    public BattleCombatResult[] CombatResults { get; }

    public SimulationTickData(
        int tick,
        int[] unitNumbers,
        BattleParameterSet[] rawParameters,
        BattleParameterSet[] modifiedParameters,
        bool[] modifierOverflowFlags,
        BattleActionType[] decisions,
        BattleCombatResult[] combatResults)
    {
        Tick = tick;
        UnitNumbers = unitNumbers;
        RawParameters = rawParameters;
        ModifiedParameters = modifiedParameters;
        ModifierOverflowFlags = modifierOverflowFlags;
        Decisions = decisions;
        CombatResults = combatResults;
    }
}
