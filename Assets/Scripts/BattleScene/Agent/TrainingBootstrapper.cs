using System.Collections.Generic;
using BattleTest;
using Unity.MLAgents;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using UnityEngine;

// GladiatorAgent.Awake/OnEnable보다 먼저 실행되어야 BehaviorType과 Recorder 설정이 적용된다.
[DefaultExecutionOrder(-1000)]
public class TrainingBootstrapper : MonoBehaviour
{
    private enum TrainingEpisodeEndReason
    {
        BattleFinished,
        Timeout,
        Requested,
    }

    // 한 TrainingScene에 여러 전투 환경이 있어도 Academy step은 전역으로 한 번만 진행한다.
    private static readonly List<TrainingBootstrapper> ActiveBootstrappers = new List<TrainingBootstrapper>();

    // 현재 FixedUpdate에서 Academy.EnvironmentStep() 호출을 소유한 bootstrapper.
    private static TrainingBootstrapper _academyStepDriver;

    // 학습 씬이 비활성화될 때 ML-Agents 자동 stepping 설정을 원래 상태로 되돌리기 위한 값.
    private static bool _academySteppingWasAutomatic;

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

    [SerializeField]
    private GladiatorAgent[] allyAgents;

    [SerializeField]
    private GladiatorAgent[] enemyAgents;

    // ML-Agents step과 battle simulation tick을 1:1 동기화.
    // 4로 두면 학습/inference 결정 빈도 차이가 발생해 시각적 진동 유발 (이전 학습에서 검증됨).
    // 1로 두면 learning rate가 충분히 작아야 하지만 자연스러움 보장에 우선.
    [SerializeField]
    private int battleTicksPerEnvironmentStep = 1;

    [Header("BC Demonstration Recording")]
    [Tooltip("체크하면 모든 Agent를 HeuristicOnly + Recorder.Record=true로 강제 설정. 룰베이스 행동을 .demo 파일로 녹화.")]
    [SerializeField]
    private bool recordDemonstrations = false;

    [Tooltip("녹화할 ML-Agents step 수. 0이면 무한 (사용자가 직접 Stop). 권장 50000~200000.")]
    [SerializeField]
    private int demonstrationStepsPerAgent = 100000;

    [Tooltip("녹화 파일 prefix. 16자 이하 alphanumeric/공백/하이픈만 허용 (ML-Agents 제약).")]
    [SerializeField]
    private string demonstrationName = "GladRuleBase";

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
    private bool _episodeEnding;
    private bool _episodeResetRequested;
    private BattleOutcome? _lastOutcome;
    private SimpleMultiAgentGroup _allyGroup;
    private SimpleMultiAgentGroup _enemyGroup;

    private void OnValidate()
    {
        battleTicksPerEnvironmentStep = Mathf.Max(1, battleTicksPerEnvironmentStep);
        defaultTeamSize = Mathf.Clamp(defaultTeamSize, 1, BattleTeamConstants.MaxUnitsPerTeam);
        defaultUnitLevel = Mathf.Max(1, defaultUnitLevel);
        defaultStatMultiplier = Mathf.Max(0f, defaultStatMultiplier);
    }

    private void OnEnable()
    {
        if (!ActiveBootstrappers.Contains(this))
            ActiveBootstrappers.Add(this);

        // BC 녹화 모드면 모든 Agent의 BehaviorType과 DemonstrationRecorder를 자동 설정.
        // 사용자는 Inspector에서 recordDemonstrations 체크박스만 토글하면 됨.
        if (recordDemonstrations)
        {
            ApplyDemonstrationRecordingMode();
            Debug.Log(
                "[TrainingBootstrapper] ▶ BC RECORDING MODE. 룰베이스 AI가 검투사를 움직이고 행동을 .demo로 녹화. "
                + $"각 Agent {demonstrationStepsPerAgent} step 후 자동 종료. 결과: Assets/Demonstrations/*.demo"
            );
        }
        else
        {
            ApplyTrainingMode();
            Debug.Log(
                "[TrainingBootstrapper] ▶ TRAINING MODE. mlagents-learn 명령으로 외부 trainer를 먼저 실행하지 않으면 "
                + "검투사가 정지합니다. 녹화하려면 Inspector에서 'Record Demonstrations'를 체크하세요."
            );
        }

        if (manuallyStepAcademy && _academyStepDriver == null)
            ClaimAcademyStepDriver();
    }

    private void ApplyDemonstrationRecordingMode()
    {
        ConfigureAgentArrayForRecording(allyAgents);
        ConfigureAgentArrayForRecording(enemyAgents);
    }

    private void ApplyTrainingMode()
    {
        ConfigureAgentArrayForTraining(allyAgents);
        ConfigureAgentArrayForTraining(enemyAgents);
    }

    private void ConfigureAgentArrayForRecording(GladiatorAgent[] agents)
    {
        if (agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            GladiatorAgent agent = agents[i];
            if (agent == null)
                continue;

            BehaviorParameters bp = agent.GetComponent<BehaviorParameters>();
            if (bp != null)
                bp.BehaviorType = BehaviorType.HeuristicOnly;

            // prefab에 Recorder가 없으면 runtime에서 자동 추가. prefab YAML 변경 불필요.
            DemonstrationRecorder rec = agent.GetComponent<DemonstrationRecorder>();
            if (rec == null)
                rec = agent.gameObject.AddComponent<DemonstrationRecorder>();

            rec.Record = true;
            rec.NumStepsToRecord = demonstrationStepsPerAgent;
            if (string.IsNullOrEmpty(rec.DemonstrationName))
                rec.DemonstrationName = demonstrationName;
        }
    }

    private void ConfigureAgentArrayForTraining(GladiatorAgent[] agents)
    {
        if (agents == null)
            return;

        for (int i = 0; i < agents.Length; i++)
        {
            GladiatorAgent agent = agents[i];
            if (agent == null)
                continue;

            BehaviorParameters bp = agent.GetComponent<BehaviorParameters>();
            if (bp != null)
                bp.BehaviorType = BehaviorType.Default;

            DemonstrationRecorder rec = agent.GetComponent<DemonstrationRecorder>();
            if (rec != null)
                rec.Record = false;
        }
    }

    private void OnDisable()
    {
        if (battleSimulationManager != null)
            battleSimulationManager.OnBattleFinished -= HandleBattleFinished;

        DisposeTrainingGroups();
        ActiveBootstrappers.Remove(this);
        ReleaseAcademyStepDriver();
    }

    private void Start()
    {
        if (battleSimulationManager != null)
        {
            battleSimulationManager.SetAutoStepInUpdate(false);
            battleSimulationManager.OnBattleFinished -= HandleBattleFinished;
            battleSimulationManager.OnBattleFinished += HandleBattleFinished;
        }

        BattleStartPayload payload = CreatePayload();
        if (payload == null)
            return;

        IReadOnlyDictionary<BattleTeamId, Vector3[]> spawnPositionsByTeam = GenerateRandomPlacements(payload);
        battleSceneFlowManager.ResetAndBootstrap(payload, spawnPositionsByTeam);

        battleSceneFlowManager.OnUnitsSpawned += RefreshAllUnitAnimations;
        RefreshAllUnitAnimations();
        LinkAgentsToUnits();
    }

    private void FixedUpdate()
    {
        if (_episodeEnding)
        {
            return;
        }

        if (manuallyStepAcademy)
        {
            if (_academyStepDriver == null)
                ClaimAcademyStepDriver();

            if (_academyStepDriver != this)
                return;

            StepAllTrainingEnvironments();
            return;
        }

        StepTrainingEnvironment();
        TryResetFinishedOrTimedOutEpisode();
    }

    // 전투 종료와 timeout은 battle tick이 진행된 뒤 같은 step 경계에서 정산한다.
    private void TryResetFinishedOrTimedOutEpisode()
    {
        if (_episodeEnding || battleSimulationManager == null)
            return;

        if (_episodeResetRequested)
        {
            ResetEpisode(TrainingEpisodeEndReason.Requested);
            return;
        }

        if (battleSimulationManager.IsBattleFinished)
        {
            ResetEpisode(TrainingEpisodeEndReason.BattleFinished);
            return;
        }

        if (battleSimulationManager.BattleTickCount >= BattleTimeoutTicks)
        {
            ResetEpisode(TrainingEpisodeEndReason.Timeout);
        }
    }

    // 이 전투 환경 하나에 대해 고정된 battle tick 수만큼 논리 시뮬레이션을 진행한다.
    private void StepTrainingEnvironment()
    {
        if (battleSimulationManager == null)
            return;

        battleSimulationManager.StepSimulationTicks(battleTicksPerEnvironmentStep);
    }

    // Academy step을 먼저 진행해 agent action을 갱신한 뒤, 모든 전투 환경을 동일한 step 경계에서 진행한다.
    private static void StepAllTrainingEnvironments()
    {
        Academy.Instance.EnvironmentStep();

        for (int i = 0; i < ActiveBootstrappers.Count; i++)
        {
            TrainingBootstrapper bootstrapper = ActiveBootstrappers[i];
            if (bootstrapper == null || !bootstrapper.isActiveAndEnabled || bootstrapper._episodeEnding)
                continue;

            bootstrapper.StepTrainingEnvironment();
        }

        for (int i = 0; i < ActiveBootstrappers.Count; i++)
        {
            TrainingBootstrapper bootstrapper = ActiveBootstrappers[i];
            if (bootstrapper == null || !bootstrapper.isActiveAndEnabled)
                continue;

            bootstrapper.TryResetFinishedOrTimedOutEpisode();
        }
    }

    private void ResetEpisode(TrainingEpisodeEndReason reason)
    {
        _episodeEnding = true;
        _episodeResetRequested = false;
        bool isTimeout = reason == TrainingEpisodeEndReason.Timeout;
        BattleTeamId? winnerTeamId =
            reason == TrainingEpisodeEndReason.BattleFinished ? _lastOutcome?.WinnerTeamId : null;
        EndTrainingGroups(reason, winnerTeamId, isTimeout);

        BattleStartPayload payload = CreatePayload();
        if (payload == null)
        {
            _episodeEnding = false;
            return;
        }

        IReadOnlyDictionary<BattleTeamId, Vector3[]> spawnPositionsByTeam = GenerateRandomPlacements(payload);
        bool resetOk = battleSceneFlowManager.ResetAndBootstrap(payload, spawnPositionsByTeam);
        if (!resetOk)
        {
            Debug.LogError("[TrainingBootstrapper] ResetEpisode failed. Could not reset battlefield.", this);
            _episodeEnding = false;
            return;
        }

        RefreshAllUnitAnimations();
        LinkAgentsToUnits();
        _lastOutcome = null;
        _episodeEnding = false;
    }

    public void RequestEpisodeReset()
    {
        if (!_episodeEnding)
        {
            _episodeResetRequested = true;
        }
    }

    private void HandleBattleFinished(BattleOutcome outcome)
    {
        _lastOutcome = outcome;
    }

    private void ClaimAcademyStepDriver()
    {
        _academyStepDriver = this;

        // ML-Agents 4.0.2에서는 DisableAutomaticStepping() 대신 이 공개 프로퍼티로 제어한다.
        _academySteppingWasAutomatic = Academy.Instance.AutomaticSteppingEnabled;
        Academy.Instance.AutomaticSteppingEnabled = false;
    }

    // 이 bootstrapper가 소유한 수동 stepping 권한을 반납하고 Academy 설정을 복구한다.
    private void ReleaseAcademyStepDriver()
    {
        if (_academyStepDriver != this)
            return;

        Academy.Instance.AutomaticSteppingEnabled = _academySteppingWasAutomatic;

        _academyStepDriver = null;
    }

    private void ForEachAgent(System.Action<GladiatorAgent> action)
    {
        foreach (GladiatorAgent agent in allyAgents)
        {
            if (agent != null)
            {
                action(agent);
            }
        }

        foreach (GladiatorAgent agent in enemyAgents)
        {
            if (agent != null)
            {
                action(agent);
            }
        }
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

    private void LinkAgentsToUnits()
    {
        BattleStartPayload payload =
            battleSimulationManager != null
                ? battleSimulationManager.InitialPayload
                : battleSceneFlowManager.CurrentPayload;
        if (payload == null)
        {
            Debug.LogError("[TrainingBootstrapper] LinkAgentsToUnits failed. Battle payload is missing.", this);
            return;
        }

        BattleRosterProjection projection = new BattleRosterProjection(payload);
        List<BattleRuntimeUnit> playerUnits = GetSortedUnitsForTeam(payload.GetPlayerTeam().TeamId, projection);
        List<BattleRuntimeUnit> hostileUnits = GetSortedUnitsForTeam(payload.GetHostileTeam().TeamId, projection);

        Debug.Log(
            $"[TrainingBootstrapper] Linking agents: {playerUnits.Count} player units / {allyAgents.Length} ally agents, {hostileUnits.Count} hostile units / {enemyAgents.Length} enemy agents."
        );

        ResetTrainingGroups();

        for (int i = 0; i < allyAgents.Length; i++)
        {
            if (allyAgents[i] == null)
            {
                continue;
            }

            BattleRuntimeUnit unit = i < playerUnits.Count ? playerUnits[i] : null;
            allyAgents[i].Initialize(unit, battleSceneFlowManager, this);
            if (unit != null)
            {
                _allyGroup.RegisterAgent(allyAgents[i]);
            }
        }

        for (int i = 0; i < enemyAgents.Length; i++)
        {
            if (enemyAgents[i] == null)
            {
                continue;
            }

            BattleRuntimeUnit unit = i < hostileUnits.Count ? hostileUnits[i] : null;
            enemyAgents[i].Initialize(unit, battleSceneFlowManager, this);
            if (unit != null)
            {
                _enemyGroup.RegisterAgent(enemyAgents[i]);
            }
        }
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

    private void EndTrainingGroups(TrainingEpisodeEndReason reason, BattleTeamId? winnerTeamId, bool isTimeout)
    {
        if (!usePocaGroupRewards || _allyGroup == null || _enemyGroup == null)
        {
            ForEachAgent(agent => agent.GiveEndReward(winnerTeamId, isTimeout));
            ForEachAgent(agent => agent.EndEpisode());
            return;
        }

        if (reason == TrainingEpisodeEndReason.BattleFinished && winnerTeamId.HasValue)
        {
            bool allyWon = winnerTeamId.Value == BattleTeamIds.Player;
            _allyGroup.AddGroupReward(allyWon ? groupWinReward : groupLossReward);
            _enemyGroup.AddGroupReward(allyWon ? groupLossReward : groupWinReward);
            _allyGroup.EndGroupEpisode();
            _enemyGroup.EndGroupEpisode();
            EndInactiveAgentEpisodes(interrupted: false);
            return;
        }

        float interruptionReward =
            reason == TrainingEpisodeEndReason.Timeout ? groupTimeoutReward : groupInterruptedReward;
        _allyGroup.AddGroupReward(interruptionReward);
        _enemyGroup.AddGroupReward(interruptionReward);
        _allyGroup.GroupEpisodeInterrupted();
        _enemyGroup.GroupEpisodeInterrupted();
        EndInactiveAgentEpisodes(interrupted: true);
    }

    private void EndInactiveAgentEpisodes(bool interrupted)
    {
        ForEachAgent(agent =>
        {
            if (agent.HasControlledUnit)
                return;

            if (interrupted)
                agent.EpisodeInterrupted();
            else
                agent.EndEpisode();
        });
    }

    private IReadOnlyDictionary<BattleTeamId, Vector3[]> GenerateRandomPlacements(BattleStartPayload payload)
    {
        BattleTeamEntry playerTeam = payload.GetPlayerTeam();
        BattleTeamEntry hostileTeam = payload.GetHostileTeam();

        SphereCollider col = battleSceneFlowManager.battlefieldCollider;
        Vector3 center = col != null ? col.bounds.center : Vector3.zero;
        float radius = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) * 0.85f : 5f;

        float bodyRadius = battleSimulationManager.UnitBodyRadius;
        float minSeparation = bodyRadius * 2f;

        float divAngle = Random.Range(0f, Mathf.PI * 2f);
        float nx = Mathf.Cos(divAngle);
        float nz = Mathf.Sin(divAngle);
        bool playerOnPositiveSide = Random.value > 0.5f;

        int playerCount = playerTeam.Units.Count;
        int hostileCount = hostileTeam.Units.Count;
        var placed = new List<Vector3>(playerCount + hostileCount);
        var playerPositions = new Vector3[playerCount];
        var hostilePositions = new Vector3[hostileCount];

        for (int i = 0; i < playerCount; i++)
        {
            playerPositions[i] = SampleHalfCircle(center, radius, nx, nz, playerOnPositiveSide, placed, minSeparation);
            placed.Add(playerPositions[i]);
        }

        for (int i = 0; i < hostileCount; i++)
        {
            hostilePositions[i] = SampleHalfCircle(
                center,
                radius,
                nx,
                nz,
                !playerOnPositiveSide,
                placed,
                minSeparation
            );
            placed.Add(hostilePositions[i]);
        }

        return new Dictionary<BattleTeamId, Vector3[]>
        {
            [playerTeam.TeamId] = playerPositions,
            [hostileTeam.TeamId] = hostilePositions,
        };
    }

    private static Vector3 SampleHalfCircle(
        Vector3 center,
        float radius,
        float nx,
        float nz,
        bool positiveSide,
        IList<Vector3> placed,
        float minSeparation
    )
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            float x = Random.Range(-radius, radius);
            float z = Random.Range(-radius, radius);
            if (x * x + z * z > radius * radius)
            {
                continue;
            }

            float dot = x * nx + z * nz;
            if (positiveSide ? dot <= 0f : dot >= 0f)
            {
                continue;
            }

            Vector3 candidate = center + new Vector3(x, 0f, z);
            bool overlaps = false;
            for (int j = 0; j < placed.Count; j++)
            {
                if ((candidate - placed[j]).sqrMagnitude < minSeparation * minSeparation)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                return candidate;
            }
        }

        float sign = positiveSide ? 1f : -1f;
        return center + new Vector3(nx * radius * 0.4f * sign, 0f, nz * radius * 0.4f * sign);
    }

    private BattleStartPayload CreatePayload()
    {
        var allySnapshots = new List<BattleUnitSnapshot>();
        var enemySnapshots = new List<BattleUnitSnapshot>();
        int teamSize = ResolveTeamSize();

        for (int i = 0; i < teamSize; i++)
        {
            if (!TryGetTrainingUnitConfig(preset?.allyTeam, i, out BattleTestUnitConfig entry))
            {
                continue;
            }

            allySnapshots.Add(
                CreateSnapshot(i + 1, BattleTeamIds.Player, "Ally", entry, PickRandomClass(entry.classSO))
            );
        }

        for (int i = 0; i < teamSize; i++)
        {
            if (!TryGetTrainingUnitConfig(preset?.enemyTeam, i, out BattleTestUnitConfig entry))
            {
                continue;
            }

            enemySnapshots.Add(
                CreateSnapshot(i + 1, BattleTeamIds.Enemy, "Enemy", entry, PickRandomClass(entry.classSO))
            );
        }

        BattleTeamEntry playerTeam = new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, allySnapshots);
        BattleTeamEntry hostileTeam = new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots);

        return new BattleStartPayload(
            new[] { playerTeam, hostileTeam },
            BattleTeamIds.Player,
            selectedEncounterIndex: 0,
            enemyAverageLevel: preset != null ? preset.enemyAverageLevel : defaultUnitLevel,
            previewRewardGold: preset != null ? preset.previewRewardGold : 0,
            battleSeed: Random.Range(1, 1000000)
        );
    }

    private int ResolveTeamSize()
    {
        float requestedTeamSize = defaultTeamSize;
        if (useCurriculumTeamSize && !string.IsNullOrWhiteSpace(teamSizeEnvironmentParameter))
        {
            requestedTeamSize = Academy.Instance.EnvironmentParameters.GetWithDefault(
                teamSizeEnvironmentParameter,
                defaultTeamSize
            );
        }

        return Mathf.Clamp(Mathf.RoundToInt(requestedTeamSize), 1, BattleTeamConstants.MaxUnitsPerTeam);
    }

    private bool TryGetTrainingUnitConfig(
        IReadOnlyList<BattleTestUnitConfig> teamConfig,
        int unitIndex,
        out BattleTestUnitConfig entry
    )
    {
        entry = default;
        if (teamConfig == null || teamConfig.Count == 0)
        {
            entry = CreateDefaultTrainingUnitConfig();
            if (!HasRandomClassPool())
            {
                Debug.LogError(
                    "[TrainingBootstrapper] Random class pool is required when no training preset team config exists.",
                    this
                );
                return false;
            }

            return true;
        }

        entry = teamConfig[unitIndex % teamConfig.Count];
        if (entry.classSO == null && !HasRandomClassPool())
        {
            Debug.LogError(
                "[TrainingBootstrapper] Training unit requires either a preset class or random class pool entry.",
                this
            );
            return false;
        }

        if (entry.level <= 0)
            entry.level = defaultUnitLevel;

        if (entry.statMultiplier <= 0f)
            entry.statMultiplier = defaultStatMultiplier;

        if (entry.weaponData == null)
            entry.weaponData = PickRandomWeapon(null);

        return true;
    }

    private BattleTestUnitConfig CreateDefaultTrainingUnitConfig()
    {
        return new BattleTestUnitConfig
        {
            level = defaultUnitLevel,
            weaponData = PickRandomWeapon(null),
            statMultiplier = defaultStatMultiplier,
        };
    }

    private GladiatorClassSO PickRandomClass(GladiatorClassSO fallback)
    {
        if (!HasRandomClassPool())
            return fallback;

        int startIndex = Random.Range(0, randomClassPool.Length);
        for (int offset = 0; offset < randomClassPool.Length; offset++)
        {
            GladiatorClassSO classSO = randomClassPool[(startIndex + offset) % randomClassPool.Length];
            if (classSO != null)
                return classSO;
        }

        return fallback;
    }

    private bool HasRandomClassPool()
    {
        if (randomClassPool == null)
            return false;

        for (int i = 0; i < randomClassPool.Length; i++)
        {
            if (randomClassPool[i] != null)
                return true;
        }

        return false;
    }

    private WeaponSO PickRandomWeapon(WeaponSO fallback)
    {
        if (randomWeaponPool == null || randomWeaponPool.Length == 0)
            return fallback;

        int startIndex = Random.Range(0, randomWeaponPool.Length);
        for (int offset = 0; offset < randomWeaponPool.Length; offset++)
        {
            WeaponSO weaponSO = randomWeaponPool[(startIndex + offset) % randomWeaponPool.Length];
            if (weaponSO != null)
                return weaponSO;
        }

        return fallback;
    }

    private static BattleUnitSnapshot CreateSnapshot(
        int sourceRuntimeId,
        BattleTeamId teamId,
        string displayPrefix,
        BattleTestUnitConfig entry,
        GladiatorClassSO classOverride
    )
    {
        GladiatorClassSO classSO = classOverride != null ? classOverride : entry.classSO;
        if (classSO == null)
        {
            Debug.LogError("[TrainingBootstrapper] Cannot create snapshot without a gladiator class.");
            return null;
        }

        int lv = Mathf.Max(1, entry.level);
        float mult = entry.statMultiplier <= 0 ? 1f : entry.statMultiplier;

        float baseHp = classSO.baseHealth + classSO.healthGrowthPerLevel * (lv - 1);
        float baseAtk = classSO.baseAttack + classSO.attackGrowthPerLevel * (lv - 1);

        float finalHp = (entry.healthOverride > 0 ? entry.healthOverride : baseHp) * mult;
        float finalAtk = (entry.attackOverride > 0 ? entry.attackOverride : baseAtk) * mult;
        float finalAtkSpeed = (entry.attackSpeedOverride > 0 ? entry.attackSpeedOverride : classSO.attackSpeed) * mult;
        float finalMove = (entry.moveSpeedOverride > 0 ? entry.moveSpeedOverride : classSO.moveSpeed) * mult;
        float finalRange = entry.attackRangeOverride > 0 ? entry.attackRangeOverride : classSO.attackRange;

        WeaponType resolvedType = WeaponType.None;
        GameObject leftPrefab = null;
        GameObject rightPrefab = null;
        bool isRanged = false;
        bool useProjectile = false;

        if (entry.weaponData != null)
        {
            resolvedType = entry.weaponData.weaponType;
            leftPrefab = entry.weaponData.leftWeaponPrefab;
            rightPrefab = entry.weaponData.rightWeaponPrefab;
            isRanged = entry.weaponData.isRanged;
            useProjectile = entry.weaponData.useProjectile;
        }

        if (entry.overrideWeaponSettings)
        {
            isRanged = entry.isRanged;
            useProjectile = entry.useProjectile;
        }

        int[] randomSkins =
            GladiatorSkinManager.Instance != null ? GladiatorSkinManager.Instance.GenerateRandomSkinIndicates() : null;

        return new BattleUnitSnapshot(
            sourceRuntimeId: sourceRuntimeId,
            teamId: teamId,
            displayName: $"{displayPrefix} {classSO.className}",
            level: lv,
            loyalty: 100,
            maxHealth: finalHp,
            currentHealth: finalHp,
            attack: finalAtk,
            attackSpeed: finalAtkSpeed,
            moveSpeed: finalMove,
            attackRange: finalRange,
            gladiatorClass: classSO,
            trait: null,
            personality: null,
            equippedPerk: null,
            weaponType: resolvedType,
            leftWeaponPrefab: leftPrefab,
            rightWeaponPrefab: rightPrefab,
            weaponSkillId: entry.weaponSkillId,
            customizeIndicates: randomSkins,
            isRanged: isRanged,
            useProjectile: useProjectile,
            portraitSprite: classSO.icon
        );
    }

    private List<BattleRuntimeUnit> GetSortedUnitsForTeam(BattleTeamId teamId, BattleRosterProjection projection)
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleRuntimeUnit Unit)>();
        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            if (unit == null || unit.TeamId != teamId)
            {
                continue;
            }

            // 녹화 모드면 BuiltInAI 유지: 룰베이스가 unit.CurrentActionType/PlannedTarget을 채워야
            // Heuristic이 그 값을 ML-Agents action으로 변환할 수 있다.
            // 학습/추론 모드에서만 ExternalAgent로 전환.
            if (!recordDemonstrations)
            {
                unit.SetControlMode(BattleUnitControlMode.ExternalAgent);
            }
            sorted.Add((ResolveSortIndex(unit, projection), unit.UnitNumber, unit));
        }

        sorted.Sort(
            (left, right) =>
            {
                int byIndex = left.SortIndex.CompareTo(right.SortIndex);
                return byIndex != 0 ? byIndex : left.UnitNumber.CompareTo(right.UnitNumber);
            }
        );

        var result = new List<BattleRuntimeUnit>(sorted.Count);
        for (int i = 0; i < sorted.Count; i++)
        {
            result.Add(sorted[i].Unit);
        }

        return result;
    }

    private static int ResolveSortIndex(BattleRuntimeUnit unit, BattleRosterProjection projection)
    {
        if (projection.IsPlayerUnit(unit) && projection.TryGetPlayerIndex(unit, out int playerIndex))
        {
            return playerIndex;
        }

        if (projection.TryGetHostileIndex(unit, out int hostileIndex))
        {
            return hostileIndex;
        }

        return unit != null ? unit.UnitNumber : int.MaxValue;
    }
}
