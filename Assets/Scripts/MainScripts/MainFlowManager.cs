using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainFlowManager : MonoBehaviour
{
    // 현재 어떤 UI가 통제권을 가지고 있는지 나타냄.
    // 메인/시장/검투사/연구/전투준비 화면 간 전환을 제어함. 이후 메뉴 추가 시 추가 필요.
    private enum UiOwner
    {
        None = 0,
        Main = 1,
        Research = 2,
        Gladiator = 3,
        Market = 4,
        BattlePreparation = 5,
        Inventory = 6,
    }

    [Header("Scene Managers")]
    [SerializeField]
    private MainUIManager mainUIManager;

    [SerializeField]
    private ResourceManager resourceManager;

    [SerializeField]
    private ResourceUIManager resourceUIManager;

    [SerializeField]
    private ResearchManager researchManager;

    [SerializeField]
    private ResearchUIManager researchUIManager;

    [SerializeField]
    private InventoryUIManager inventoryUIManager;

    [SerializeField]
    private BattleManager battleManager;

    [SerializeField]
    private BattleUIManager battleUIManager;

    [SerializeField]
    private GladiatorManager gladiatorManager;

    [SerializeField]
    private GladiatorUIManager gladiatorUIManager;

    [SerializeField]
    private InventoryManager inventoryManager;

    [SerializeField]
    private MarketManager marketManager;

    [SerializeField]
    private MarketUIManager marketUIManager;

    [SerializeField]
    private RecruitFactory recruitFactory;

    [SerializeField]
    private EquipmentFactory equipmentFactory;

    [Header("Battle Scene")]
    [SerializeField]
    private string battleSceneName = "BattleScene"; // 전투 시작 시 실제로 로드할 배틀씬 이름

    [Header("Title Scene")]
    [SerializeField]
    private string titleSceneName = "TitleScene";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private SessionManager _sessionManager;
    private ContentDatabaseProvider _contentDatabaseProvider;
    private RandomManager _randomManager;
    private SceneLoader _sceneLoader;
    private BattleSessionManager _battleSessionManager;
    private UiOwner _uiOwner = UiOwner.None;
    private bool _initialized;

    private void Start()
    {
        InitializeScene();
    }

    // 메인씬 전체 초기화 함수
    // DDOL 매니저 재연결, factory 초기화, 보유 검투사/장비 스타터 지급,
    // 시장/전투 후보 초기화, 각 UI 초기화, 펜딩 전투 보상 지급까지 한 번에 수행
    private void InitializeScene()
    {
        if (_initialized)
        {
            return;
        }

        _sessionManager = SessionManager.Instance;
        _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        _randomManager = RandomManager.Instance;
        _sceneLoader = SceneLoader.Instance;
        _battleSessionManager = BattleSessionManager.Instance;

        resourceManager = ResourceManager.Instance;
        gladiatorManager = GladiatorManager.Instance;
        inventoryManager = InventoryManager.Instance;
        marketManager = MarketManager.Instance;

        if (!ValidateDependencies())
        {
            return;
        }

        BalanceSO balance = _contentDatabaseProvider.Balance;

        equipmentFactory.Initialize(_contentDatabaseProvider, _randomManager);
        recruitFactory.Initialize(_contentDatabaseProvider, _sessionManager, _randomManager, equipmentFactory);

        inventoryManager.Initialize(_contentDatabaseProvider, _randomManager);
        gladiatorManager.Initialize(balance, _randomManager);
        resourceManager.Initialize(balance);
        marketManager.Initialize(
            recruitFactory,
            equipmentFactory,
            gladiatorManager,
            inventoryManager,
            resourceManager,
            _contentDatabaseProvider,
            researchManager
        );
        marketManager.InitializeDay(_sessionManager.CurrentDay);

        gladiatorManager.GrantRandomStarterGladiators(_contentDatabaseProvider, _sessionManager, 6);
        inventoryManager.GrantRandomStarterWeapons(_contentDatabaseProvider);
        researchManager.Initialize(_contentDatabaseProvider);

        battleManager.Initialize(_sessionManager, balance, recruitFactory);
        battleManager.InitializeDay(_sessionManager.CurrentDay);

        TryApplyPendingLoadedData();

        resourceUIManager.Initialize(resourceManager);
        researchUIManager.Initialize(this, researchManager);
        inventoryUIManager.Initialize(this, inventoryManager, researchManager);
        gladiatorUIManager.Initialize(this, gladiatorManager, inventoryManager);
        battleUIManager.Initialize(this, battleManager);
        marketUIManager.Initialize(this, marketManager, resourceManager, gladiatorManager, inventoryManager, researchManager);
        mainUIManager.Initialize(this, _sessionManager);

        TryGrantPendingBattleRewardOnMainSceneEnter();

        _uiOwner = UiOwner.Main;
        ApplyUiState();

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Main scene initialized.", this);
        }
    }

    public void HandleSaveToSlotRequested(int slotIndex)
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        SaveGameService.SaveToSlot(slotIndex);
        mainUIManager.RefreshSaveSlotPreviews();

        if (verboseLog)
        {
            Debug.Log($"[MainFlowManager] Save requested. Slot={slotIndex}", this);
        }
    }

    private bool ValidateDependencies()
    {
        bool ok = true;

        if (_sessionManager == null)
        {
            Debug.LogError(
                "[MainFlowManager] SessionManager.Instance is null. Boot Scene를 거치지 않았거나 DDOL 초기화가 안 된 상태임.",
                this
            );
            ok = false;
        }
        if (_randomManager == null)
        {
            Debug.LogError(
                "[MainFlowManager] RandomManager.Instance is null. Boot Scene를 거치지 않았거나 DDOL 초기화가 안 된 상태임.",
                this
            );
            ok = false;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError(
                "[MainFlowManager] SceneLoader.Instance is null. Boot Scene를 거치지 않았거나 DDOL 초기화가 안 된 상태임.",
                this
            );
            ok = false;
        }

        if (_battleSessionManager == null)
        {
            Debug.LogError(
                "[MainFlowManager] BattleSessionManager.Instance is null. Boot Scene를 거치지 않았거나 DDOL 초기화가 안 된 상태임.",
                this
            );
            ok = false;
        }

        if (string.IsNullOrWhiteSpace(battleSceneName))
        {
            Debug.LogError("[MainFlowManager] battleSceneName is empty.", this);
            ok = false;
        }

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError(
                "[MainFlowManager] ContentDatabaseProvider.Instance is null. Boot Scene를 거치지 않았거나 DDOL 초기화가 안 된 상태임.",
                this
            );
            ok = false;
        }

        if (mainUIManager == null)
        {
            Debug.LogError("[MainFlowManager] mainUIManager is missing.", this);
            ok = false;
        }

        if (resourceManager == null)
        {
            Debug.LogError("[MainFlowManager] resourceManager is missing.", this);
            ok = false;
        }

        if (resourceUIManager == null)
        {
            Debug.LogError("[MainFlowManager] resourceUIManager is missing.", this);
            ok = false;
        }

        if (researchManager == null)
        {
            Debug.LogError("[MainFlowManager] researchManager is missing.", this);
            ok = false;
        }

        if (researchUIManager == null)
        {
            Debug.LogError("[MainFlowManager] researchUIManager is missing.", this);
            ok = false;
        }

        if (inventoryUIManager == null)
        {
            Debug.LogError("[MainFlowManager] inventoryUIManager is missing.", this);
            ok = false;
        }

        if (battleManager == null)
        {
            Debug.LogError("[MainFlowManager] battleManager is missing.", this);
            ok = false;
        }

        if (battleUIManager == null)
        {
            Debug.LogError("[MainFlowManager] battleUIManager is missing.", this);
            ok = false;
        }

        if (gladiatorManager == null)
        {
            Debug.LogError("[MainFlowManager] gladiatorManager is missing.", this);
            ok = false;
        }

        if (gladiatorUIManager == null)
        {
            Debug.LogError("[MainFlowManager] gladiatorUIManager is missing.", this);
            ok = false;
        }

        if (inventoryManager == null)
        {
            Debug.LogError("[MainFlowManager] inventoryManager is missing.", this);
            ok = false;
        }

        if (marketManager == null)
        {
            Debug.LogError("[MainFlowManager] marketManager is missing.", this);
            ok = false;
        }

        if (marketUIManager == null)
        {
            Debug.LogError("[MainFlowManager] marketUIManager is missing.", this);
            ok = false;
        }

        if (recruitFactory == null)
        {
            Debug.LogError("[MainFlowManager] recruitFactory is missing.", this);
            ok = false;
        }

        if (equipmentFactory == null)
        {
            Debug.LogError("[MainFlowManager] equipmentFactory is missing.", this);
            ok = false;
        }

        if (_contentDatabaseProvider != null && _contentDatabaseProvider.Balance == null)
        {
            Debug.LogError("[MainFlowManager] ContentDatabaseProvider.Balance is null.", this);
            ok = false;
        }

        return ok;
    }

    public void HandleGladiatorMenuRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        _uiOwner = UiOwner.Gladiator;
        gladiatorUIManager.OpenPanel();
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Gladiator panel opened.", this);
        }
    }

    public void HandleGladiatorBackRequested()
    {
        if (_uiOwner != UiOwner.Gladiator)
        {
            return;
        }

        gladiatorUIManager.ClosePanel();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Gladiator panel closed. Main UI regained control.", this);
        }
    }

    public void HandleResearchMenuRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        _uiOwner = UiOwner.Research;
        researchUIManager.OpenPanel();
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Research panel opened.", this);
        }
    }

    public void HandleResearchBackRequested()
    {
        if (_uiOwner != UiOwner.Research)
        {
            return;
        }

        researchUIManager.ClosePanel();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Research panel closed. Main UI regained control.", this);
        }
    }

    public void HandleBattleMenuRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        string failReason;
        if (!battleManager.TryOpenBattlePreparation(out failReason))
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[MainFlowManager] " + failReason, this);
            }

            ApplyUiState();
            return;
        }

        _uiOwner = UiOwner.BattlePreparation;
        battleUIManager.OpenBattlePanel(battleManager.DailyEncounters, battleManager.SelectedEncounterIndex);
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Battle preparation panel opened.", this);
        }
    }

    public void HandleBattleEncounterSelected(int encounterIndex)
    {
        if (_uiOwner != UiOwner.BattlePreparation)
        {
            return;
        }

        if (!battleManager.TrySelectEncounter(encounterIndex))
        {
            return;
        }

        battleUIManager.RefreshSelection(battleManager.SelectedEncounterIndex);

        if (verboseLog)
        {
            Debug.Log(
                $"[MainFlowManager] Battle encounter selected. Index={battleManager.SelectedEncounterIndex}",
                this
            );
        }
    }

    // 현재 선택된 전투 후보와 보유 검투사로 전투 시작용 payload를 만든다
    // 그 payload를 boot scene DDOL인 BattleSessionManager에 전달해 저장한 뒤 배틀씬 로드를 시작
    public void HandleBattleStartRequested()
    {
        if (_uiOwner != UiOwner.BattlePreparation)
        {
            return;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError("[MainFlowManager] SceneLoader is null.", this);
            return;
        }

        if (_battleSessionManager == null)
        {
            Debug.LogError("[MainFlowManager] BattleSessionManager is null.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(battleSceneName))
        {
            Debug.LogError("[MainFlowManager] battleSceneName is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError(
                $"[MainFlowManager] Battle scene '{battleSceneName}' is not in Build Settings or cannot be loaded.",
                this
            );
            return;
        }

        if (_sceneLoader.IsLoading)
        {
            Debug.LogWarning("[MainFlowManager] SceneLoader is already loading another scene.", this);
            return;
        }

        if (!TryBuildBattleStartPayload(out BattleStartPayload payload))
        {
            return;
        }

        StartCoroutine(LoadBattleSceneRoutine(payload));
    }

    public void HandleBattlePreparationBackRequested()
    {
        if (_uiOwner != UiOwner.BattlePreparation)
        {
            return;
        }

        battleManager.ClosePreparation();
        battleUIManager.CloseAll();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Battle preparation panel closed. Main UI regained control.", this);
        }
    }

    // 전투 진입 직전에 아군/적군 snapshot과 battleSeed를 묶어
    // BattleStartPayload를 완성함.
    private bool TryBuildBattleStartPayload(out BattleStartPayload payload)
    {
        payload = null;

        if (!battleManager.TryGetSelectedEncounterForBattle(out BattleEncounterPreview encounter))
        {
            Debug.LogWarning("[MainFlowManager] Cannot build battle payload because no encounter is selected.", this);
            return false;
        }

        if (!TryBuildAllySnapshotsForBattle(out List<BattleUnitSnapshot> allySnapshots))
        {
            return false;
        }

        if (!TryBuildEnemySnapshotsForBattle(encounter, out List<BattleUnitSnapshot> enemySnapshots))
        {
            return false;
        }

        int battleSeed =
            _randomManager != null
                ? _randomManager.NextInt(RandomStreamType.BattleSimulation, int.MinValue, int.MaxValue)
                : Random.Range(0, int.MaxValue);

        BattleTeamEntry playerTeam = new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, allySnapshots);
        BattleTeamEntry enemyTeam = new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots);
        BattleTeamEntry[] teams = { playerTeam, enemyTeam };

        payload = new BattleStartPayload(
            teams,
            BattleTeamIds.Player,
            encounter.EncounterIndex,
            encounter.AverageLevel,
            encounter.PreviewRewardGold,
            battleSeed
        );

        return true;
    }

    // 현재 보유 검투사 목록의 앞 최대 BattleTeamConstants.MaxUnitsPerTeam명을 전투용 아군 snapshot으로 복사함.
    // 프로토타입에서 전투에 들어가는 건 실제 인스턴스가 아니라, 각 유닛의 정보를 복사한 스냅샷임. (정보의 변형이나 버그를 방지, 오버헤드 감소)
    // 즉, 실제 보유 데이터 자체를 넘기지 않고 전투 시작용 복사본을 만든다는 것
    private bool TryBuildAllySnapshotsForBattle(out List<BattleUnitSnapshot> allySnapshots)
    {
        allySnapshots = new List<BattleUnitSnapshot>(BattleTeamConstants.MaxUnitsPerTeam);

        if (gladiatorManager == null)
        {
            Debug.LogError("[MainFlowManager] gladiatorManager is null.", this);
            return false;
        }

        IReadOnlyList<OwnedGladiatorData> ownedGladiators = gladiatorManager.OwnedGladiators;
        if (ownedGladiators == null || ownedGladiators.Count == 0)
        {
            Debug.LogWarning("[MainFlowManager] Cannot start battle because there are no owned gladiators.", this);
            return false;
        }

        int count = Mathf.Min(BattleTeamConstants.MaxUnitsPerTeam, ownedGladiators.Count);

        for (int i = 0; i < count; i++)
        {
            OwnedGladiatorData source = ownedGladiators[i];
            if (source == null)
            {
                Debug.LogWarning($"[MainFlowManager] Owned gladiator at index {i} is null. Skipping.", this);
                continue;
            }

            BattleUnitSnapshot snapshot = BattleUnitSnapshot.FromOwnedGladiator(source, BattleTeamIds.Player);
            if (snapshot != null)
            {
                allySnapshots.Add(snapshot);
            }
        }

        if (allySnapshots.Count == 0)
        {
            Debug.LogWarning(
                "[MainFlowManager] Cannot start battle because ally snapshot build result is empty.",
                this
            );
            return false;
        }

        return true;
    }

    // 선택된 전투 후보가 들고 있는 적 preview를 실제 전투용 적 snapshot 리스트로 복사
    private bool TryBuildEnemySnapshotsForBattle(
        BattleEncounterPreview encounter,
        out List<BattleUnitSnapshot> enemySnapshots
    )
    {
        enemySnapshots = new List<BattleUnitSnapshot>(BattleTeamConstants.MaxUnitsPerTeam);

        if (encounter == null)
        {
            Debug.LogError("[MainFlowManager] Encounter is null.", this);
            return false;
        }

        IReadOnlyList<BattleUnitSnapshot> encounterUnits = encounter.EnemyUnits;
        if (encounterUnits == null || encounterUnits.Count == 0)
        {
            Debug.LogWarning("[MainFlowManager] Selected encounter has no enemy units.", this);
            return false;
        }

        int count = Mathf.Min(BattleTeamConstants.MaxUnitsPerTeam, encounterUnits.Count);

        for (int i = 0; i < count; i++)
        {
            BattleUnitSnapshot source = encounterUnits[i];
            if (source == null)
            {
                Debug.LogWarning($"[MainFlowManager] Encounter enemy snapshot at index {i} is null. Skipping.", this);
                continue;
            }

            enemySnapshots.Add(source.Clone());
        }

        if (enemySnapshots.Count == 0)
        {
            Debug.LogWarning(
                "[MainFlowManager] Cannot start battle because enemy snapshot build result is empty.",
                this
            );
            return false;
        }

        return true;
    }

    // 전투 payload를 BattleSessionManager에 저장하고,
    // 배틀씬 로드를 시작한 뒤 '오늘 전투 사용 처리'까지 수행함
    // 로드 시작 실패 시 방어로써 저장된 payload는 즉시 비움
    private IEnumerator LoadBattleSceneRoutine(BattleStartPayload payload)
    {
        if (payload == null)
        {
            yield break;
        }

        if (_battleSessionManager == null)
        {
            Debug.LogError("[MainFlowManager] BattleSessionManager is null.", this);
            yield break;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError("[MainFlowManager] SceneLoader is null.", this);
            yield break;
        }

        _battleSessionManager.StorePayload(payload);

        bool started = _sceneLoader.TryLoadScene(battleSceneName);

        if (!started)
        {
            _battleSessionManager.ClearPayload();
            Debug.LogError($"[MainFlowManager] Failed to start BattleScene load. SceneName={battleSceneName}", this);
            yield break;
        }

        if (_sessionManager != null)
        {
            _sessionManager.MarkBattleUsed();
        }

        battleManager.ClosePreparation();
        battleUIManager.CloseAll();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log(
                $"[MainFlowManager] Battle payload stored. "
                    + $"Teams={payload.Teams.Count}, PlayerTeam={payload.PlayerTeamId.Value}, "
                    + $"EncounterIndex={payload.SelectedEncounterIndex}, BattleSeed={payload.BattleSeed}",
                this
            );
        }

        yield break;
    }

    public void HandleInventoryMenuRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        _uiOwner = UiOwner.Inventory;
        inventoryUIManager.OpenPanel();
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Inventory panel opened.", this);
        }
    }

    public void HandleInventoryBackRequested()
    {
        if (_uiOwner != UiOwner.Inventory)
        {
            return;
        }

        inventoryUIManager.ClosePanel();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Inventory panel closed. Main UI regained control.", this);
        }
    }

    public void HandleReturnToTitleRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError("[MainFlowManager] SceneLoader is null.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            Debug.LogError("[MainFlowManager] titleSceneName is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            Debug.LogError(
                $"[MainFlowManager] Title scene '{titleSceneName}' is not in Build Settings or cannot be loaded.",
                this
            );
            return;
        }

        if (_sceneLoader.IsLoading)
        {
            Debug.LogWarning("[MainFlowManager] SceneLoader is already loading another scene.", this);
            return;
        }

        bool started = _sceneLoader.TryLoadScene(titleSceneName);

        if (!started)
        {
            Debug.LogError($"[MainFlowManager] Failed to start TitleScene load. SceneName={titleSceneName}", this);
            return;
        }

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Returning to title scene.", this);
        }
    }

    public void HandleMarketMenuRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        _uiOwner = UiOwner.Market;
        marketUIManager.OpenMarketHome();
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Market panel opened.", this);
        }
    }

    public void HandleMarketBackRequested()
    {
        if (_uiOwner != UiOwner.Market)
        {
            return;
        }

        marketUIManager.CloseMarket();
        _uiOwner = UiOwner.Main;
        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log("[MainFlowManager] Market panel closed. Main UI regained control.", this);
        }
    }

    // 하루 종료 처리.
    // 날짜를 1일 진행시키고, 새 날짜 기준으로 시장과 전투 후보를 다시 생성함
    public void HandleEodRequested()
    {
        if (_uiOwner != UiOwner.Main)
        {
            return;
        }

        _sessionManager.AdvanceDay();
        marketManager.InitializeDay(_sessionManager.CurrentDay);
        battleManager.InitializeDay(_sessionManager.CurrentDay);

        ApplyUiState();

        if (verboseLog)
        {
            Debug.Log($"[MainFlowManager] EOD complete. CurrentDay={_sessionManager.CurrentDay}", this);
        }
    }

    // 현재 _uiOwner와 전투 사용 여부를 기준으로
    // 메인 메뉴, 전투 버튼, EOD 버튼의 활성 상태를 다시 맞춘다.
    private void ApplyUiState()
    {
        bool isMainOwner = _uiOwner == UiOwner.Main;
        bool canUseBattle = isMainOwner && _sessionManager != null && _sessionManager.CanUseBattleToday();

        mainUIManager.SetMainMenuInteractable(isMainOwner);
        mainUIManager.SetBattleButtonInteractable(canUseBattle);
        mainUIManager.SetEodButtonInteractable(isMainOwner);

        if (_sessionManager != null)
        {
            mainUIManager.RefreshDayText(_sessionManager.CurrentDay);
        }
    }

    // 메인씬 재진입 시 저장돼 있던 펜딩 전투 보상을 실제 골드로 지급함
    // 리팩토링 대상: 지급 성공이 돼야 검투사 전체 승리 XP도 여기서 함께 주어짐.
    private void TryGrantPendingBattleRewardOnMainSceneEnter()
    {
        if (_sessionManager == null)
        {
            Debug.LogError(
                "[MainFlowManager] SessionManager is null while trying to grant pending battle reward.",
                this
            );
            return;
        }

        if (resourceManager == null)
        {
            Debug.LogError(
                "[MainFlowManager] resourceManager is null while trying to grant pending battle reward.",
                this
            );
            return;
        }

        if (!_sessionManager.HasPendingBattleReward)
        {
            return;
        }

        int paidGold = resourceManager.GrantPendingBattleReward(_sessionManager);

        if (paidGold > 0)
        {
            Debug.Log("exp granted");
            gladiatorManager.GrantVictoryXpToAllOwnedGladiators();
        }

        if (verboseLog)
        {
            Debug.Log($"[MainFlowManager] Pending battle reward granted on MainScene enter. PaidGold={paidGold}", this);
        }
    }

    private void TryApplyPendingLoadedData()
    {
        if (!SaveGameService.TryConsumePendingLoadedData(out SaveSlotData data) || data == null)
        {
            return;
        }

        SaveGameService.ApplyLoadedDataToRuntime(data);

        if (verboseLog)
        {
            Debug.Log(
                $"[MainFlowManager] Loaded save applied. Slot={data.slotIndex}, Day={data.day}, Gold={data.gold}",
                this
            );
        }
    }
}
