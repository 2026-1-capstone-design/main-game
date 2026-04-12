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
    public readonly float GroupTimeoutReward;
    public readonly float GroupInterruptedReward;

    public TrainingAgentBindingSettings(
        BattleMlControlledSide controlledSide,
        bool useCurriculumOpponentMode,
        string opponentModeEnvironmentParameter,
        GladiatorAgent[] allyAgents,
        GladiatorAgent[] enemyAgents,
        bool usePocaGroupRewards,
        float groupWinReward,
        float groupLossReward,
        float groupTimeoutReward,
        float groupInterruptedReward
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
        GroupTimeoutReward = groupTimeoutReward;
        GroupInterruptedReward = groupInterruptedReward;
    }
}
