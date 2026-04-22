using System.Collections.Generic;
using BattleTest;
using UnityEngine;

// Training Platform 하나의 에피소드 생명주기를 관리한다.
// BattleSessionManager를 거치지 않고 BattleSceneFlowManager에 payload를 직접 주입한다.
//
// Inspector 설정:
//   - BattleSceneFlowManager.autoBootstrapFromSessionManager = false
//   - allyAgents: GladiatorAgent + BehaviorParameters + DecisionRequester 가 붙은
//     고정 GameObject 배열 (BattleRuntimeUnit 프리팹과 별개의 독립 GameObject)
public class TrainingBootstrapper : MonoBehaviour
{
    public BattleSceneFlowManager battleSceneFlowManager;
    public BattleSimulationManager battleSimulationManager;

    [SerializeField] private BattleTestPresetSO preset;
    [SerializeField] private GladiatorAgent[] allyAgents;
    [SerializeField] private GladiatorAgent[] enemyAgents;

    private const int BattleTimeoutTicks = 1 * 60 * 60;

    private bool _episodeEnding;

    private void Start()
    {
        BattleStartPayload payload = CreatePayload();
        var (allyPos, enemyPos) = GenerateRandomPlacements(payload.AllyUnits.Count, payload.EnemyUnits.Count);
        battleSceneFlowManager.ResetAndBootstrap(payload, allyPos, enemyPos);

        battleSceneFlowManager.OnUnitsSpawned += RefreshAllUnitAnimations;
        RefreshAllUnitAnimations();

        LinkAgentsToUnits();
    }

    private void Update()
    {
        if (_episodeEnding)
            return;

        // Debug.Log("[TrainingBootstrapper] Tick: " + battleSimulationManager.BattleTickCount);
        // if (!battleSimulationManager.IsBattleFinished
        //     && battleSimulationManager.BattleTickCount >= BattleTimeoutTicks)
        // {
        //     ResetEpisode(isTimeout: true);
        //     return;
        // }

        if (battleSimulationManager.IsBattleFinished)
            ResetEpisode(isTimeout: false);
    }

    private void ResetEpisode(bool isTimeout)
    {
        _episodeEnding = true;

        ForEachAgent(agent => agent.EndEpisode());

        BattleStartPayload payload = CreatePayload();
        var (allyPos, enemyPos) = GenerateRandomPlacements(payload.AllyUnits.Count, payload.EnemyUnits.Count);
        bool resetOk = battleSceneFlowManager.ResetAndBootstrap(payload, allyPos, enemyPos);
        if (!resetOk)
        {
            Debug.LogError("[TrainingBootstrapper] ResetEpisode failed. Could not reset battlefield.", this);
            _episodeEnding = false;
            return;
        }

        // if (isTimeout)
        // {
        //     Debug.LogWarning("[TrainingBootstrapper] Episode ended due to timeout.");
        //     ForEachAgent(agent => agent.AddReward(-1000f)); // Timeout 패널티
        // }

        RefreshAllUnitAnimations();
        LinkAgentsToUnits();

        _episodeEnding = false;
    }

    private void ForEachAgent(System.Action<GladiatorAgent> action)
    {
        foreach (GladiatorAgent agent in allyAgents)
        {
            if (agent != null)
                action(agent);
        }
        foreach (GladiatorAgent agent in enemyAgents)
        {
            if (agent != null)
                action(agent);
        }
    }

    private void RefreshAllUnitAnimations()
    {
        // BattleSceneFlowManager에서 생성된 모든 유닛에 대해 애니메이션 초기화 또는 리프레시 작업 수행
        if (AnimationManager.Instance == null)
        {
            Debug.LogError("[TrainingBootstrapper] RefreshAllUnitAnimations failed. AnimationManager instance not found.", this);
            return;
        }

        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            if (unit == null || unit.Snapshot == null)
                continue;

            Animator animator = unit.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                continue;
            }

            AnimatorOverrideController controller =
                AnimationManager.Instance.GetControllerByWeaponType(unit.Snapshot.WeaponType);
            if (controller != null && animator.runtimeAnimatorController != controller)
                animator.runtimeAnimatorController = controller;
        }
    }

    private void LinkAgentsToUnits()
    {
        var allyUnits = new List<BattleRuntimeUnit>();
        var enemyUnits = new List<BattleRuntimeUnit>();
        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            unit.SetExternallyControlled(true);
            if (unit.IsEnemy)
                enemyUnits.Add(unit);
            else
                allyUnits.Add(unit);
        }

        Debug.Log($"[TrainingBootstrapper] Linking agents: {allyUnits.Count} ally units / {allyAgents.Length} ally agents, {enemyUnits.Count} enemy units / {enemyAgents.Length} enemy agents.");
        for (int i = 0; i < allyAgents.Length; i++)
        {
            if (allyAgents[i] == null)
                continue;

            BattleRuntimeUnit unit = i < allyUnits.Count ? allyUnits[i] : null;
            allyAgents[i].Initialize(unit, battleSceneFlowManager);
        }
        for (int i = 0; i < enemyAgents.Length; i++)
        {
            if (enemyAgents[i] == null)
                continue;

            BattleRuntimeUnit unit = i < enemyUnits.Count ? enemyUnits[i] : null;
            enemyAgents[i].Initialize(unit, battleSceneFlowManager);
        }
    }

    // 원형 경기장을 원점을 지나는 랜덤 직선으로 반으로 나눈 뒤,
    // 양쪽에 아군/적군을 무작위로 배치한다. 유닛끼리 겹치지 않도록 분리 거리를 보장한다.
    private (Vector3[] allyPositions, Vector3[] enemyPositions) GenerateRandomPlacements(int allyCount, int enemyCount)
    {
        BoxCollider col = battleSceneFlowManager.BattlefieldCollider;
        Vector3 center = col != null ? col.bounds.center : Vector3.zero;
        float radius = col != null
            ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) * 0.85f // 경기장 가장자리 여유
            : 5f;

        float bodyRadius = battleSimulationManager.UnitBodyRadius;
        float minSeparation = bodyRadius * 2f;

        float divAngle = Random.Range(0f, Mathf.PI * 2f);
        float nx = Mathf.Cos(divAngle);
        float nz = Mathf.Sin(divAngle);
        bool allyOnPositiveSide = Random.value > 0.5f;

        var placed = new List<Vector3>(allyCount + enemyCount);
        var allyPos = new Vector3[allyCount];
        var enemyPos = new Vector3[enemyCount];

        for (int i = 0; i < allyCount; i++)
        {
            allyPos[i] = SampleHalfCircle(center, radius, nx, nz, allyOnPositiveSide, placed, minSeparation);
            placed.Add(allyPos[i]);
        }
        for (int i = 0; i < enemyCount; i++)
        {
            enemyPos[i] = SampleHalfCircle(center, radius, nx, nz, !allyOnPositiveSide, placed, minSeparation);
            placed.Add(enemyPos[i]);
        }

        return (allyPos, enemyPos);
    }

    private static Vector3 SampleHalfCircle(
        Vector3 center, float radius, float nx, float nz, bool positiveSide,
        IList<Vector3> placed, float minSeparation)
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            float x = Random.Range(-radius, radius);
            float z = Random.Range(-radius, radius);
            if (x * x + z * z > radius * radius)
                continue;
            float dot = x * nx + z * nz;
            if (positiveSide ? dot <= 0f : dot >= 0f)
                continue;

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
                return candidate;
        }
        // 분리 조건을 만족하는 위치를 찾지 못한 경우 반원 중심 방향으로 fallback
        float sign = positiveSide ? 1f : -1f;
        return center + new Vector3(nx * radius * 0.4f * sign, 0f, nz * radius * 0.4f * sign);
    }

    private BattleStartPayload CreatePayload()
    {
        var allySnapshots = new List<BattleUnitSnapshot>();
        var enemySnapshots = new List<BattleUnitSnapshot>();

        for (int i = 0; i < Mathf.Min(6, preset.allyTeam.Count); i++)
        {
            BattleTestUnitConfig entry = preset.allyTeam[i];
            if (entry.classSO == null)
                continue;
            allySnapshots.Add(CreateSnapshot(i + 1, false, entry));
        }

        for (int i = 0; i < Mathf.Min(6, preset.enemyTeam.Count); i++)
        {
            BattleTestUnitConfig entry = preset.enemyTeam[i];
            if (entry.classSO == null)
                continue;
            enemySnapshots.Add(CreateSnapshot(i + 7, true, entry));
        }

        return new BattleStartPayload(
            allySnapshots, enemySnapshots,
            selectedEncounterIndex: 0,
            enemyAverageLevel: preset.enemyAverageLevel,
            previewRewardGold: preset.previewRewardGold,
            battleSeed: Random.Range(1, 1000000));
    }

    private static BattleUnitSnapshot CreateSnapshot(int id, bool isEnemy, BattleTestUnitConfig entry)
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

        return new BattleUnitSnapshot(
            sourceRuntimeId: id,
            isEnemy: isEnemy,
            displayName: (isEnemy ? "Enemy " : "Ally ") + classSO.className,
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
            isRanged: isRanged,
            useProjectile: useProjectile,
            portraitSprite: classSO.icon
        );
    }
}
