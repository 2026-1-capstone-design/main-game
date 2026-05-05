using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

public readonly struct TrainingAgentBindingSettings
{
    public readonly GladiatorControlledSide ControlledSide;
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
        GladiatorControlledSide controlledSide,
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

public sealed class TrainingAgentBinder
{
    private static readonly int[] ExpectedDiscreteBranches =
    {
        GladiatorActionSchema.CommandBranchSize,
        GladiatorActionSchema.StanceBranchSize,
        GladiatorActionSchema.PathModeBranchSize,
        GladiatorActionSchema.AnchorKindBranchSize,
        GladiatorActionSchema.AnchorSlotBranchSize,
    };

    private readonly BattleSceneFlowManager _flowManager;
    private readonly TrainingBootstrapper _trainingBootstrapper;
    private readonly Object _logContext;

    private SimpleMultiAgentGroup _allyGroup;
    private SimpleMultiAgentGroup _enemyGroup;
    private TrainingAgentBindingSettings _settings;

    public TrainingAgentBinder(
        BattleSceneFlowManager flowManager,
        TrainingBootstrapper trainingBootstrapper,
        Object logContext
    )
    {
        _flowManager = flowManager;
        _trainingBootstrapper = trainingBootstrapper;
        _logContext = logContext;
    }

    public void Bind(BattleStartPayload payload, TrainingAgentBindingSettings settings)
    {
        _settings = settings;
        if (payload == null)
        {
            Debug.LogError("[TrainingAgentBinder] Bind failed. Battle payload is missing.", _logContext);
            return;
        }

        if (_flowManager == null)
        {
            Debug.LogError("[TrainingAgentBinder] Bind failed. BattleSceneFlowManager is missing.", _logContext);
            return;
        }

        BattleRosterProjection projection = new BattleRosterProjection(payload);
        List<BattleRuntimeUnit> playerUnits = GladiatorUnitSelection.GetSortedUnitsForTeam(
            _flowManager.RuntimeUnits,
            payload.GetPlayerTeam().TeamId,
            projection
        );
        List<BattleRuntimeUnit> hostileUnits = GladiatorUnitSelection.GetSortedUnitsForTeam(
            _flowManager.RuntimeUnits,
            payload.GetHostileTeam().TeamId,
            projection
        );
        GladiatorControlledSide resolvedControlledSide = ResolveControlledSide(settings);
        bool controlsPlayerTeam = ControlsPlayerTeam(resolvedControlledSide);
        bool controlsHostileTeam = ControlsHostileTeam(resolvedControlledSide);

        Debug.Log(
            $"[TrainingAgentBinder] Linking agents: Side={resolvedControlledSide}, "
                + $"{playerUnits.Count} player units / {GetAgentCount(settings.AllyAgents)} ally agents, "
                + $"{hostileUnits.Count} hostile units / {GetAgentCount(settings.EnemyAgents)} enemy agents.",
            _logContext
        );

        ResetTrainingGroups();
        ApplyControlMode(playerUnits, controlsPlayerTeam);
        ApplyControlMode(hostileUnits, controlsHostileTeam);

        BindAgentsToUnits(settings.AllyAgents, playerUnits, _allyGroup, controlsPlayerTeam);
        BindAgentsToUnits(settings.EnemyAgents, hostileUnits, _enemyGroup, controlsHostileTeam);
    }

    public void EndTrainingGroups(
        TrainingEpisodeEndReason reason,
        BattleTeamId? winnerTeamId,
        bool isTimeout,
        float timeRemainingRatio = 0f,
        float winnerHpRatio = 0f,
        float allyHpRatio = 0f,
        float enemyHpRatio = 0f
    )
    {
        ForEachControlledAgent(agent => agent.FlushEpisodeMetrics());
        RecordEpisodeOutcome(reason, winnerTeamId, isTimeout);

        if (!_settings.UsePocaGroupRewards || _allyGroup == null || _enemyGroup == null)
        {
            ForEachControlledAgent(agent => agent.EndEpisode());
            return;
        }

        if (reason == TrainingEpisodeEndReason.BattleFinished && winnerTeamId.HasValue)
        {
            bool allyWon = winnerTeamId.Value == BattleTeamIds.Player;
            float speedMultiplier = 1f + (_settings.WinSpeedBonus - 1f) * timeRemainingRatio;
            float hpMultiplier = 1f + (_settings.WinHpBonus - 1f) * winnerHpRatio;
            float combinedMultiplier = speedMultiplier * hpMultiplier;
            _allyGroup.AddGroupReward(
                (allyWon ? _settings.GroupWinReward : _settings.GroupLossReward) * combinedMultiplier
            );
            _enemyGroup.AddGroupReward(
                (allyWon ? _settings.GroupLossReward : _settings.GroupWinReward) * combinedMultiplier
            );
            _allyGroup.EndGroupEpisode();
            _enemyGroup.EndGroupEpisode();
            return;
        }

        float timeoutReward =
            _settings.GroupLossReward
            * _settings.WinSpeedBonus
            * _settings.WinHpBonus
            * _settings.TimeoutMultiplier
            * ComputeTimeoutHpMultiplier(enemyHpRatio);
        float interruptionReward =
            reason == TrainingEpisodeEndReason.Timeout ? timeoutReward : _settings.GroupInterruptedReward;
        _allyGroup.AddGroupReward(interruptionReward);
        _enemyGroup.AddGroupReward(interruptionReward);
        _allyGroup.GroupEpisodeInterrupted();
        _enemyGroup.GroupEpisodeInterrupted();
    }

    private static void RecordEpisodeOutcome(
        TrainingEpisodeEndReason reason,
        BattleTeamId? winnerTeamId,
        bool isTimeout
    )
    {
        var recorder = Academy.Instance.StatsRecorder;
        bool battleFinished = reason == TrainingEpisodeEndReason.BattleFinished && winnerTeamId.HasValue;
        recorder.Add("Combat/BattleFinished", battleFinished ? 1f : 0f, StatAggregationMethod.Average);
        recorder.Add("Combat/Timeout", isTimeout ? 1f : 0f, StatAggregationMethod.Average);
        if (battleFinished)
        {
            recorder.Add(
                "Combat/AllyWin",
                winnerTeamId.Value == BattleTeamIds.Player ? 1f : 0f,
                StatAggregationMethod.Average
            );
        }
    }

    public void Dispose()
    {
        DisposeTrainingGroups();
    }

    private void ResetTrainingGroups()
    {
        DisposeTrainingGroups();
        _allyGroup = new SimpleMultiAgentGroup();
        _enemyGroup = new SimpleMultiAgentGroup();
    }

    private void DisposeTrainingGroups()
    {
        _allyGroup?.Dispose();
        _enemyGroup?.Dispose();
        _allyGroup = null;
        _enemyGroup = null;
    }

    private void ForEachControlledAgent(System.Action<GladiatorAgent> action)
    {
        if (action == null)
        {
            return;
        }

        if (_settings.AllyAgents != null)
        {
            foreach (GladiatorAgent agent in _settings.AllyAgents)
            {
                if (IsActiveControlledAgent(agent))
                {
                    action(agent);
                }
            }
        }

        if (_settings.EnemyAgents != null)
        {
            foreach (GladiatorAgent agent in _settings.EnemyAgents)
            {
                if (IsActiveControlledAgent(agent))
                {
                    action(agent);
                }
            }
        }
    }

    private void BindAgentsToUnits(
        GladiatorAgent[] agents,
        IReadOnlyList<BattleRuntimeUnit> units,
        SimpleMultiAgentGroup group,
        bool bindTeam
    )
    {
        if (agents == null)
        {
            return;
        }

        for (int i = 0; i < agents.Length; i++)
        {
            GladiatorAgent agent = agents[i];
            if (agent == null)
            {
                continue;
            }

            BattleRuntimeUnit unit = bindTeam && i < units.Count ? units[i] : null;
            if (unit == null)
            {
                agent.Initialize(null, _flowManager, _trainingBootstrapper);
                agent.gameObject.SetActive(false);
                continue;
            }

            if (agent.gameObject.activeSelf)
            {
                agent.gameObject.SetActive(false);
            }
            ConfigureAgentContract(agent, unit, i);
            agent.gameObject.SetActive(true);
            agent.Initialize(unit, _flowManager, _trainingBootstrapper);
            group.RegisterAgent(agent);
        }
    }

    private static void ConfigureAgentContract(GladiatorAgent agent, BattleRuntimeUnit unit, int agentIndex)
    {
        if (agent == null)
        {
            return;
        }

        BehaviorParameters behaviorParameters = agent.GetComponent<BehaviorParameters>();
        DecisionRequester decisionRequester = agent.GetComponent<DecisionRequester>();
        if (behaviorParameters == null || decisionRequester == null)
        {
            return;
        }

        behaviorParameters.BrainParameters.VectorObservationSize = GladiatorObservationSchema.TotalSize;
        behaviorParameters.BrainParameters.ActionSpec = new ActionSpec(
            GladiatorActionSchema.ContinuousSize,
            (int[])ExpectedDiscreteBranches.Clone()
        );
        behaviorParameters.TeamId = unit != null ? unit.TeamId.GetHashCode() : 0;

        decisionRequester.DecisionPeriod = Mathf.Max(1, decisionRequester.DecisionPeriod);
        decisionRequester.DecisionStep = agentIndex % decisionRequester.DecisionPeriod;
    }

    private static void ApplyControlMode(IReadOnlyList<BattleRuntimeUnit> units, bool usesAgentPolicyControl)
    {
        if (units == null)
        {
            return;
        }

        BattleUnitControlMode mode = usesAgentPolicyControl
            ? BattleUnitControlMode.AgentPolicy
            : BattleUnitControlMode.BuiltInAI;
        for (int i = 0; i < units.Count; i++)
        {
            units[i]?.SetControlMode(mode);
        }
    }

    private static GladiatorControlledSide ResolveControlledSide(TrainingAgentBindingSettings settings)
    {
        if (!settings.UseCurriculumOpponentMode || string.IsNullOrWhiteSpace(settings.OpponentModeEnvironmentParameter))
        {
            return settings.ControlledSide;
        }

        float opponentMode = Academy.Instance.EnvironmentParameters.GetWithDefault(
            settings.OpponentModeEnvironmentParameter,
            settings.ControlledSide == GladiatorControlledSide.BothTeams ? 1f : 0f
        );

        return opponentMode >= 0.5f ? GladiatorControlledSide.BothTeams : GladiatorControlledSide.PlayerTeam;
    }

    private static bool ControlsPlayerTeam(GladiatorControlledSide side) =>
        side == GladiatorControlledSide.PlayerTeam || side == GladiatorControlledSide.BothTeams;

    private static bool ControlsHostileTeam(GladiatorControlledSide side) =>
        side == GladiatorControlledSide.HostileTeam || side == GladiatorControlledSide.BothTeams;

    private static int GetAgentCount(GladiatorAgent[] agents) => agents != null ? agents.Length : 0;

    private float ComputeTimeoutHpMultiplier(float enemyHpRatio)
    {
        float t = Mathf.Clamp01(enemyHpRatio);
        return 1f + (_settings.TimeoutHpRatioMultiplierMax - 1f) * t;
    }

    private static bool IsActiveControlledAgent(GladiatorAgent agent) =>
        agent != null && agent.gameObject.activeInHierarchy && agent.HasControlledUnit;
}
