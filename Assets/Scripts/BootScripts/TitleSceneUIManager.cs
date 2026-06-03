using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleSceneUIManager : MonoBehaviour
{
    [Header("Title Scene")]
    [SerializeField]
    private string mainSceneName = "MainScene";

    [Header("Buttons")]
    [SerializeField]
    private Button newGameButton;

    [SerializeField]
    private Button loadGameButton;

    [SerializeField]
    private Button settingsButton;

    [SerializeField]
    private Button quitButton;

    [Header("Settings Modal")]
    [SerializeField]
    private GameObject settingsModalRoot;

    [SerializeField]
    private RawImage settingsDimBackground;

    [SerializeField]
    private TMP_Text settingsGameTitleText;

    [SerializeField]
    private Button gameSettingButton;

    [SerializeField]
    private Button audioSettingButton;

    [SerializeField]
    private Button videoSettingButton;

    [SerializeField]
    private Button keySettingButton;

    [Header("Load Game Modal")]
    [SerializeField]
    private GameObject loadGameModalRoot;

    [SerializeField]
    private Button loadGameCloseButton;

    [SerializeField]
    private Button[] loadGameSlotButtons = new Button[5];

    [SerializeField]
    private TMP_Text[] loadGameSlotTexts = new TMP_Text[5];

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private SceneLoader _sceneLoader;
    private bool _initialized; // 버튼 이벤트 중복 바인딩 방지
    private bool _isNavigating; // 씬 이동 중 중복 클릭 방지
    private Button _settingsBackdropButton;
    private Button _loadGameBackdropButton;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _sceneLoader = SceneLoader.Instance;

        BindButton(newGameButton, OnNewGameClicked);
        BindButton(loadGameButton, OnLoadGameClicked);
        BindButton(settingsButton, OnSettingsClicked);
        BindButton(quitButton, OnQuitClicked);
        BindButton(loadGameCloseButton, OnCloseLoadGameClicked);

        CacheSettingsControls();
        BindSettingsControls();

        CacheLoadGameControls();

        if (loadGameModalRoot != null)
        {
            loadGameModalRoot.SetActive(false);
        }

        if (settingsModalRoot != null)
        {
            settingsModalRoot.SetActive(false);
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Title scene initialized.", this);
        }
    }

    // 새 게임 시작 버튼: 메인 씬으로 진입
    private void OnNewGameClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        SaveGameService.SetPendingLoadedData(null);
        ResetRuntimeSessionForNewGame();

        TryStartMainSceneLoad();
    }

    // 타이틀로 돌아온 뒤 새 게임을 시작할 때 DDOL 세션 상태를 명시적으로 초기화한다.
    // 부팅 시퀀스는 앱 실행 시 한 번만 돌기 때문에, 여기서 초기화하지 않으면 이전 플레이의 일차/전투 사용 상태가 남을 수 있다.
    private void ResetRuntimeSessionForNewGame()
    {
        SessionManager.Instance?.StartNewSession();
        RandomManager.Instance?.InitializeForNewSession();
        ResourceManager.Instance?.ResetForNewSession();
        InventoryManager.Instance?.ResetForNewSession();
        GladiatorManager.Instance?.ResetForNewSession();
        MarketManager.Instance?.ResetForNewSession();
        SquadManager.Instance?.ResetForNewSession();
        BattleSessionManager.Instance?.ClearPayload();
        RecruitFactory.ResetReservedGladiatorDisplayNames();
    }

    private bool TryStartMainSceneLoad()
    {
        if (_sceneLoader == null)
        {
            Debug.LogError("[TitleSceneUIManager] SceneLoader.Instance is null.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogError("[TitleSceneUIManager] mainSceneName is empty.", this);
            return false;
        }

        _isNavigating = true;

        bool started = _sceneLoader.TryLoadMainScene(mainSceneName);
        if (!started)
        {
            _isNavigating = false;
            Debug.LogWarning("[TitleSceneUIManager] Failed to start MainScene load.", this);
            return false;
        }

        if (verboseLog)
        {
            Debug.Log($"[TitleSceneUIManager] Loading main scene: {mainSceneName}", this);
        }

        return true;
    }

    // 로드 게임 버튼: 저장 슬롯 미리보기 모달을 열고 나중에 로직을 연결할 준비를 한다.
    private void OnLoadGameClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (loadGameModalRoot == null)
        {
            Debug.LogError("[TitleSceneUIManager] loadGameModalRoot is null.", this);
            return;
        }

        RefreshLoadGameSlotPreviewTexts();
        loadGameModalRoot.SetActive(true);

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Load game modal opened.", this);
        }
    }

    // 설정 버튼
    private void OnSettingsClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (settingsModalRoot == null)
        {
            Debug.LogError("[TitleSceneUIManager] settingsModalRoot is null.", this);
            return;
        }

        settingsModalRoot.SetActive(true);

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Settings modal opened.", this);
        }
    }

    // 모달 바깥의 배경을 눌러도 같은 닫기 실행
    private void OnCloseSettingsClicked()
    {
        if (settingsModalRoot == null)
        {
            Debug.LogError("[TitleSceneUIManager] settingsModalRoot is null.", this);
            return;
        }

        settingsModalRoot.SetActive(false);

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Settings modal closed.", this);
        }
    }

    // LOAD GAME 모달 닫기 버튼 및 배경 클릭에서 공통으로 호출되는 닫기 실행
    private void OnCloseLoadGameClicked()
    {
        if (loadGameModalRoot == null)
        {
            return;
        }

        loadGameModalRoot.SetActive(false);

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Load game modal closed.", this);
        }
    }

    // 종료 버튼: 에디터에서는 플레이 모드 종료, 빌드에서는 앱 종료
    private void OnQuitClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Quit button clicked.", this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.AddListener(action);
    }

    private void CacheSettingsControls()
    {
        if (settingsModalRoot == null)
        {
            return;
        }

        Transform modalRootTransform = settingsModalRoot.transform;

        if (settingsDimBackground == null)
        {
            settingsDimBackground = FindChildComponent<RawImage>(modalRootTransform, "DimBackground");
        }

        if (settingsGameTitleText == null)
        {
            settingsGameTitleText = FindChildComponent<TMP_Text>(modalRootTransform, "GameTitleText");
        }

        if (gameSettingButton == null)
        {
            gameSettingButton = FindChildComponent<Button>(modalRootTransform, "GameSettingButton");
        }

        if (audioSettingButton == null)
        {
            audioSettingButton = FindChildComponent<Button>(modalRootTransform, "AudioSettingButton");
        }

        if (videoSettingButton == null)
        {
            videoSettingButton = FindChildComponent<Button>(modalRootTransform, "VideoSettingButton");
        }

        if (keySettingButton == null)
        {
            keySettingButton = FindChildComponent<Button>(modalRootTransform, "KeySettingButton");
        }

        if (settingsDimBackground == null)
        {
            return;
        }

        _settingsBackdropButton = settingsDimBackground.GetComponent<Button>();
        if (_settingsBackdropButton == null)
        {
            _settingsBackdropButton = settingsDimBackground.gameObject.AddComponent<Button>();
        }

        _settingsBackdropButton.transition = Selectable.Transition.None;
        _settingsBackdropButton.targetGraphic = settingsDimBackground;
    }

    // 씬에 이미 배치된 로드 모달 참조를 캐싱하고, 배경 클릭 닫기 버튼을 준비
    private void CacheLoadGameControls()
    {
        if (loadGameModalRoot == null)
        {
            return;
        }

        Transform modalRootTransform = loadGameModalRoot.transform;

        if (loadGameCloseButton == null)
        {
            loadGameCloseButton = FindChildComponent<Button>(modalRootTransform, "CloseButton");
        }

        Transform backdropTransform = FindChildTransform(modalRootTransform, "DimBackground");
        if (backdropTransform != null)
        {
            Image backdropImage = backdropTransform.GetComponent<Image>();
            _loadGameBackdropButton = backdropTransform.GetComponent<Button>();

            if (_loadGameBackdropButton == null)
            {
                _loadGameBackdropButton = backdropTransform.gameObject.AddComponent<Button>();
            }

            _loadGameBackdropButton.transition = Selectable.Transition.None;
            _loadGameBackdropButton.targetGraphic = backdropImage;
            _loadGameBackdropButton.onClick.RemoveListener(OnCloseLoadGameClicked);
            _loadGameBackdropButton.onClick.AddListener(OnCloseLoadGameClicked);
        }

        CacheLoadGameSlotTextReferences(modalRootTransform);
    }

    // 모달 배경을 누르면 설정 선택 화면을 닫는다.
    private void BindSettingsControls()
    {
        if (_settingsBackdropButton == null)
        {
            return;
        }

        _settingsBackdropButton.onClick.RemoveListener(OnCloseSettingsClicked);
        _settingsBackdropButton.onClick.AddListener(OnCloseSettingsClicked);
    }

    // 슬롯 텍스트들을 캐싱해 두면 이후 실제 세이브 데이터 연결 시 갱신만 하면 된다.
    private void CacheLoadGameSlotTextReferences(Transform modalRootTransform)
    {
        if (loadGameSlotButtons == null || loadGameSlotButtons.Length != 5)
        {
            loadGameSlotButtons = new Button[5];
        }

        if (loadGameSlotTexts == null || loadGameSlotTexts.Length != 5)
        {
            loadGameSlotTexts = new TMP_Text[5];
        }

        for (int slotIndex = 0; slotIndex < loadGameSlotButtons.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            Button slotButton = loadGameSlotButtons[slotIndex];
            if (slotButton == null)
            {
                slotButton = FindLoadSlotButton(modalRootTransform, slotNumber);
                loadGameSlotButtons[slotIndex] = slotButton;
            }

            string slotTextName = $"Slot{slotIndex + 1}Text";
            TMP_Text slotText = loadGameSlotTexts[slotIndex];
            if (slotText == null)
            {
                slotText = FindChildComponent<TMP_Text>(modalRootTransform, slotTextName);
                loadGameSlotTexts[slotIndex] = slotText;
            }

            if (loadGameSlotTexts[slotIndex] is TextMeshProUGUI tmpText)
            {
                tmpText.raycastTarget = false;
            }

            if (loadGameSlotButtons[slotIndex] != null)
            {
                int capturedSlotNumber = slotNumber;
                loadGameSlotButtons[slotIndex].onClick.RemoveAllListeners();
                loadGameSlotButtons[slotIndex].onClick.AddListener(() => OnLoadGameSlotClicked(capturedSlotNumber));

                if (verboseLog)
                {
                    Debug.Log(
                        $"[TitleSceneUIManager] Bound load slot button: Slot{capturedSlotNumber} -> {loadGameSlotButtons[slotIndex].name}",
                        this
                    );
                }
            }

            if (verboseLog)
            {
                string textName = loadGameSlotTexts[slotIndex] != null ? loadGameSlotTexts[slotIndex].name : "null";
                string buttonName =
                    loadGameSlotButtons[slotIndex] != null ? loadGameSlotButtons[slotIndex].name : "null";
                Debug.Log(
                    $"[TitleSceneUIManager] Load slot bind: index={slotNumber}, button={buttonName}, text={textName}",
                    this
                );
            }
        }
    }

    private static Button FindLoadSlotButton(Transform modalRootTransform, int slotNumber)
    {
        Button button = FindChildComponent<Button>(modalRootTransform, $"Slot{slotNumber}Button");
        if (button != null)
        {
            return button;
        }

        return FindChildComponent<Button>(modalRootTransform, $"Slot{slotNumber}");
    }

    // 저장 데이터 프리뷰를 기반으로 슬롯 텍스트를 갱신한다.
    private void RefreshLoadGameSlotPreviewTexts()
    {
        if (loadGameSlotTexts == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < loadGameSlotTexts.Length; slotIndex++)
        {
            TMP_Text slotText = loadGameSlotTexts[slotIndex];
            if (slotText == null)
            {
                continue;
            }

            slotText.text = BuildLoadSlotPreviewText(slotIndex + 1);
        }
    }

    private static string BuildLoadSlotPreviewText(int slotNumber)
    {
        SaveGameService.SaveSlotPreview preview = SaveGameService.GetSlotPreview(slotNumber);
        if (!preview.hasData)
        {
            return "빈 슬롯";
        }

        string savedTimeText = "-";
        if (
            DateTime.TryParse(
                preview.savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime savedAtUtc
            )
        )
        {
            savedTimeText = savedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return $"슬롯 {slotNumber}  |  일차: {preview.day}  |  골드: {preview.gold}  |  저장일시: {savedTimeText}";
    }

    private void OnLoadGameSlotClicked(int slotNumber)
    {
        // 슬롯에 저장 데이터가 있으면 pending으로 넘기고 메인 씬 로드를 시작한다.
        if (_isNavigating)
        {
            return;
        }

        if (!SaveGameService.TryLoadSlot(slotNumber, out SaveSlotData data) || data == null)
        {
            if (verboseLog)
            {
                Debug.Log($"[TitleSceneUIManager] Load requested for empty slot: {slotNumber}", this);
            }

            return;
        }

        SaveGameService.SetPendingLoadedData(data);
        bool started = TryStartMainSceneLoad();

        if (!started)
        {
            SaveGameService.SetPendingLoadedData(null);
            return;
        }

        if (verboseLog)
        {
            Debug.Log($"[TitleSceneUIManager] Load game clicked. Slot={slotNumber}", this);
        }
    }

    // 자식 계층을 이름으로 재귀 탐색한다.
    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindChildTransform(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    // 이름으로 찾은 자식에서 원하는 컴포넌트를 꺼낸다.
    private static T FindChildComponent<T>(Transform parent, string childName)
        where T : Component
    {
        Transform child = FindChildTransform(parent, childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }
}
