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

    [Header("Agent Control")]
    [SerializeField]
    private BattleMlControlledSide controlledSide = BattleMlControlledSide.BothTeams;

    [SerializeField]
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
    private float groupWinReward = 1f;

    [SerializeField]
    private float groupLossReward = -1f;

    [SerializeField]
    private float groupTimeoutReward = -0.25f;

    [SerializeField]
    private float groupInterruptedReward = -0.1f;

    // true이면 ML-Agents 자동 FixedUpdate stepper를 끄고 이 bootstrapper가 직접 Academy step을 진행한다.
    [SerializeField]
    private bool manuallyStepAcademy = true;

    private const int BattleTimeoutTicks = 1 * 60 * 60;

    private TrainingAcademyStepCoordinator _academyStepCoordinator;
    private TrainingEpisodeController _episodeController;
    private TrainingAgentBinder _agentBinder;

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
        defaultTeamSize = Mathf.Clamp(defaultTeamSize, 1, BattleTeamConstants.MaxUnitsPerTeam);
        defaultUnitLevel = Mathf.Max(1, defaultUnitLevel);
        defaultStatMultiplier = Mathf.Max(0f, defaultStatMultiplier);
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
            defaultStatMultiplier
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
            groupWinReward,
            groupLossReward,
            groupTimeoutReward,
            groupInterruptedReward
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
}
