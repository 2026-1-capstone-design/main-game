using BattleTest;
using UnityEngine;

public class TrainingBootstrapper : MonoBehaviour, ITrainingEnvironment
{
    public BattleSceneFlowManager battleSceneFlowManager;
    public BattleSimulationManager battleSimulationManager;

    [SerializeField]
    private BattleTestPresetSO preset;

    [Header("Curriculum")]
    [SerializeField]
    private bool useCurriculumTeamSize = true;

    [SerializeField]
    private string teamSizeEnvironmentParameter = "team_size";

    [SerializeField]
    private int defaultTeamSize = 1;

    [SerializeField]
    private GladiatorClassSO[] randomClassPool;

    [SerializeField]
    private WeaponSO[] randomWeaponPool;

    [SerializeField]
    private int defaultUnitLevel = 1;

    [SerializeField]
    private float defaultStatMultiplier = 1f;

    [Header("Training Stat Advantage")]
    [SerializeField]
    private string allyStatMultiplierEnvironmentParameter = "ally_stat_multiplier";

    [SerializeField]
    private string enemyStatMultiplierEnvironmentParameter = "enemy_stat_multiplier";

    [SerializeField]
    private float defaultAllyStatMultiplier = 1.15f;

    [SerializeField]
    private float defaultEnemyStatMultiplier = 1f;

    [Header("Editor Playback")]
    [SerializeField]
    private float timeScale = 1f;

    [SerializeField]
    private bool logTrainingProgress = true;

    [SerializeField]
    private int logProgressInterval = 100;

    [Header("Agent Control")]
    [SerializeField]
    private BattleMlControlledSide controlledSide = BattleMlControlledSide.BothTeams;

    [SerializeField]
    [Tooltip("config YAML 파일에 {opponentModeEnvironmentParameter}가 설정되어 있으면 controlledSide를 덮어씌운다.")]
    private bool useCurriculumOpponentMode = true;

    [SerializeField]
    private string opponentModeEnvironmentParameter = "opponent_mode";

    [SerializeField]
    private GladiatorAgent[] allyAgents;

    [SerializeField]
    private GladiatorAgent[] enemyAgents;

    [SerializeField]
    private int battleTicksPerEnvironmentStep = 1;

    [Header("POCA Group Rewards")]
    [SerializeField]
    private bool usePocaGroupRewards = true;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    // true이면 ML-Agents 자동 FixedUpdate stepper를 끄고 이 bootstrapper가 직접 Academy step을 진행한다.
    [SerializeField]
    private bool manuallyStepAcademy = true;

    private const int BattleTimeoutTicks = 1 * 60 * 60;

    private TrainingAcademyStepCoordinator _academyStepCoordinator;
    private TrainingEpisodeController _episodeController;
    private TrainingAgentBinder _agentBinder;
    private bool _timeScaleApplied;

    private static int _activeTimeScaleUsers;
    private static float _previousTimeScale = 1f;

    public int BattleTimeoutTickLimit => BattleTimeoutTicks;

    public bool IsTrainingEnvironmentActive => isActiveAndEnabled;

    public bool IsEpisodeEnding => _episodeController != null && _episodeController.IsEpisodeEnding;

    public float BattleTimeoutRemainingRatio
    {
        get
        {
            if (battleSimulationManager == null)
            {
                return 1f;
            }

            return Mathf.Clamp01(
                (BattleTimeoutTicks - battleSimulationManager.BattleTickCount) / (float)BattleTimeoutTicks
            );
        }
    }

    private void OnValidate()
    {
        battleTicksPerEnvironmentStep = Mathf.Max(1, battleTicksPerEnvironmentStep);
        logProgressInterval = Mathf.Max(1, logProgressInterval);
        defaultTeamSize = Mathf.Clamp(defaultTeamSize, 1, BattleTeamConstants.MaxUnitsPerTeam);
        defaultUnitLevel = Mathf.Max(1, defaultUnitLevel);
        defaultStatMultiplier = Mathf.Max(0f, defaultStatMultiplier);
        defaultAllyStatMultiplier = Mathf.Max(0f, defaultAllyStatMultiplier);
        defaultEnemyStatMultiplier = Mathf.Max(0f, defaultEnemyStatMultiplier);
    }

    private void OnEnable()
    {
        _academyStepCoordinator = TrainingAcademyStepCoordinator.Instance;
        _academyStepCoordinator.Register(this);

        if (manuallyStepAcademy && !_academyStepCoordinator.HasDriver)
        {
            _academyStepCoordinator.ClaimDriver(this);
        }
    }

    private void OnDisable()
    {
        ReleaseTimeScale();

        if (battleSimulationManager != null && _episodeController != null)
        {
            battleSimulationManager.OnBattleFinished -= _episodeController.HandleBattleFinished;
        }

        if (battleSceneFlowManager != null)
        {
            battleSceneFlowManager.OnUnitsSpawned -= RefreshAllUnitAnimations;
        }

        _agentBinder?.Dispose();
        _academyStepCoordinator?.Unregister(this);
    }

    private void Start()
    {
        ApplyTimeScale();
        BuildServices();

        if (battleSimulationManager != null)
        {
            battleSimulationManager.SetAutoStepInUpdate(false);
            battleSimulationManager.OnBattleFinished -= _episodeController.HandleBattleFinished;
            battleSimulationManager.OnBattleFinished += _episodeController.HandleBattleFinished;
        }

        if (battleSceneFlowManager != null)
        {
            battleSceneFlowManager.OnUnitsSpawned -= RefreshAllUnitAnimations;
            battleSceneFlowManager.OnUnitsSpawned += RefreshAllUnitAnimations;
        }

        _episodeController.StartEpisode();
        LogTrainingProgress("Episode started");
    }

    private void FixedUpdate()
    {
        if (_episodeController == null || _episodeController.IsEpisodeEnding)
        {
            return;
        }

        if (manuallyStepAcademy)
        {
            _academyStepCoordinator.TickIfDriver(this);
            return;
        }

        StepTrainingEnvironment();
        TryResetFinishedOrTimedOutEpisode();
    }

    public void StepTrainingEnvironment()
    {
        _episodeController?.TickBattle(battleTicksPerEnvironmentStep);
        LogTrainingProgress("Progress");
    }

    public void TryResetFinishedOrTimedOutEpisode()
    {
        _episodeController?.TryResetIfFinishedOrTimedOut(BattleTimeoutTicks);
    }

    public void RequestEpisodeReset()
    {
        _episodeController?.RequestReset();
    }

    private void BuildServices()
    {
        TrainingBattlePayloadFactory payloadFactory = new TrainingBattlePayloadFactory(this);
        TrainingSpawnPlacementSampler placementSampler = new TrainingSpawnPlacementSampler();
        _agentBinder = new TrainingAgentBinder(battleSceneFlowManager, this, this);
        _episodeController = new TrainingEpisodeController(
            battleSceneFlowManager,
            battleSimulationManager,
            payloadFactory,
            placementSampler,
            _agentBinder,
            CreatePayloadSettings,
            CreateBindingSettings,
            RefreshAllUnitAnimations,
            this
        );
    }

    private void ApplyTimeScale()
    {
        if (_timeScaleApplied)
        {
            return;
        }

        if (_activeTimeScaleUsers == 0)
        {
            _previousTimeScale = Time.timeScale;
        }

        _activeTimeScaleUsers++;
        Time.timeScale = Mathf.Max(0f, timeScale);
        _timeScaleApplied = true;
    }

    private void ReleaseTimeScale()
    {
        if (!_timeScaleApplied)
        {
            return;
        }

        _timeScaleApplied = false;
        _activeTimeScaleUsers = Mathf.Max(0, _activeTimeScaleUsers - 1);
        if (_activeTimeScaleUsers == 0)
        {
            Time.timeScale = _previousTimeScale;
        }
    }

    private TrainingBattlePayloadSettings CreatePayloadSettings()
    {
        return new TrainingBattlePayloadSettings(
            preset,
            useCurriculumTeamSize,
            teamSizeEnvironmentParameter,
            defaultTeamSize,
            randomClassPool,
            randomWeaponPool,
            defaultUnitLevel,
            defaultStatMultiplier,
            allyStatMultiplierEnvironmentParameter,
            enemyStatMultiplierEnvironmentParameter,
            defaultAllyStatMultiplier,
            defaultEnemyStatMultiplier
        );
    }

    private TrainingAgentBindingSettings CreateBindingSettings()
    {
        return new TrainingAgentBindingSettings(
            controlledSide,
            useCurriculumOpponentMode,
            opponentModeEnvironmentParameter,
            allyAgents,
            enemyAgents,
            usePocaGroupRewards,
            rewardConfig != null ? rewardConfig.groupWin : 0f,
            rewardConfig != null ? rewardConfig.groupLoss : 0f,
            rewardConfig != null ? rewardConfig.groupInterrupted : 0f,
            rewardConfig != null ? rewardConfig.winSpeedBonus : 0.5f,
            rewardConfig != null ? rewardConfig.winHpBonus : 0.5f,
            rewardConfig != null ? rewardConfig.timeoutPenaltyScale : 1.2f
        );
    }

    private void RefreshAllUnitAnimations()
    {
        if (AnimationManager.Instance == null)
        {
            Debug.LogError(
                "[TrainingBootstrapper] RefreshAllUnitAnimations failed. AnimationManager instance not found.",
                this
            );
            return;
        }

        if (battleSceneFlowManager == null)
        {
            return;
        }

        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            if (unit == null || unit.Snapshot == null)
            {
                continue;
            }

            Animator animator = unit.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                continue;
            }

            AnimatorOverrideController controller = AnimationManager.Instance.GetControllerByWeaponType(
                unit.Snapshot.WeaponType
            );
            if (controller != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
            }
        }
    }

    private void LogTrainingProgress(string label)
    {
        if (!logTrainingProgress || battleSimulationManager == null)
        {
            return;
        }

        int academyStepCount = _academyStepCoordinator != null ? _academyStepCoordinator.EnvironmentStepCount : 0;
        int battleTickCount = battleSimulationManager.BattleTickCount;
        if (
            !string.Equals(label, "Episode started", System.StringComparison.Ordinal)
            && academyStepCount % logProgressInterval != 0
        )
        {
            return;
        }

        Debug.Log(
            $"[TrainingBootstrapper] {label}: academyStep={academyStepCount}, battleTick={battleTickCount}, timeScale={Time.timeScale}, stepTicks={battleTicksPerEnvironmentStep}",
            this
        );
    }
}
