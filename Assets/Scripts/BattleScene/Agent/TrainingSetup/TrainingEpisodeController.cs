using System.Collections.Generic;
using UnityEngine;

public enum TrainingEpisodeEndReason
{
    BattleFinished,
    Timeout,
    Requested,
}

public sealed class TrainingEpisodeController
{
    private readonly BattleSceneFlowManager _flowManager;
    private readonly BattleSimulationManager _simulationManager;
    private readonly TrainingBattlePayloadFactory _payloadFactory;
    private readonly TrainingSpawnPlacementSampler _placementSampler;
    private readonly TrainingAgentBinder _agentBinder;
    private readonly System.Func<TrainingBattlePayloadSettings> _payloadSettingsProvider;
    private readonly System.Func<TrainingAgentBindingSettings> _bindingSettingsProvider;
    private readonly System.Action _refreshAnimations;
    private readonly Object _logContext;

    private bool _episodeEnding;
    private bool _episodeResetRequested;
    private BattleOutcome? _lastOutcome;
    private int _battleTimeoutTicks;

    public TrainingEpisodeController(
        BattleSceneFlowManager flowManager,
        BattleSimulationManager simulationManager,
        TrainingBattlePayloadFactory payloadFactory,
        TrainingSpawnPlacementSampler placementSampler,
        TrainingAgentBinder agentBinder,
        System.Func<TrainingBattlePayloadSettings> payloadSettingsProvider,
        System.Func<TrainingAgentBindingSettings> bindingSettingsProvider,
        System.Action refreshAnimations,
        Object logContext
    )
    {
        _flowManager = flowManager;
        _simulationManager = simulationManager;
        _payloadFactory = payloadFactory;
        _placementSampler = placementSampler;
        _agentBinder = agentBinder;
        _payloadSettingsProvider = payloadSettingsProvider;
        _bindingSettingsProvider = bindingSettingsProvider;
        _refreshAnimations = refreshAnimations;
        _logContext = logContext;
    }

    public bool IsEpisodeEnding => _episodeEnding;

    public void StartEpisode()
    {
        BootstrapEpisode();
    }

    public void RequestReset()
    {
        if (!_episodeEnding)
        {
            _episodeResetRequested = true;
        }
    }

    public void TickBattle(int battleTicksPerEnvironmentStep)
    {
        if (_simulationManager == null)
        {
            return;
        }

        _simulationManager.StepSimulationTicks(battleTicksPerEnvironmentStep);
    }

    public void TryResetIfFinishedOrTimedOut(int battleTimeoutTicks)
    {
        if (_episodeEnding || _simulationManager == null)
            return;

        _battleTimeoutTicks = battleTimeoutTicks;

        if (_episodeResetRequested)
        {
            ResetEpisode(TrainingEpisodeEndReason.Requested);
            return;
        }

        if (_simulationManager.IsBattleFinished)
        {
            ResetEpisode(TrainingEpisodeEndReason.BattleFinished);
            return;
        }

        if (_simulationManager.BattleTickCount >= battleTimeoutTicks)
        {
            ResetEpisode(TrainingEpisodeEndReason.Timeout);
        }
    }

    public void HandleBattleFinished(BattleOutcome outcome)
    {
        _lastOutcome = outcome;
    }

    private void ResetEpisode(TrainingEpisodeEndReason reason)
    {
        _episodeEnding = true;
        _episodeResetRequested = false;
        bool isTimeout = reason == TrainingEpisodeEndReason.Timeout;
        BattleTeamId? winnerTeamId =
            reason == TrainingEpisodeEndReason.BattleFinished ? _lastOutcome?.WinnerTeamId : null;
        float timeRemainingRatio = ComputeTimeRemainingRatio();
        float winnerHpRatio = winnerTeamId.HasValue ? ComputeTeamHpRatio(winnerTeamId.Value) : 0f;
        float allyHpRatio = ComputeTeamHpRatio(BattleTeamIds.Player);
        float enemyHpRatio = ComputeTeamHpRatio(BattleTeamIds.Enemy);
        _agentBinder.EndTrainingGroups(
            reason,
            winnerTeamId,
            isTimeout,
            timeRemainingRatio,
            winnerHpRatio,
            allyHpRatio,
            enemyHpRatio
        );

        if (!BootstrapEpisode())
        {
            _episodeEnding = false;
            return;
        }

        _lastOutcome = null;
        _episodeEnding = false;
    }

    private float ComputeTimeRemainingRatio()
    {
        if (_simulationManager == null || _battleTimeoutTicks <= 0)
            return 0f;
        return UnityEngine.Mathf.Clamp01(1f - _simulationManager.BattleTickCount / (float)_battleTimeoutTicks);
    }

    private float ComputeTeamHpRatio(BattleTeamId teamId)
    {
        if (_flowManager == null)
            return 0f;

        float totalMax = 0f;
        float totalCurrent = 0f;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit == null || unit.TeamId != teamId)
                continue;
            totalMax += unit.MaxHealth;
            totalCurrent += unit.CurrentHealth;
        }

        return totalMax > 0f ? UnityEngine.Mathf.Clamp01(totalCurrent / totalMax) : 0f;
    }

    private bool BootstrapEpisode()
    {
        BattleStartPayload payload = _payloadFactory.Create(_payloadSettingsProvider());
        if (payload == null)
        {
            return false;
        }

        IReadOnlyDictionary<BattleTeamId, Vector3[]> spawnPositionsByTeam = _placementSampler.GenerateRandomPlacements(
            payload,
            _flowManager != null ? _flowManager.battlefieldCollider : null,
            _simulationManager != null ? _simulationManager.UnitBodyRadius : 0.5f
        );
        bool resetOk = _flowManager != null && _flowManager.ResetAndBootstrap(payload, spawnPositionsByTeam);
        if (!resetOk)
        {
            Debug.LogError("[TrainingEpisodeController] Could not reset battlefield.", _logContext);
            return false;
        }

        _refreshAnimations?.Invoke();
        _agentBinder.Bind(payload, _bindingSettingsProvider());
        return true;
    }
}
