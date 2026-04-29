public interface ITrainingEnvironment
{
    bool IsTrainingEnvironmentActive { get; }
    bool IsEpisodeEnding { get; }

    void StepTrainingEnvironment();
    void TryResetFinishedOrTimedOutEpisode();
}
