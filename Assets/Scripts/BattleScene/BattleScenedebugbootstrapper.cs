using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
[DisallowMultipleComponent]
public sealed class BattleSceneDebugBootstrapper : MonoBehaviour
{
    // ── 더미 유닛 공통 스탯 ───────────────────────────────────
    [Header("Dummy Unit Stats")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private float attack = 30f;
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackRange = 2f;

    // ── 팀 구성 ───────────────────────────────────────────────
    [Header("Team Size (1 ~ 6)")]
    [SerializeField, Range(1, 6)] private int allyCount = 3;
    [SerializeField, Range(1, 6)] private int enemyCount = 3;

    // ── 배틀 메타 ─────────────────────────────────────────────
    [Header("Battle Meta")]
    [SerializeField] private int battleSeed = 42;
    [SerializeField] private float enemyAverageLevel = 1f;
    [SerializeField] private int previewRewardGold = 0;
    [SerializeField] private int selectedEncounterIndex = 0;

    // ── GladiatorClassSO (선택) ───────────────────────────────
    [Header("Optional: assign your GladiatorClassSO")]
    [SerializeField] private GladiatorClassSO gladiatorClassSO; // 없어도 동작함

    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        // 이미 싱글톤이 있으면 (= 정상 플로우로 진입한 경우) 아무것도 안 함
        if (BattleSessionManager.Instance != null)
        {
            Debug.Log("[DebugBootstrapper] BattleSessionManager already exists. Skipping debug injection.");
            Destroy(gameObject);
            return;
        }

        InjectDebugSession();
    }

    private void InjectDebugSession()
    {
        // ── 1. BattleSessionManager 싱글톤 생성 ──────────────
        GameObject managerGO = new GameObject("[DEBUG] BattleSessionManager");
        BattleSessionManager sessionManager = managerGO.AddComponent<BattleSessionManager>();
        DontDestroyOnLoad(managerGO);

        // ── 2. 더미 유닛 스냅샷 생성 ─────────────────────────
        List<BattleUnitSnapshot> allies = CreateDummyTeam(false, allyCount);
        List<BattleUnitSnapshot> enemies = CreateDummyTeam(true, enemyCount);

        // ── 3. Payload 주입 ───────────────────────────────────
        BattleStartPayload payload = new BattleStartPayload(
            allies,
            enemies,
            selectedEncounterIndex,
            enemyAverageLevel,
            previewRewardGold,
            battleSeed
        );

        sessionManager.StorePayload(payload);

        Debug.Log($"[DebugBootstrapper] Injected debug payload. Allies={allies.Count}, Enemies={enemies.Count}");
    }

    private List<BattleUnitSnapshot> CreateDummyTeam(bool isEnemy, int count)
    {
        List<BattleUnitSnapshot> list = new List<BattleUnitSnapshot>();
        string prefix = isEnemy ? "Enemy" : "Ally";

        for (int i = 0; i < count; i++)
        {
            BattleUnitSnapshot snapshot = new BattleUnitSnapshot(
                sourceRuntimeId: i,
                isEnemy: isEnemy,
                displayName: $"{prefix}{i + 1}",
                level: 1,
                loyalty: 50,
                maxHealth: maxHealth,
                currentHealth: maxHealth,
                attack: attack,
                attackSpeed: attackSpeed,
                moveSpeed: moveSpeed,
                attackRange: attackRange,
                gladiatorClass: gladiatorClassSO,   // null 허용
                trait: null,
                personality: null,
                equippedPerk: null,
                weaponType: WeaponType.None,
                leftWeaponPrefab: null,
                rightWeaponPrefab: null,
                weaponSkillId: WeaponSkillId.None,
                isRanged: false,
                useProjectile: false,
                portraitSprite: null
            );

            list.Add(snapshot);
        }

        return list;
    }
}
#endif
