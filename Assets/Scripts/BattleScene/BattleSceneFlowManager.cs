using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSceneFlowManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private BoxCollider battlefieldCollider;
    [SerializeField] private GameObject runtimeUnitRootPrefab;
    [SerializeField] private Transform runtimeUnitRoot;
    [SerializeField] private Transform[] allyPlaceholders = new Transform[6];
    [SerializeField] private Transform[] enemyPlaceholders = new Transform[6];

    [Header("Battle")]
    [SerializeField] private BattleSimulationManager battleSimulationManager;
    [SerializeField] private BattleStatusGridUIManager battleStatusGridUIManager;
    [SerializeField] private BattleSceneUIManager battleSceneUIManager;
    [SerializeField] private BattleOrdersManager battleOrdersManager;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    [Header("Debug / Temporary Ally Stat Override")]
    [SerializeField] private bool useTemporaryAllyAttackRangeOverride = true;

    [Header("Debug / Temporary Enemy Stat Override")]
    [SerializeField] private bool useTemporaryEnemyStatsMatchAllies = true;

#if UNITY_EDITOR
    // ── 에디터 단독 실행용 더미 데이터 ────────────────────────
    [Header("─── Editor Debug Only (BattleScene 단독 실행 시) ───")]
    [Tooltip("BattleScene을 단독으로 Play할 때 더미 데이터로 자동 실행합니다.\n실제 플로우(BootScene → BattleScene)로 진입하면 무시됩니다.")]
    [SerializeField] private bool enableDebugMode = true;

    [SerializeField] private GladiatorClassSO debugGladiatorClass; // null 허용
    [SerializeField, Range(1, 6)] private int debugAllyCount = 3;
    [SerializeField, Range(1, 6)] private int debugEnemyCount = 3;
    [SerializeField] private float debugMaxHealth = 500f;
    [SerializeField] private float debugAttack = 30f;
    [SerializeField] private float debugAttackSpeed = 1f;
    [SerializeField] private float debugMoveSpeed = 3f;
    [SerializeField] private float debugAttackRange = 2f;
    // ─────────────────────────────────────────────────────────
#endif

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();
    private BattleStartPayload _initialPayloadSnapshot;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;

    private void Start()
    {
#if UNITY_EDITOR
        // BattleSessionManager가 없을 때만 더미 주입 (단독 실행 상황)
        if (enableDebugMode && BattleSessionManager.Instance == null)
        {
            InjectDebugSession();
        }
#endif
        BootstrapScene();
    }

#if UNITY_EDITOR
    private void InjectDebugSession()
    {
        GameObject managerGO = new GameObject("[DEBUG] BattleSessionManager");
        BattleSessionManager sessionManager = managerGO.AddComponent<BattleSessionManager>();
        DontDestroyOnLoad(managerGO);

        List<BattleUnitSnapshot> allies = CreateDebugTeam(false, debugAllyCount);
        List<BattleUnitSnapshot> enemies = CreateDebugTeam(true, debugEnemyCount);

        BattleStartPayload payload = new BattleStartPayload(
            allies,
            enemies,
            selectedEncounterIndex: 0,
            enemyAverageLevel: 1f,
            previewRewardGold: 0,
            battleSeed: 42
        );

        sessionManager.StorePayload(payload);

        Debug.Log($"[BattleSceneFlowManager] DEBUG MODE: 더미 payload 주입 완료. Allies={allies.Count}, Enemies={enemies.Count}");
    }

    private List<BattleUnitSnapshot> CreateDebugTeam(bool isEnemy, int count)
    {
        List<BattleUnitSnapshot> list = new List<BattleUnitSnapshot>();
        string prefix = isEnemy ? "Enemy" : "Ally";

        for (int i = 0; i < count; i++)
        {
            list.Add(new BattleUnitSnapshot(
                sourceRuntimeId: i,
                isEnemy: isEnemy,
                displayName: $"{prefix}{i + 1}",
                level: 1,
                loyalty: 50,
                maxHealth: debugMaxHealth,
                currentHealth: debugMaxHealth,
                attack: debugAttack,
                attackSpeed: debugAttackSpeed,
                moveSpeed: debugMoveSpeed,
                attackRange: debugAttackRange,
                gladiatorClass: debugGladiatorClass,
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
            ));
        }

        return list;
    }
#endif

    private void BootstrapScene()
    {
        BattleSessionManager battleSessionManager = BattleSessionManager.Instance;
        if (battleSessionManager == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BattleSessionManager.Instance is null.", this);
            return;
        }

        if (!battleSessionManager.TryGetPayload(out BattleStartPayload payload) || payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BattleStartPayload is missing.", this);
            return;
        }

        if (!ValidateBootstrapSetup(payload))
        {
            return;
        }

        _initialPayloadSnapshot = ClonePayload(payload);

        bool success = BootstrapFromPayload(ClonePayload(_initialPayloadSnapshot));
        if (!success)
        {
            return;
        }

        battleSessionManager.ClearPayload();

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneFlowManager] Battle scene bootstrapped. RuntimeUnitCount={_runtimeUnits.Count}",
                this
            );
        }
    }

    public bool RestartCurrentBattle()
    {
        if (_initialPayloadSnapshot == null)
        {
            Debug.LogError("[BattleSceneFlowManager] Restart failed. Initial payload snapshot is null.", this);
            return false;
        }

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.HideAll();
        }

        DestroyRuntimeUnits();

        bool success = BootstrapFromPayload(ClonePayload(_initialPayloadSnapshot));
        if (!success)
        {
            Debug.LogError("[BattleSceneFlowManager] Restart failed during bootstrap.", this);
            return false;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneFlowManager] Battle restarted in-place. RuntimeUnitCount={_runtimeUnits.Count}",
                this
            );
        }

        return true;
    }

    private bool BootstrapFromPayload(BattleStartPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BootstrapFromPayload failed. Payload is null.", this);
            return false;
        }

        // 임시 밸런스 테스트용: 아군 스냅샷의 사거리를 조정한 뒤, 적군도 같은 수치로 맞춘다.
        payload = BuildTemporaryStatMatchedPayload(payload);

        if (!ValidateBootstrapSetup(payload))
        {
            return false;
        }

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.Initialize();
            battleSceneUIManager.HideAll();
        }

        _runtimeUnits.Clear();

        bool allyOk = SpawnTeam(payload.AllyUnits, allyPlaceholders, false, 1);
        bool enemyOk = SpawnTeam(payload.EnemyUnits, enemyPlaceholders, true, 7);

        if (!allyOk || !enemyOk)
        {
            return false;
        }

        battleOrdersManager.Initialize(_runtimeUnits, battlefieldCollider);

        battleSimulationManager.Initialize(
            _runtimeUnits,
            battlefieldCollider,
            battleStatusGridUIManager,
            battleSceneUIManager,
            payload);

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.RefreshSpeedText();
        }

        return true;
    }

    private void DestroyRuntimeUnits()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
                continue;

            GameObject rootObject = unit.RuntimeRootObject;
            if (rootObject != null)
            {
                rootObject.SetActive(false);
                Destroy(rootObject);
            }
        }

        _runtimeUnits.Clear();
    }

    private bool ValidateBootstrapSetup(BattleStartPayload payload)
    {
        if (payload == null)
            return false;
        if (runtimeUnitRootPrefab == null)
            return false;
        if (battleSimulationManager == null)
            return false;
        if (battleOrdersManager == null)
            return false;
        if (battlefieldCollider == null)
        {
            Debug.LogError("[BattleSceneFlowManager] battlefieldCollider is not assigned. Please assign a BoxCollider.", this);
            return false;
        }

        if (allyPlaceholders == null || allyPlaceholders.Length < 6)
            return false;
        if (enemyPlaceholders == null || enemyPlaceholders.Length < 6)
            return false;

        if (payload.AllyUnits == null || payload.AllyUnits.Count == 0)
            return false;
        if (payload.EnemyUnits == null || payload.EnemyUnits.Count == 0)
            return false;

        return true;
    }

    private bool SpawnTeam(
        IReadOnlyList<BattleUnitSnapshot> snapshots,
        Transform[] placeholders,
        bool isEnemy,
        int unitNumberStart)
    {
        if (snapshots == null)
            return false;

        int spawnCount = Mathf.Min(6, Mathf.Min(snapshots.Count, placeholders.Length));
        Transform parent = runtimeUnitRoot != null ? runtimeUnitRoot : battlefieldCollider.transform;

        for (int i = 0; i < spawnCount; i++)
        {
            BattleUnitSnapshot snapshot = snapshots[i];
            Transform placeholder = placeholders[i];

            if (snapshot == null)
                continue;
            if (placeholder == null)
                return false;

            GameObject runtimeRoot = Instantiate(runtimeUnitRootPrefab, parent);
            BattleRuntimeUnit runtimeUnit = runtimeRoot.GetComponentInChildren<BattleRuntimeUnit>(true);

            if (runtimeUnit == null)
            {
                Destroy(runtimeRoot);
                return false;
            }

            runtimeUnit.Initialize(snapshot.Clone(), unitNumberStart + i, isEnemy);

            runtimeUnit.PlaceOnBattlefieldPlaceholder(placeholder, battlefieldCollider.transform);

            _runtimeUnits.Add(runtimeUnit);
        }

        return true;
    }

    // 슬롯(1~6) 기준 임시 공격 사거리 분포: [1,1,4,4,8,8]
    private static float GetTemporaryAllyAttackRangeBySlot(int allySlot)
    {
        switch (allySlot)
        {
            case 1:
            case 2:
                return 1f;
            case 3:
            case 4:
                return 4f;
            case 5:
            case 6:
                return 8f;
            default:
                return 4f;
        }
    }

    // 전투 시작용 payload를 임시 밸런스 테스트 규칙에 맞게 재구성한다.
    private BattleStartPayload BuildTemporaryStatMatchedPayload(BattleStartPayload source)
    {
        if (source == null)
            return null;

        List<BattleUnitSnapshot> allyUnits = CloneSnapshots(source.AllyUnits);
        if (useTemporaryAllyAttackRangeOverride)
        {
            ApplyTemporaryAllyAttackRangeOverride(allyUnits);
        }

        List<BattleUnitSnapshot> enemyUnits = useTemporaryEnemyStatsMatchAllies
            ? BuildEnemySnapshotsMatchingAllies(source.EnemyUnits, allyUnits)
            : CloneSnapshots(source.EnemyUnits);

        return new BattleStartPayload(
            allyUnits,
            enemyUnits,
            source.SelectedEncounterIndex,
            source.EnemyAverageLevel,
            source.PreviewRewardGold,
            source.BattleSeed
        );
    }

    // 아군 1~6의 사거리를 1,1,4,4,8,8로 임시 조정한다.
    private static void ApplyTemporaryAllyAttackRangeOverride(List<BattleUnitSnapshot> allyUnits)
    {
        if (allyUnits == null)
            return;

        for (int i = 0; i < allyUnits.Count; i++)
        {
            BattleUnitSnapshot ally = allyUnits[i];
            if (ally == null)
                continue;

            float attackRange = GetTemporaryAllyAttackRangeBySlot(i + 1);
            allyUnits[i] = CreateSnapshotWithOverriddenStats(ally, ally.IsEnemy, ally.DisplayName, ally.Level, ally.Loyalty, ally.MaxHealth, ally.CurrentHealth, ally.Attack, ally.AttackSpeed, ally.MoveSpeed, attackRange, ally.GladiatorClass, ally.Trait, ally.Personality, ally.EquippedPerk, ally.WeaponType, ally.LeftWeaponPrefab, ally.RightWeaponPrefab, ally.WeaponSkillId, ally.IsRanged, ally.UseProjectile, ally.PortraitSprite);
        }
    }

    // 적군 스냅샷을 아군 스냅샷과 동일한 수치로 맞춘다. 디스플레이 이름과 enemy flag는 유지한다.
    private static List<BattleUnitSnapshot> BuildEnemySnapshotsMatchingAllies(IReadOnlyList<BattleUnitSnapshot> enemySource, IReadOnlyList<BattleUnitSnapshot> allySource)
    {
        List<BattleUnitSnapshot> result = new List<BattleUnitSnapshot>();
        if (enemySource == null || allySource == null)
            return result;

        int count = Mathf.Min(enemySource.Count, allySource.Count);
        for (int i = 0; i < count; i++)
        {
            BattleUnitSnapshot enemy = enemySource[i];
            BattleUnitSnapshot ally = allySource[i];
            if (enemy == null || ally == null)
                continue;

            // 적군 식별과 외형 정보는 유지하고, 전투 수치만 아군과 동일하게 덮어쓴다.
            result.Add(CreateSnapshotWithOverriddenStats(
                enemy,
                true,
                enemy.DisplayName,
                ally.Level,
                ally.Loyalty,
                ally.MaxHealth,
                ally.CurrentHealth,
                ally.Attack,
                ally.AttackSpeed,
                ally.MoveSpeed,
                ally.AttackRange,
                enemy.GladiatorClass,
                enemy.Trait,
                enemy.Personality,
                enemy.EquippedPerk,
                enemy.WeaponType,
                enemy.LeftWeaponPrefab,
                enemy.RightWeaponPrefab,
                enemy.WeaponSkillId,
                enemy.IsRanged,
                enemy.UseProjectile,
                enemy.PortraitSprite));
        }

        return result;
    }

    // BattleUnitSnapshot은 불변이므로, 임시 테스트용 수치를 반영한 새 스냅샷을 생성한다.
    private static BattleUnitSnapshot CreateSnapshotWithOverriddenStats(
        BattleUnitSnapshot source,
        bool isEnemy,
        string displayName,
        int level,
        int loyalty,
        float maxHealth,
        float currentHealth,
        float attack,
        float attackSpeed,
        float moveSpeed,
        float attackRange,
        GladiatorClassSO gladiatorClass,
        TraitSO trait,
        PersonalitySO personality,
        PerkSO equippedPerk,
        WeaponType weaponType,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab,
        WeaponSkillId weaponSkillId,
        bool isRanged,
        bool useProjectile,
        Sprite portraitSprite)
    {
        if (source == null)
            return null;

        return new BattleUnitSnapshot(
            source.SourceRuntimeId,
            isEnemy,
            displayName,
            level,
            loyalty,
            maxHealth,
            currentHealth,
            attack,
            attackSpeed,
            moveSpeed,
            attackRange,
            gladiatorClass,
            trait,
            personality,
            equippedPerk,
            weaponType,
            leftWeaponPrefab,
            rightWeaponPrefab,
            weaponSkillId,
            isRanged,
            useProjectile,
            portraitSprite
        );
    }

    private BattleStartPayload ClonePayload(BattleStartPayload source)
    {
        List<BattleUnitSnapshot> allyUnits = CloneSnapshots(source.AllyUnits);
        List<BattleUnitSnapshot> enemyUnits = CloneSnapshots(source.EnemyUnits);

        return new BattleStartPayload(
            allyUnits,
            enemyUnits,
            source.SelectedEncounterIndex,
            source.EnemyAverageLevel,
            source.PreviewRewardGold,
            source.BattleSeed
        );
    }

    private List<BattleUnitSnapshot> CloneSnapshots(IReadOnlyList<BattleUnitSnapshot> source)
    {
        List<BattleUnitSnapshot> result = new List<BattleUnitSnapshot>();
        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            BattleUnitSnapshot snapshot = source[i];
            if (snapshot != null)
                result.Add(snapshot.Clone());
        }

        return result;
    }
}
