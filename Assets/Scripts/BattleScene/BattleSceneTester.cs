using System.Collections.Generic;
using UnityEngine;
using BattleTest;

[DisallowMultipleComponent]
public sealed class BattleSceneTester : MonoBehaviour
{
    [Header("Master Toggle")]
    public bool useTestEnvironment = true;

    [Header("Current Test Scenario")]
    public BattleTestPresetSO currentPreset;

    private void Awake()
    {
        if (!useTestEnvironment)
            return;

        // 이미 Payload가 있다면(정상적인 게임 흐름) 테스트 데이터를 덮어쓰지 않음
        if (BattleSessionManager.Instance != null && BattleSessionManager.Instance.HasPayload)
            return;

        // BattleSessionManager가 씬에 없다면 생성
        if (BattleSessionManager.Instance == null)
        {
            var existingManager = FindFirstObjectByType<BattleSessionManager>();
            if (existingManager == null)
            {
                GameObject managerObj = new GameObject("Temp_BattleSessionManager");
                managerObj.AddComponent<BattleSessionManager>();
                Debug.Log("[BattleSceneTester] Created temporary BattleSessionManager.");
            }
        }

        if (currentPreset == null)
        {
            Debug.LogWarning("[BattleSceneTester] No preset selected! Battle will not start correctly if no payload exists.");
            return;
        }

        CreateAndStoreTestDataFromPreset(currentPreset);

        // 모든 Awake가 Start보다 먼저 실행되므로, 여기서 구독하면 FlowManager.Start() 발화 전에 등록이 보장됨
        var flowManager = FindFirstObjectByType<BattleSceneFlowManager>();
        if (flowManager != null)
            flowManager.OnUnitsSpawned += RefreshAllUnitAnimations;
        else
            Debug.LogWarning("[BattleSceneTester] BattleSceneFlowManager not found. Animation refresh will be skipped.");
    }

    private void OnDestroy()
    {
        var flowManager = FindFirstObjectByType<BattleSceneFlowManager>();
        if (flowManager != null)
            flowManager.OnUnitsSpawned -= RefreshAllUnitAnimations;
    }

    private void Start()
    {
        if (!useTestEnvironment)
            return;

        BattleSimulationManager simManager = FindFirstObjectByType<BattleSimulationManager>();
        if (simManager != null && simManager.simulationSpeedMultiplier <= 0)
        {
            simManager.simulationSpeedMultiplier = 1f;
        }
    }

    private void RefreshAllUnitAnimations()
    {
        BattleSceneFlowManager flowManager = FindFirstObjectByType<BattleSceneFlowManager>();
        if (flowManager == null)
        {
            Debug.LogError("[BattleSceneTester] BattleSceneFlowManager not found during animation refresh.", this);
            return;
        }

        var units = flowManager.RuntimeUnits;

        if (units == null || units.Count == 0)
            return;

        if (AnimationManager.Instance == null)
            return;

        foreach (var unit in units)
        {
            if (unit == null || unit.Snapshot == null)
                continue;

            Animator animator = unit.GetComponentInChildren<Animator>();
            if (animator == null)
                continue;

            // 이미 애니메이션이 적용되어 있을 수도 있지만, 테스트 환경에서는 강제로 덮어씌움
            AnimatorOverrideController weaponMotion = AnimationManager.Instance.GetControllerByWeaponType(unit.Snapshot.WeaponType);
            if (weaponMotion != null && animator.runtimeAnimatorController != weaponMotion)
            {
                animator.runtimeAnimatorController = weaponMotion;
                Debug.Log($"[BattleSceneTester] Force-applied {unit.Snapshot.WeaponType} animation to {unit.name}");
            }
        }
    }

    private void CreateAndStoreTestDataFromPreset(BattleTestPresetSO preset)
    {
        var allySnapshots = new List<BattleUnitSnapshot>();
        var enemySnapshots = new List<BattleUnitSnapshot>();

        for (int i = 0; i < Mathf.Min(6, preset.allyTeam.Count); i++)
        {
            var entry = preset.allyTeam[i];
            if (entry.classSO == null)
                continue;
            allySnapshots.Add(CreateSnapshotFromEntry(i + 1, false, entry));
        }

        for (int i = 0; i < Mathf.Min(6, preset.enemyTeam.Count); i++)
        {
            var entry = preset.enemyTeam[i];
            if (entry.classSO == null)
                continue;
            enemySnapshots.Add(CreateSnapshotFromEntry(i + 7, true, entry));
        }

        int battleSeed = preset.battleSeed != 0 ? preset.battleSeed : Random.Range(1, 1000000);
        Debug.Log("[BattleSceneTester] Generated battle seed: " + battleSeed);
        BattleStartPayload testPayload = new BattleStartPayload(
            allySnapshots, enemySnapshots, 0,
            preset.enemyAverageLevel, preset.previewRewardGold,
            battleSeed
        );

        BattleSessionManager.Instance.StorePayload(testPayload);
        Debug.Log($"[BattleSceneTester] Stored test payload: {preset.scenarioName} (Ally:{allySnapshots.Count}, Enemy:{enemySnapshots.Count})");
    }

    private BattleUnitSnapshot CreateSnapshotFromEntry(int id, bool isEnemy, BattleTestUnitConfig entry)
    {
        var classSO = entry.classSO;
        int lv = Mathf.Max(1, entry.level);
        float mult = entry.statMultiplier <= 0 ? 1f : entry.statMultiplier;

        float baseHp = classSO.baseHealth + (classSO.healthGrowthPerLevel * (lv - 1));
        float baseAtk = classSO.baseAttack + (classSO.attackGrowthPerLevel * (lv - 1));

        float finalHp = (entry.healthOverride > 0 ? entry.healthOverride : baseHp) * mult;
        float finalAtk = (entry.attackOverride > 0 ? entry.attackOverride : baseAtk) * mult;
        float finalAtkSpeed = (entry.attackSpeedOverride > 0 ? entry.attackSpeedOverride : classSO.attackSpeed) * mult;
        float finalMove = (entry.moveSpeedOverride > 0 ? entry.moveSpeedOverride : classSO.moveSpeed) * mult;
        float finalRange = (entry.attackRangeOverride > 0 ? entry.attackRangeOverride : classSO.attackRange);

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

            finalAtk += entry.weaponData.baseAttackBonus;
            finalAtkSpeed += entry.weaponData.baseAttackSpeedBonus;
            finalHp += entry.weaponData.baseHealthBonus;
            finalMove += entry.weaponData.baseMoveSpeedBonus;
            finalRange += entry.weaponData.baseAttackRangeBonus;
        }

        // Weapon Overrides
        if (entry.overrideWeaponSettings)
        {
            isRanged = entry.isRanged;
            useProjectile = entry.useProjectile;
        }

        int[] randomSkins = GladiatorSkinManager.Instance.GenerateRandomSkinIndicates();

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
            customizeIndicates: randomSkins,
            portraitSprite: classSO.icon
        );
    }
}
