public interface IGladiatorCurriculumSource
{
    GladiatorAnchorCurriculum CurrentAnchorCurriculum { get; }
    GladiatorRoleCurriculum CurrentRoleCurriculum { get; }
    float BattleTimeoutRemainingRatio { get; }
    void RequestEpisodeReset();
}
