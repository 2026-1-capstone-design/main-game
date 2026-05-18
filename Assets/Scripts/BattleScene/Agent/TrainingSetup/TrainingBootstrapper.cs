using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Serialization;

public class TrainingBootstrapper : MonoBehaviour, ITrainingEnvironment, IGladiatorCurriculumSource
{
    public BattleSceneFlowManager battleSceneFlowManager;
    public BattleSimulationManager battleSimulationManager;

    [Header("Curriculum")]
    [SerializeField]
    private bool useCurriculumTeamSize = true;

    [SerializeField]
    private string teamSizeEnvironmentParameter = "team_size";

    [SerializeField]
    private int defaultTeamSize = 1;

    [SerializeField]
    private TrainingGladiatorPreset[] randomPresetPool;

    [Header("Training Stat Advantage")]
    [SerializeField]
    private string allyStatMultiplierEnvironmentParameter = "ally_stat_multiplier";

    [SerializeField]
    private string enemyStatMultiplierEnvironmentParameter = "enemy_stat_multiplier";

    [SerializeField]
    private float defaultAllyStatMultiplier = 1.15f;

    [SerializeField]
    private float defaultEnemyStatMultiplier = 1f;

    [Header("Training Playback")]
    [SerializeField]
    [FormerlySerializedAs("timeScale")]
    [Tooltip("Training 전용 시간 배속. Battle tick 진행과 Animator 재생 속도를 같은 값으로 맞춘다.")]
    private float editorTimeScale = 1f;

    [SerializeField]
    [Tooltip("빌드/CLI 학습에서 사용할 Training 전용 시간 배속.")]
    private float standaloneTimeScale = 1f;

    [SerializeField]
    private bool logTrainingProgress = true;

    [SerializeField]
    private int logProgressInterval = 100;

    [Header("Agent Control")]
    [SerializeField]
    private GladiatorControlledSide controlledSide = GladiatorControlledSide.BothTeams;

    [SerializeField]
    [Tooltip("config YAML 파일에 {opponentModeEnvironmentParameter}가 설정되어 있으면 controlledSide를 덮어씌운다.")]
    private bool useCurriculumOpponentMode = true;

    [SerializeField]
    private string opponentModeEnvironmentParameter = TrainingCurriculumParameterNames.OpponentMode;

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
    private float _trainingStepAccumulator;

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
        defaultAllyStatMultiplier = Mathf.Max(0f, defaultAllyStatMultiplier);
        defaultEnemyStatMultiplier = Mathf.Max(0f, defaultEnemyStatMultiplier);
        editorTimeScale = Mathf.Max(0f, editorTimeScale);
        standaloneTimeScale = Mathf.Max(0f, standaloneTimeScale);
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
        BuildServices();

        if (battleSimulationManager != null)
        {
            battleSimulationManager.SetTrainingOptimizedSimulation(true);
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

        int environmentSteps = ConsumeTrainingEnvironmentSteps();
        if (environmentSteps <= 0)
        {
            return;
        }

        if (manuallyStepAcademy)
        {
            for (int i = 0; i < environmentSteps; i++)
            {
                _academyStepCoordinator.TickIfDriver(this);
            }
            return;
        }

        for (int i = 0; i < environmentSteps; i++)
        {
            StepTrainingEnvironment();
            TryResetFinishedOrTimedOutEpisode();
        }
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

    private int ConsumeTrainingEnvironmentSteps()
    {
        if (battleSimulationManager == null)
        {
            return 0;
        }

        float tickInterval = battleSimulationManager.TickInterval;
        if (tickInterval <= 0f)
        {
            tickInterval = 1f / Mathf.Max(1f, battleSimulationManager.simulationTickRate);
        }

        int ticksPerEnvironmentStep = Mathf.Max(1, battleTicksPerEnvironmentStep);
        float environmentStepInterval = tickInterval * ticksPerEnvironmentStep;
        if (environmentStepInterval <= 0f)
        {
            return 0;
        }

        _trainingStepAccumulator += Time.fixedDeltaTime * GetConfiguredTrainingTimeScale();
        int stepCount = Mathf.FloorToInt(_trainingStepAccumulator / environmentStepInterval);
        if (stepCount <= 0)
        {
            return 0;
        }

        _trainingStepAccumulator -= stepCount * environmentStepInterval;
        return stepCount;
    }

    private float GetConfiguredTrainingTimeScale() =>
        Mathf.Max(0f, Application.isEditor ? editorTimeScale : standaloneTimeScale);

    private TrainingBattlePayloadSettings CreatePayloadSettings()
    {
        return new TrainingBattlePayloadSettings(
            useCurriculumTeamSize,
            teamSizeEnvironmentParameter,
            defaultTeamSize,
            randomPresetPool,
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
            rewardConfig != null ? rewardConfig.winSpeedBonus : 1.5f,
            rewardConfig != null ? rewardConfig.winHpBonus : 1.5f,
            rewardConfig != null ? rewardConfig.timeoutMultiplier : 1.2f,
            rewardConfig != null ? rewardConfig.timeoutHpRatioMultiplierMax : 1.5f
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

            unit.SetAnimationSpeed(GetConfiguredTrainingTimeScale());
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
            $"[TrainingBootstrapper] {label}: academyStep={academyStepCount}, battleTick={battleTickCount}, trainingTimeScale={GetConfiguredTrainingTimeScale()}, stepTicks={battleTicksPerEnvironmentStep}",
            this
        );
    }
}
