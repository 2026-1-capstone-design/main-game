/*
f
1: afc에 달린 동명의 컴포넌트 인스펙터에 적힌 수치대로 검투사 1~6의 스탯 조정, 무기까지 뽑아서 장착.
2: 무기만 장착, 스탯은 그대로
3: 적군 평균레벨 조정
4: 경험치 지급

5: 상대 모두 사망, 전투 승리
6: 아군 모두 사망, 패배
7: 리플레이
8: 골드 지급 10000 





*/



using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AfcBattleCheatCode : MonoBehaviour
{
    [System.Serializable]
    private struct GladiatorStatOverride
    {
        public float maxHealth;
        public float attack;
        public float moveSpeed;
        public float attackSpeed;

        [Header("(Optional) Weapon Override")]
        public WeaponType weaponType;
        public WeaponSkillId weaponSkillId;
        public int weaponLevel;
    }

    [Header("Scene")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string battleSceneName = "BattleScene";

    [Header("F1/F2 Owned Gladiator Overrides (Slots 1~6)")]
    [SerializeField] private GladiatorStatOverride[] gladiatorOverrides = new GladiatorStatOverride[6];

    [Header("F3 Encounter Override")]
    [SerializeField] private int enemyAverageLevelOverride = 1;

    [Header("F4 Gladiator XP")]
    [SerializeField] private int gladiatorXpGrantAmount = 1000;

    [Header("F8 Gold")]
    [SerializeField] private int goldGrantAmount = 10000;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;

        // F7과 기능이 스왑된 F8 (어느 씬에서나 동작)
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            GrantGold();
        }

        if (activeSceneName == mainSceneName)
        {
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                ApplyOwnedGladiatorOverrides();
            }
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                ApplyEquipmentOnlyOverrides();
            }
            if (keyboard.f3Key.wasPressedThisFrame)
            {
                ApplyEncounterAverageLevelOverride();
            }
            if (keyboard.f4Key.wasPressedThisFrame)
            {
                GrantGladiatorXp();
            }
        }

        if (activeSceneName == battleSceneName)
        {
            if (keyboard.f5Key.wasPressedThisFrame)
            {
                BattleSimulationManager simulationManager = FindBattleSimulationManager();
                if (!CanUseBattleOnlyCheat(simulationManager, "F5"))
                {
                    return;
                }

                DisableEntireTeam(simulationManager, disableEnemies: true);
            }

            if (keyboard.f6Key.wasPressedThisFrame)
            {
                BattleSimulationManager simulationManager = FindBattleSimulationManager();
                if (!CanUseBattleOnlyCheat(simulationManager, "F6"))
                {
                    return;
                }

                DisableEntireTeam(simulationManager, disableEnemies: false);
            }

            // F8과 기능이 스왑된 F7 (배틀 씬에서만 동작)
            if (keyboard.f7Key.wasPressedThisFrame)
            {
                BattleSceneFlowManager flowManager = FindBattleSceneFlowManager();
                if (flowManager == null)
                {
                    Debug.LogWarning("[AfcBattleCheatCode] F7 blocked. BattleSceneFlowManager not found.", this);
                    return;
                }

                RestartCurrentBattle(flowManager);
            }
        }
    }

    // 치트 코드는 어차피 유저들이 안쓰니 성능 지장 없음. 싱글턴 선언하기보단 편하게 이걸로
    private BattleSimulationManager FindBattleSimulationManager()
    {
        return FindFirstObjectByType<BattleSimulationManager>();
    }

    private BattleSceneFlowManager FindBattleSceneFlowManager()
    {
        return FindFirstObjectByType<BattleSceneFlowManager>();
    }

    private bool CanUseBattleOnlyCheat(BattleSimulationManager simulationManager, string cheatName)
    {
        if (simulationManager == null)
        {
            Debug.LogWarning($"[AfcBattleCheatCode] {cheatName} blocked. BattleSimulationManager not found.", this);
            return false;
        }

        if (simulationManager.IsBattleFinished)
        {
            Debug.LogWarning($"[AfcBattleCheatCode] {cheatName} blocked. Battle is already finished.", this);
            return false;
        }

        return true;
    }

    private void ApplyOwnedGladiatorOverrides()
    {
        GladiatorManager gladiatorManager = GladiatorManager.Instance;
        if (gladiatorManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F1 failed. GladiatorManager.Instance is null.", this);
            return;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F1 failed. InventoryManager.Instance is null.", this);
            return;
        }

        EquipmentFactory equipmentFactory = FindFirstObjectByType<EquipmentFactory>();
        if (equipmentFactory == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F1 failed. EquipmentFactory not found in MainScene.", this);
            return;
        }

        IReadOnlyList<OwnedGladiatorData> ownedGladiators = gladiatorManager.OwnedGladiators;
        if (ownedGladiators == null || ownedGladiators.Count == 0)
        {
            Debug.LogWarning("[AfcBattleCheatCode] F1 skipped. No owned gladiators.", this);
            return;
        }

        int count = Mathf.Min(6, Mathf.Min(ownedGladiators.Count, gladiatorOverrides != null ? gladiatorOverrides.Length : 0));
        int equippedWeaponCount = 0;

        for (int i = 0; i < count; i++)
        {
            OwnedGladiatorData gladiator = ownedGladiators[i];
            if (gladiator == null)
            {
                continue;
            }

            GladiatorStatOverride statOverride = gladiatorOverrides[i];

            bool equippedWeapon = TryApplyWeaponOverrideToGladiator(
                gladiatorManager,
                inventoryManager,
                equipmentFactory,
                gladiator,
                statOverride);

            if (equippedWeapon)
            {
                equippedWeaponCount++;
            }

            gladiator.CachedMaxHealth = Mathf.Max(1f, statOverride.maxHealth);
            gladiator.CurrentHealth = gladiator.CachedMaxHealth;
            gladiator.CachedAttack = Mathf.Max(0f, statOverride.attack);
            gladiator.CachedMoveSpeed = Mathf.Max(0f, statOverride.moveSpeed);
            gladiator.CachedAttackSpeed = Mathf.Max(0f, statOverride.attackSpeed);
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[AfcBattleCheatCode] F1 applied. " +
                $"OverriddenOwnedGladiatorCount={count}, EquippedWeaponCount={equippedWeaponCount}",
                this
            );
        }
    }

    private void ApplyEquipmentOnlyOverrides()
    {
        GladiatorManager gladiatorManager = GladiatorManager.Instance;
        if (gladiatorManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F2 failed. GladiatorManager.Instance is null.", this);
            return;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F2 failed. InventoryManager.Instance is null.", this);
            return;
        }

        EquipmentFactory equipmentFactory = FindFirstObjectByType<EquipmentFactory>();
        if (equipmentFactory == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F2 failed. EquipmentFactory not found in MainScene.", this);
            return;
        }

        IReadOnlyList<OwnedGladiatorData> ownedGladiators = gladiatorManager.OwnedGladiators;
        if (ownedGladiators == null || ownedGladiators.Count == 0)
        {
            Debug.LogWarning("[AfcBattleCheatCode] F2 skipped. No owned gladiators.", this);
            return;
        }

        int count = Mathf.Min(6, Mathf.Min(ownedGladiators.Count, gladiatorOverrides != null ? gladiatorOverrides.Length : 0));
        int equippedWeaponCount = 0;

        for (int i = 0; i < count; i++)
        {
            OwnedGladiatorData gladiator = ownedGladiators[i];
            if (gladiator == null)
            {
                continue;
            }

            GladiatorStatOverride statOverride = gladiatorOverrides[i];

            bool equippedWeapon = TryApplyWeaponOverrideToGladiator(
                gladiatorManager,
                inventoryManager,
                equipmentFactory,
                gladiator,
                statOverride);

            if (equippedWeapon)
            {
                equippedWeaponCount++;
            }
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[AfcBattleCheatCode] F2 applied. " +
                $"OverriddenOwnedGladiatorCount={count}, EquippedWeaponCount={equippedWeaponCount}",
                this
            );
        }
    }

    private bool TryApplyWeaponOverrideToGladiator(
    GladiatorManager gladiatorManager,
    InventoryManager inventoryManager,
    EquipmentFactory equipmentFactory,
    OwnedGladiatorData gladiator,
    GladiatorStatOverride statOverride)
    {
        if (gladiatorManager == null || inventoryManager == null || equipmentFactory == null || gladiator == null)
        {
            return false;
        }

        if (statOverride.weaponType == WeaponType.None)
        {
            return false;
        }

        OwnedWeaponData weaponPreview = equipmentFactory.CreateWeaponPreviewFromSpec(
            statOverride.weaponType,
            statOverride.weaponSkillId,
            Mathf.Max(1, statOverride.weaponLevel)
        );

        if (weaponPreview == null)
        {
            Debug.LogWarning(
                $"[AfcBattleCheatCode] Weapon override failed. " +
                $"Gladiator={gladiator.DisplayName}, WeaponType={statOverride.weaponType}, WeaponSkillId={statOverride.weaponSkillId}",
                this
            );
            return false;
        }

        if (!inventoryManager.TryAddOwnedWeaponFromPreview(weaponPreview, out OwnedWeaponData ownedWeapon) || ownedWeapon == null)
        {
            Debug.LogWarning(
                $"[AfcBattleCheatCode] Weapon add failed. " +
                $"Gladiator={gladiator.DisplayName}, WeaponType={statOverride.weaponType}",
                this
            );
            return false;
        }

        if (!gladiatorManager.TryEquipWeapon(gladiator, ownedWeapon, out string failReason))
        {
            inventoryManager.RemoveOwnedWeapon(ownedWeapon);

            Debug.LogWarning(
                $"[AfcBattleCheatCode] Weapon equip failed. " +
                $"Gladiator={gladiator.DisplayName}, Weapon={ownedWeapon.DisplayName}, Reason={failReason}",
                this
            );
            return false;
        }

        return true;
    }

    private void ApplyEncounterAverageLevelOverride()
    {
        BattleManager battleManager = FindFirstObjectByType<BattleManager>();
        if (battleManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F3 failed. BattleManager not found in MainScene.", this);
            return;
        }

        battleManager.SetCheatEncounterAverageLevelOverride(enemyAverageLevelOverride);

        bool regenerated = battleManager.RegenerateDailyEncountersForCheat();
        if (!regenerated)
        {
            Debug.LogWarning("[AfcBattleCheatCode] F3 failed. Encounter regeneration was rejected.", this);
            return;
        }

        BattleUIManager battleUIManager = FindFirstObjectByType<BattleUIManager>();
        if (battleUIManager != null && battleManager.IsBattlePanelOpen)
        {
            battleUIManager.OpenBattlePanel(battleManager.DailyEncounters, battleManager.SelectedEncounterIndex);
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[AfcBattleCheatCode] F3 applied. " +
                $"EncounterAverageLevelOverride={Mathf.Max(1, enemyAverageLevelOverride)}, " +
                $"SelectionCleared=true, PanelRefreshed={(battleUIManager != null && battleManager.IsBattlePanelOpen)}",
                this
            );
        }
    }

    private void DisableEntireTeam(BattleSimulationManager simulationManager, bool disableEnemies)
    {
        if (simulationManager == null)
        {
            return;
        }

        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = simulationManager.RuntimeUnits;
        if (runtimeUnits == null)
        {
            return;
        }

        int affectedCount = 0;

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            if (unit.IsCombatDisabled)
            {
                continue;
            }

            if (unit.IsEnemy != disableEnemies)
            {
                continue;
            }

            unit.ApplyDamage(unit.CurrentHealth + unit.MaxHealth + 999999f);
            affectedCount++;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[AfcBattleCheatCode] {(disableEnemies ? "F5" : "F6")} applied. " +
                $"Team={(disableEnemies ? "Enemy" : "Ally")}, Affected={affectedCount}",
                this
            );
        }
    }

    private void GrantGladiatorXp()
    {
        GladiatorManager gladiatorManager = GladiatorManager.Instance;
        if (gladiatorManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F4 failed. GladiatorManager.Instance is null.", this);
            return;
        }

        int safeAmount = Mathf.Max(0, gladiatorXpGrantAmount);
        gladiatorManager.GrantXpToAllOwnedGladiators(safeAmount, "F4 Cheat XP");

        if (verboseLog)
        {
            Debug.Log($"[AfcBattleCheatCode] F4 applied. All owned gladiators gained +{safeAmount} XP", this);
        }
    }

    private void GrantGold()
    {
        ResourceManager resourceManager = ResourceManager.Instance;
        if (resourceManager == null)
        {
            Debug.LogError("[AfcBattleCheatCode] F8 failed. ResourceManager.Instance is null.", this);
            return;
        }

        resourceManager.AddGold(goldGrantAmount);

        if (verboseLog)
        {
            Debug.Log($"[AfcBattleCheatCode] F8 applied. Gold +{goldGrantAmount}", this);
        }
    }

    private void RestartCurrentBattle(BattleSceneFlowManager flowManager)
    {
        if (flowManager == null)
        {
            return;
        }

        bool restarted = flowManager.RestartCurrentBattle();
        if (!restarted)
        {
            Debug.LogWarning("[AfcBattleCheatCode] F7 failed. FlowManager rejected in-place restart.", this);
            return;
        }

        if (verboseLog)
        {
            Debug.Log("[AfcBattleCheatCode] F7 applied. Battle restarted in-place.", this);
        }
    }
}