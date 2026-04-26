using System.Collections.Generic;
using BattleTest;
using Unity.MLAgents;
using UnityEngine;

public class TrainingBootstrapper : MonoBehaviour
{
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

    [SerializeField]
    private GladiatorAgent[] allyAgents;

    [SerializeField]
    private GladiatorAgent[] enemyAgents;

    [SerializeField]
    private int battleTicksPerEnvironmentStep = 1;

    // true이면 ML-Agents 자동 FixedUpdate stepper를 끄고 이 bootstrapper가 직접 Academy step을 진행한다.
    [SerializeField]
    private bool manuallyStepAcademy = true;

    private const int BattleTimeoutTicks = 1 * 60 * 60;
    private bool _episodeEnding;
    private bool _episodeResetRequested;

    private void OnValidate()
    {
        battleTicksPerEnvironmentStep = Mathf.Max(1, battleTicksPerEnvironmentStep);
    }

    private void OnEnable()
    {
        if (!ActiveBootstrappers.Contains(this))
            ActiveBootstrappers.Add(this);

        if (manuallyStepAcademy && _academyStepDriver == null)
            ClaimAcademyStepDriver();
    }

    private void OnDisable()
    {
        ActiveBootstrappers.Remove(this);
        ReleaseAcademyStepDriver();
    }

    private void Start()
    {
        if (battleSimulationManager != null)
            battleSimulationManager.SetAutoStepInUpdate(false);

        BattleStartPayload payload = CreatePayload();
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
            ResetEpisode(isTimeout: false);
            return;
        }

        if (battleSimulationManager.IsBattleFinished)
        {
            ResetEpisode(isTimeout: false);
            return;
        }

        if (battleSimulationManager.BattleTickCount >= BattleTimeoutTicks)
        {
            ResetEpisode(isTimeout: true);
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

    private void ResetEpisode(bool isTimeout)
    {
        _episodeEnding = true;
        _episodeResetRequested = false;
        ForEachAgent(agent => agent.EndEpisode());

        BattleStartPayload payload = CreatePayload();
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
        _episodeEnding = false;
    }

    public void RequestEpisodeReset()
    {
        if (!_episodeEnding)
        {
            _episodeResetRequested = true;
        }
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

        for (int i = 0; i < allyAgents.Length; i++)
        {
            if (allyAgents[i] == null)
            {
                continue;
            }

            BattleRuntimeUnit unit = i < playerUnits.Count ? playerUnits[i] : null;
            allyAgents[i].Initialize(unit, battleSceneFlowManager, this);
        }

        for (int i = 0; i < enemyAgents.Length; i++)
        {
            if (enemyAgents[i] == null)
            {
                continue;
            }

            BattleRuntimeUnit unit = i < hostileUnits.Count ? hostileUnits[i] : null;
            enemyAgents[i].Initialize(unit, battleSceneFlowManager, this);
        }
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

        for (int i = 0; i < Mathf.Min(BattleTeamConstants.MaxUnitsPerTeam, preset.allyTeam.Count); i++)
        {
            BattleTestUnitConfig entry = preset.allyTeam[i];
            if (entry.classSO == null)
            {
                continue;
            }

            allySnapshots.Add(CreateSnapshot(i + 1, BattleTeamIds.Player, "Ally", entry));
        }

        for (int i = 0; i < Mathf.Min(BattleTeamConstants.MaxUnitsPerTeam, preset.enemyTeam.Count); i++)
        {
            BattleTestUnitConfig entry = preset.enemyTeam[i];
            if (entry.classSO == null)
            {
                continue;
            }

            enemySnapshots.Add(CreateSnapshot(i + 1, BattleTeamIds.Enemy, "Enemy", entry));
        }

        BattleTeamEntry playerTeam = new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, allySnapshots);
        BattleTeamEntry hostileTeam = new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots);

        return new BattleStartPayload(
            new[] { playerTeam, hostileTeam },
            BattleTeamIds.Player,
            selectedEncounterIndex: 0,
            enemyAverageLevel: preset.enemyAverageLevel,
            previewRewardGold: preset.previewRewardGold,
            battleSeed: Random.Range(1, 1000000)
        );
    }

    private static BattleUnitSnapshot CreateSnapshot(
        int sourceRuntimeId,
        BattleTeamId teamId,
        string displayPrefix,
        BattleTestUnitConfig entry
    )
    {
        GladiatorClassSO classSO = entry.classSO;
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

            unit.SetControlMode(BattleUnitControlMode.ExternalAgent);
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
