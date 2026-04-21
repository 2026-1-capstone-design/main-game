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

    private bool _episodeEnding;

    private void Start()
    {
        // 유닛 생성
        BattleStartPayload payload = CreatePayload();
        battleSceneFlowManager.ResetAndBootstrap(payload);

        // Animation 초기화
        battleSceneFlowManager.OnUnitsSpawned += RefreshAllUnitAnimations;
        RefreshAllUnitAnimations();

        LinkAgentsToUnits();
    }

    private void Update()
    {
        if (_episodeEnding || !battleSimulationManager.IsBattleFinished)
            return;

        _episodeEnding = true;

        bool allyWon = CheckAllyWon();
        foreach (GladiatorAgent agent in allyAgents)
        {
            if (agent != null)
                agent.GiveEndReward(allyWon);
        }

        // 유닛 재생성 후 에이전트 재연결
        BattleStartPayload payload = CreatePayload();
        battleSceneFlowManager.ResetAndBootstrap(payload);
        RefreshAllUnitAnimations();
        LinkAgentsToUnits();

        foreach (GladiatorAgent agent in allyAgents)
        {
            if (agent != null)
                agent.EndEpisode();
        }

        _episodeEnding = false;
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
        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            unit.SetExternallyControlled(true);
            if (!unit.IsEnemy)
                allyUnits.Add(unit);
        }

        Debug.Log($"[TrainingBootstrapper] Linking agents to units. Found {allyUnits.Count} ally units and {allyAgents.Length} ally agents.");
        for (int i = 0; i < allyAgents.Length; i++)
        {
            if (allyAgents[i] == null)
                continue;

            BattleRuntimeUnit unit = i < allyUnits.Count ? allyUnits[i] : null;
            allyAgents[i].Initialize(unit, battleSceneFlowManager);
        }
    }

    private bool CheckAllyWon()
    {
        bool hasLivingAlly = false;
        bool hasLivingEnemy = false;
        foreach (BattleRuntimeUnit unit in battleSceneFlowManager.RuntimeUnits)
        {
            if (unit.IsCombatDisabled)
                continue;
            if (unit.IsEnemy)
                hasLivingEnemy = true;
            else
                hasLivingAlly = true;
        }
        return hasLivingAlly && !hasLivingEnemy;
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
