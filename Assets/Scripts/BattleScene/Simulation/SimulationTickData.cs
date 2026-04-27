public sealed class SimulationTickData
{
    public int Tick { get; private set; }
    public int UnitCount { get; private set; }
    public int CombatResultCount { get; private set; }
    public int[] UnitNumbers { get; }
    public BattleParameterSet[] RawParameters { get; }
    public BattleParameterSet[] ModifiedParameters { get; }
    public bool[] ModifierOverflowFlags { get; }
    public BattleActionType[] Decisions { get; }
    public BattleCombatResult[] CombatResults { get; private set; }

    public SimulationTickData(
        int[] unitNumbers,
        BattleParameterSet[] rawParameters,
        BattleParameterSet[] modifiedParameters,
        bool[] modifierOverflowFlags,
        BattleActionType[] decisions,
        BattleCombatResult[] combatResults
    )
    {
        UnitNumbers = unitNumbers;
        RawParameters = rawParameters;
        ModifiedParameters = modifiedParameters;
        ModifierOverflowFlags = modifierOverflowFlags;
        Decisions = decisions;
        CombatResults = combatResults;
    }

    public void Update(int tick, int unitCount, int combatResultCount)
    {
        Tick = tick;
        UnitCount = unitCount;
        CombatResultCount = combatResultCount;
    }

    public void UpdateCombatResultsBuffer(BattleCombatResult[] combatResults)
    {
        CombatResults = combatResults;
    }
}
