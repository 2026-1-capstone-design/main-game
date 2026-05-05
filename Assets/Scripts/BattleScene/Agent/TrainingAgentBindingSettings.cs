public readonly struct TrainingAgentBindingSettings
{
    public readonly BattleMlControlledSide ControlledSide;
    public readonly bool UseCurriculumOpponentMode;
    public readonly string OpponentModeEnvironmentParameter;
    public readonly GladiatorAgent[] AllyAgents;
    public readonly GladiatorAgent[] EnemyAgents;
    public readonly bool UsePocaGroupRewards;
    public readonly float GroupWinReward;
    public readonly float GroupLossReward;
    public readonly float GroupInterruptedReward;
    public readonly float WinSpeedBonus;
    public readonly float WinHpBonus;
    public readonly float TimeoutMultiplier;
    public readonly float TimeoutHpRatioMultiplierMax;

    public TrainingAgentBindingSettings(
        BattleMlControlledSide controlledSide,
        bool useCurriculumOpponentMode,
        string opponentModeEnvironmentParameter,
        GladiatorAgent[] allyAgents,
        GladiatorAgent[] enemyAgents,
        bool usePocaGroupRewards,
        float groupWinReward,
        float groupLossReward,
        float groupInterruptedReward,
        float winSpeedBonus,
        float winHpBonus,
        float timeoutMultiplier,
        float timeoutHpRatioMultiplierMax
    )
    {
        ControlledSide = controlledSide;
        UseCurriculumOpponentMode = useCurriculumOpponentMode;
        OpponentModeEnvironmentParameter = opponentModeEnvironmentParameter;
        AllyAgents = allyAgents;
        EnemyAgents = enemyAgents;
        UsePocaGroupRewards = usePocaGroupRewards;
        GroupWinReward = groupWinReward;
        GroupLossReward = groupLossReward;
        GroupInterruptedReward = groupInterruptedReward;
        WinSpeedBonus = winSpeedBonus;
        WinHpBonus = winHpBonus;
        TimeoutMultiplier = timeoutMultiplier;
        TimeoutHpRatioMultiplierMax = timeoutHpRatioMultiplierMax;
    }
}
