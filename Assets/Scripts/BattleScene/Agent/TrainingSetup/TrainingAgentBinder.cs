using System.Collections.Generic;
using Unity.MLAgents;
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
    private readonly BattleSceneFlowManager _flowManager;
    private readonly IGladiatorCurriculumSource _curriculumSource;
    private readonly Object _logContext;

    private SimpleMultiAgentGroup _allyGroup;
    private SimpleMultiAgentGroup _enemyGroup;
    private TrainingAgentBindingSettings _settings;

    public TrainingAgentBinder(
        BattleSceneFlowManager flowManager,
        IGladiatorCurriculumSource curriculumSource,
        Object logContext
    )
    {
        _flowManager = flowManager;
        _curriculumSource = curriculumSource;
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
        if (reason == TrainingEpisodeEndReason.BattleFinished)
        {
            ForEachControlledAgent(agent => agent.RewardTerminalSurvivalIfAlive());
        }

        ForEachControlledAgent(agent => agent.FlushEpisodeMetrics());
        RecordEpisodeOutcome(reason, winnerTeamId);

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
                NormalizeTeamOutcomeReward(
                    (allyWon ? _settings.GroupWinReward : _settings.GroupLossReward) * combinedMultiplier
                )
            );
            _enemyGroup.AddGroupReward(
                NormalizeTeamOutcomeReward(
                    (allyWon ? _settings.GroupLossReward : _settings.GroupWinReward) * combinedMultiplier
                )
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
            reason == TrainingEpisodeEndReason.Timeout
                ? NormalizeTeamOutcomeReward(timeoutReward)
                : NormalizeTeamOutcomeReward(_settings.GroupInterruptedReward);
        _allyGroup.AddGroupReward(interruptionReward);
        _enemyGroup.AddGroupReward(interruptionReward);
        _allyGroup.GroupEpisodeInterrupted();
        _enemyGroup.GroupEpisodeInterrupted();
    }

    private void RecordEpisodeOutcome(TrainingEpisodeEndReason reason, BattleTeamId? winnerTeamId)
    {
        var recorder = Academy.Instance.StatsRecorder;
        bool battleFinished = reason == TrainingEpisodeEndReason.BattleFinished && winnerTeamId.HasValue;
        // 승패가 정상적으로 확정된 전투 종료인지 여부를 기록한다. timeout이나 강제 중단은 0이다.
        recorder.Add("Combat/BattleFinished", battleFinished ? 1f : 0f, StatAggregationMethod.Average);
        // 경기 종료 시점에 전장 전체에 남아 있는 체력 총합이, 참가 유닛 전체 최대 체력 총합 대비 어느 정도인지 나타낸다.
        recorder.Add(
            "Combat/FinalBattleRemainingHealthRatio",
            ComputeFinalBattleRemainingHealthRatio(),
            StatAggregationMethod.Average
        );
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
                agent.Initialize(null, _flowManager, _curriculumSource);
                agent.gameObject.SetActive(false);
                continue;
            }

            if (agent.gameObject.activeSelf)
            {
                agent.gameObject.SetActive(false);
            }
            if (
                !GladiatorAgentContract.TryApplyRuntimeOverrides(
                    agent,
                    unit,
                    i,
                    false,
                    _logContext,
                    "[TrainingAgentBinder]"
                )
            )
            {
                agent.gameObject.SetActive(false);
                continue;
            }

            agent.gameObject.SetActive(true);
            agent.Initialize(unit, _flowManager, _curriculumSource);
            group.RegisterAgent(agent);
        }
    }

    private static void ApplyControlMode(IReadOnlyList<BattleRuntimeUnit> units, bool usesAgentPolicyControl)
    {
        if (units == null)
        {
            return;
        }

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            BattleSceneFlowManager flowManager =
                unit != null ? unit.GetComponentInParent<BattleSceneFlowManager>() : null;
            flowManager?.BattleSimulationManager?.SetUnitControlMode(
                unit != null ? unit.State : null,
                usesAgentPolicyControl ? BattleUnitControlMode.AgentPolicy : BattleUnitControlMode.BuiltInAI
            );
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

    private float NormalizeTeamOutcomeReward(float reward)
    {
        float theoreticalMinReward =
            _settings.GroupLossReward
            * _settings.WinSpeedBonus
            * _settings.WinHpBonus
            * _settings.TimeoutMultiplier
            * _settings.TimeoutHpRatioMultiplierMax;
        float theoreticalMaxReward = _settings.GroupWinReward * _settings.WinSpeedBonus * _settings.WinHpBonus;

        if (Mathf.Approximately(theoreticalMinReward, theoreticalMaxReward))
        {
            return Mathf.Clamp(reward, -5f, 5f);
        }

        float normalizedReward = Mathf.InverseLerp(theoreticalMinReward, theoreticalMaxReward, reward);
        return Mathf.Lerp(-5f, 5f, normalizedReward);
    }

    private float ComputeFinalBattleRemainingHealthRatio()
    {
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        if (runtimeUnits == null || runtimeUnits.Count == 0)
        {
            return 0f;
        }

        float totalRemainingHealth = 0f;
        float totalMaxHealth = 0f;
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            BattleUnitCombatState state = unit != null ? unit.State : null;
            if (state == null || state.MaxHealth <= 0f)
            {
                continue;
            }

            totalMaxHealth += state.MaxHealth;
            totalRemainingHealth += Mathf.Max(0f, state.CurrentHealth);
        }

        return totalMaxHealth > 0f ? totalRemainingHealth / totalMaxHealth : 0f;
    }

    private static bool IsActiveControlledAgent(GladiatorAgent agent) =>
        agent != null && agent.gameObject.activeInHierarchy && agent.HasControlledUnit;
}
