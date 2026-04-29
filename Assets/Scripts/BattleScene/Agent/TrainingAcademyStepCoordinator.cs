using System.Collections.Generic;
using Unity.MLAgents;

public sealed class TrainingAcademyStepCoordinator
{
    private static readonly TrainingAcademyStepCoordinator Shared = new TrainingAcademyStepCoordinator();

    private readonly List<ITrainingEnvironment> _environments = new List<ITrainingEnvironment>();
    private ITrainingEnvironment _driver;
    private bool _academySteppingWasAutomatic;
    private int _environmentStepCount;

    private TrainingAcademyStepCoordinator() { }

    public static TrainingAcademyStepCoordinator Instance => Shared;

    public bool HasDriver => _driver != null;

    public int EnvironmentStepCount => _environmentStepCount;

    public void Register(ITrainingEnvironment environment)
    {
        if (environment != null && !_environments.Contains(environment))
        {
            _environments.Add(environment);
        }
    }

    public void Unregister(ITrainingEnvironment environment)
    {
        _environments.Remove(environment);
        ReleaseDriver(environment);
    }

    public bool ClaimDriver(ITrainingEnvironment environment)
    {
        if (environment == null)
        {
            return false;
        }

        if (_driver != null && _driver != environment)
        {
            return false;
        }

        if (_driver == null)
        {
            _driver = environment;
            _academySteppingWasAutomatic = Academy.Instance.AutomaticSteppingEnabled;
            Academy.Instance.AutomaticSteppingEnabled = false;
        }

        return true;
    }

    public void ReleaseDriver(ITrainingEnvironment environment)
    {
        if (_driver != environment)
        {
            return;
        }

        Academy.Instance.AutomaticSteppingEnabled = _academySteppingWasAutomatic;
        _driver = null;
    }

    public bool TickIfDriver(ITrainingEnvironment environment)
    {
        if (_driver == null)
        {
            ClaimDriver(environment);
        }

        if (_driver != environment)
        {
            return false;
        }

        Tick();
        return true;
    }

    public void Tick()
    {
        Academy.Instance.EnvironmentStep();
        _environmentStepCount++;

        for (int i = 0; i < _environments.Count; i++)
        {
            ITrainingEnvironment environment = _environments[i];
            if (environment == null || !environment.IsTrainingEnvironmentActive || environment.IsEpisodeEnding)
            {
                continue;
            }

            environment.StepTrainingEnvironment();
        }

        for (int i = 0; i < _environments.Count; i++)
        {
            ITrainingEnvironment environment = _environments[i];
            if (environment == null || !environment.IsTrainingEnvironmentActive)
            {
                continue;
            }

            environment.TryResetFinishedOrTimedOutEpisode();
        }
    }
}
