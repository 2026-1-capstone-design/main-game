using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public sealed class TrainingAgentBinder
{
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
        List<BattleRuntimeUnit> playerUnits = BattleMlUnitSelection.GetSortedUnitsForTeam(
            _flowManager.RuntimeUnits,
            payload.GetPlayerTeam().TeamId,
            projection
        );
        List<BattleRuntimeUnit> hostileUnits = BattleMlUnitSelection.GetSortedUnitsForTeam(
            _flowManager.RuntimeUnits,
            payload.GetHostileTeam().TeamId,
            projection
        );
        BattleMlControlledSide resolvedControlledSide = ResolveControlledSide(settings);
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

    public void EndTrainingGroups(TrainingEpisodeEndReason reason, BattleTeamId? winnerTeamId, bool isTimeout)
    {
        if (!_settings.UsePocaGroupRewards || _allyGroup == null || _enemyGroup == null)
        {
            ForEachControlledAgent(agent => agent.GiveEndReward(winnerTeamId, isTimeout));
            ForEachControlledAgent(agent => agent.EndEpisode());
            return;
        }

        if (reason == TrainingEpisodeEndReason.BattleFinished && winnerTeamId.HasValue)
        {
            bool allyWon = winnerTeamId.Value == BattleTeamIds.Player;
            _allyGroup.AddGroupReward(allyWon ? _settings.GroupWinReward : _settings.GroupLossReward);
            _enemyGroup.AddGroupReward(allyWon ? _settings.GroupLossReward : _settings.GroupWinReward);
            _allyGroup.EndGroupEpisode();
            _enemyGroup.EndGroupEpisode();
            return;
        }

        float interruptionReward =
            reason == TrainingEpisodeEndReason.Timeout
                ? _settings.GroupTimeoutReward
                : _settings.GroupInterruptedReward;
        _allyGroup.AddGroupReward(interruptionReward);
        _enemyGroup.AddGroupReward(interruptionReward);
        _allyGroup.GroupEpisodeInterrupted();
        _enemyGroup.GroupEpisodeInterrupted();
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

            if (!agent.gameObject.activeSelf)
            {
                agent.gameObject.SetActive(true);
            }

            agent.Initialize(unit, _flowManager, _trainingBootstrapper);
            group.RegisterAgent(agent);
        }
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

    private static BattleMlControlledSide ResolveControlledSide(TrainingAgentBindingSettings settings)
    {
        if (!settings.UseCurriculumOpponentMode || string.IsNullOrWhiteSpace(settings.OpponentModeEnvironmentParameter))
        {
            return settings.ControlledSide;
        }

        float opponentMode = Academy.Instance.EnvironmentParameters.GetWithDefault(
            settings.OpponentModeEnvironmentParameter,
            settings.ControlledSide == BattleMlControlledSide.BothTeams ? 1f : 0f
        );

        return opponentMode >= 0.5f ? BattleMlControlledSide.BothTeams : BattleMlControlledSide.PlayerTeam;
    }

    private static bool ControlsPlayerTeam(BattleMlControlledSide side) =>
        side == BattleMlControlledSide.PlayerTeam || side == BattleMlControlledSide.BothTeams;

    private static bool ControlsHostileTeam(BattleMlControlledSide side) =>
        side == BattleMlControlledSide.HostileTeam || side == BattleMlControlledSide.BothTeams;

    private static int GetAgentCount(GladiatorAgent[] agents) => agents != null ? agents.Length : 0;

    private static bool IsActiveControlledAgent(GladiatorAgent agent) =>
        agent != null && agent.gameObject.activeInHierarchy && agent.HasControlledUnit;
}
