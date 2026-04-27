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
    private string mainSceneName = "MainScene"; // New Game 버튼에서 이동할 메인 씬 이름

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
    private Button settingsCloseButton;

    [SerializeField]
    private Dropdown languageDropdown;

    [SerializeField]
    private Slider bgmVolumeSlider;

    [SerializeField]
    private Slider sfxVolumeSlider;

    [SerializeField]
    private Slider brightnessSlider;

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
    private Button[] _loadGameSlotButtons;
    private TMP_Text[] _loadGameSlotTexts;

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
        BindButton(settingsCloseButton, OnCloseSettingsClicked);
        BindButton(quitButton, OnQuitClicked);
        BindButton(loadGameCloseButton, OnCloseLoadGameClicked);

        CacheSettingsControls();
        BindSettingsControls();
        SyncSettingsControlsFromGlobalValues();

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

        TryStartMainSceneLoad();
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

        EnsureLoadGameModal();

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

    // 설정 버튼: 옵션 UI 연동 시 구현 예정
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
        SyncSettingsControlsFromGlobalValues();

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Settings modal opened.", this);
        }
    }

    // 모달 바깥의 어두운 배경을 눌러도 같은 닫기 흐름을 타게 한다.
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

    // 로드 게임 모달 닫기 버튼 및 배경 클릭에서 공통으로 호출되는 닫기 흐름이다.
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

    // 언어 변경값을 전역 설정에 저장한다.
    private void OnLanguageChanged(int selectedIndex)
    {
        GameSettings.SetLanguage((GameLanguage)selectedIndex);
    }

    // BGM 슬라이더 변화값을 전역 설정과 오디오에 반영한다.
    private void OnBgmVolumeChanged(float value)
    {
        GameSettings.SetBgmVolume(value);
        ApplyAudioSettings();
    }

    // SFX 슬라이더 변화값을 전역 설정과 오디오에 반영한다.
    private void OnSfxVolumeChanged(float value)
    {
        GameSettings.SetSfxVolume(value);
        ApplyAudioSettings();
    }

    // 밝기 슬라이더 변화값을 전역 설정과 현재 씬 조명에 반영한다.
    private void OnBrightnessChanged(float value)
    {
        GameSettings.SetBrightness(value);
        GameSettings.ApplyBrightnessToCurrentScene();
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

        if (languageDropdown == null)
        {
            languageDropdown = FindChildComponent<Dropdown>(modalRootTransform, "LanguageDropdown");
        }

        if (bgmVolumeSlider == null)
        {
            bgmVolumeSlider = FindChildComponent<Slider>(modalRootTransform, "BgmSlider");
        }

        if (sfxVolumeSlider == null)
        {
            sfxVolumeSlider = FindChildComponent<Slider>(modalRootTransform, "SfxSlider");
        }

        if (brightnessSlider == null)
        {
            brightnessSlider = FindChildComponent<Slider>(modalRootTransform, "BrightnessSlider");
        }

        Transform backdropTransform = FindChildTransform(modalRootTransform, "DimBackground");
        if (backdropTransform != null)
        {
            Image backdropImage = backdropTransform.GetComponent<Image>();
            _settingsBackdropButton = backdropTransform.GetComponent<Button>();

            if (_settingsBackdropButton == null)
            {
                _settingsBackdropButton = backdropTransform.gameObject.AddComponent<Button>();
            }

            _settingsBackdropButton.transition = Selectable.Transition.None;
            _settingsBackdropButton.targetGraphic = backdropImage;
        }
    }

    // 씬에 이미 배치된 로드 모달 참조를 캐싱하고, 배경 클릭 닫기 버튼을 준비한다.
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

    // 모달 안의 실제 UI 컴포넌트를 씬 계층 이름으로 찾아 둔다.
    private void BindSettingsControls()
    {
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        if (_settingsBackdropButton != null)
        {
            _settingsBackdropButton.onClick.RemoveListener(OnCloseSettingsClicked);
            _settingsBackdropButton.onClick.AddListener(OnCloseSettingsClicked);
        }
    }

    // 저장된 전역 설정값을 현재 UI 상태로 되돌린다.
    private void SyncSettingsControlsFromGlobalValues()
    {
        if (languageDropdown != null)
        {
            languageDropdown.SetValueWithoutNotify((int)GameSettings.Language);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
        }
    }

    // 슬롯 텍스트들을 캐싱해 두면 이후 실제 세이브 데이터 연결 시 갱신만 하면 된다.
    private void CacheLoadGameSlotTextReferences(Transform modalRootTransform)
    {
        _loadGameSlotButtons = new Button[5];
        _loadGameSlotTexts = new TMP_Text[5];

        if (loadGameSlotButtons == null || loadGameSlotButtons.Length != 5)
        {
            loadGameSlotButtons = new Button[5];
        }

        if (loadGameSlotTexts == null || loadGameSlotTexts.Length != 5)
        {
            loadGameSlotTexts = new TMP_Text[5];
        }

        for (int slotIndex = 0; slotIndex < _loadGameSlotTexts.Length; slotIndex++)
        {
            int slotNumber = slotIndex + 1;
            Button slotButton = loadGameSlotButtons[slotIndex];
            if (slotButton == null)
            {
                slotButton = FindLoadSlotButton(modalRootTransform, slotNumber);
            }

            _loadGameSlotButtons[slotIndex] = slotButton;
            loadGameSlotButtons[slotIndex] = slotButton;

            string slotTextName = $"Slot{slotIndex + 1}Text";
            TMP_Text slotText = loadGameSlotTexts[slotIndex];
            if (slotText == null)
            {
                slotText = FindChildComponent<TMP_Text>(modalRootTransform, slotTextName);
            }

            _loadGameSlotTexts[slotIndex] = slotText;
            loadGameSlotTexts[slotIndex] = slotText;

            if (_loadGameSlotTexts[slotIndex] is TextMeshProUGUI tmpText)
            {
                tmpText.raycastTarget = false;
            }

            slotButton = _loadGameSlotButtons[slotIndex];
            if (slotButton != null)
            {
                int capturedSlotNumber = slotNumber;
                slotButton.onClick.RemoveAllListeners();
                slotButton.onClick.AddListener(() => OnLoadGameSlotClicked(capturedSlotNumber));

                if (verboseLog)
                {
                    Debug.Log(
                        $"[TitleSceneUIManager] Bound load slot button: Slot{capturedSlotNumber} -> {slotButton.name}",
                        this
                    );
                }
            }

            if (verboseLog)
            {
                string textName = _loadGameSlotTexts[slotIndex] != null ? _loadGameSlotTexts[slotIndex].name : "null";
                string buttonName = slotButton != null ? slotButton.name : "null";
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
        if (_loadGameSlotTexts == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < _loadGameSlotTexts.Length; slotIndex++)
        {
            TMP_Text slotText = _loadGameSlotTexts[slotIndex];
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
            return "Empty Slot";
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

        return $"SLOT {slotNumber}  |  DAY: {preview.day}  |  GOLD: {preview.gold}  |  SAVED: {savedTimeText}";
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

    // 씬에 모달이 없으면 런타임에 기본형 UI를 자동 생성해 즉시 테스트 가능하게 만든다.
    private void EnsureLoadGameModal()
    {
        // 씬에 로드 모달이 없을 때만 테스트 가능한 기본 UI를 동적으로 생성한다.
        if (loadGameModalRoot != null)
        {
            return;
        }

        Transform canvasTransform = ResolveCanvasTransform();
        if (canvasTransform == null)
        {
            Debug.LogError("[TitleSceneUIManager] Canvas not found. Cannot create load game modal.", this);
            return;
        }

        Font defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        loadGameModalRoot = CreateUiObject("LoadGameModalRoot", canvasTransform);
        RectTransform modalRootRect = loadGameModalRoot.GetComponent<RectTransform>();
        StretchToParent(modalRootRect);

        GameObject dimBackgroundObject = CreateUiObject("DimBackground", modalRootRect);
        RectTransform dimRect = dimBackgroundObject.GetComponent<RectTransform>();
        StretchToParent(dimRect);

        Image dimImage = dimBackgroundObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.75f);

        _loadGameBackdropButton = dimBackgroundObject.AddComponent<Button>();
        _loadGameBackdropButton.transition = Selectable.Transition.None;
        _loadGameBackdropButton.targetGraphic = dimImage;
        _loadGameBackdropButton.onClick.RemoveListener(OnCloseLoadGameClicked);
        _loadGameBackdropButton.onClick.AddListener(OnCloseLoadGameClicked);

        GameObject panelObject = CreateUiObject("LoadGamePanel", modalRootRect);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(920f, 620f);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.09f, 0.1f, 0.12f, 0.96f);

        GameObject titleObject = CreateUiObject("TitleText", panelRect);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(-120f, 48f);

        Text titleText = titleObject.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.fontSize = 28;
        titleText.color = Color.white;
        titleText.text = "LOAD GAME";

        GameObject closeButtonObject = CreateUiObject("CloseButton", panelRect);
        RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-24f, -24f);
        closeRect.sizeDelta = new Vector2(42f, 42f);

        Image closeImage = closeButtonObject.AddComponent<Image>();
        closeImage.color = new Color(0.22f, 0.23f, 0.27f, 1f);

        loadGameCloseButton = closeButtonObject.AddComponent<Button>();
        loadGameCloseButton.targetGraphic = closeImage;
        loadGameCloseButton.onClick.RemoveListener(OnCloseLoadGameClicked);
        loadGameCloseButton.onClick.AddListener(OnCloseLoadGameClicked);

        GameObject closeTextObject = CreateUiObject("CloseText", closeRect);
        RectTransform closeTextRect = closeTextObject.GetComponent<RectTransform>();
        StretchToParent(closeTextRect);

        Text closeText = closeTextObject.AddComponent<Text>();
        closeText.font = defaultFont;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.fontSize = 24;
        closeText.color = Color.white;
        closeText.text = "X";

        _loadGameSlotButtons = new Button[5];
        _loadGameSlotTexts = new TMP_Text[5];

        if (loadGameSlotButtons == null || loadGameSlotButtons.Length != 5)
        {
            loadGameSlotButtons = new Button[5];
        }

        if (loadGameSlotTexts == null || loadGameSlotTexts.Length != 5)
        {
            loadGameSlotTexts = new TMP_Text[5];
        }

        for (int slotIndex = 0; slotIndex < _loadGameSlotTexts.Length; slotIndex++)
        {
            float topOffset = 110f + (slotIndex * 92f);

            GameObject slotObject = CreateUiObject($"Slot{slotIndex + 1}", panelRect);
            RectTransform slotRect = slotObject.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0f, 1f);
            slotRect.anchorMax = new Vector2(1f, 1f);
            slotRect.pivot = new Vector2(0.5f, 1f);
            slotRect.anchoredPosition = new Vector2(0f, -topOffset);
            slotRect.sizeDelta = new Vector2(-52f, 74f);

            Image slotImage = slotObject.AddComponent<Image>();
            slotImage.color = new Color(0.14f, 0.15f, 0.18f, 1f);

            Button slotButton = slotObject.AddComponent<Button>();
            slotButton.targetGraphic = slotImage;
            int slotNumber = slotIndex + 1;
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => OnLoadGameSlotClicked(slotNumber));

            _loadGameSlotButtons[slotIndex] = slotButton;
            loadGameSlotButtons[slotIndex] = slotButton;

            GameObject slotTextObject = CreateUiObject($"Slot{slotIndex + 1}Text", slotRect);
            RectTransform slotTextRect = slotTextObject.GetComponent<RectTransform>();
            slotTextRect.anchorMin = new Vector2(0f, 0f);
            slotTextRect.anchorMax = new Vector2(1f, 1f);
            slotTextRect.pivot = new Vector2(0.5f, 0.5f);
            slotTextRect.anchoredPosition = Vector2.zero;
            slotTextRect.sizeDelta = new Vector2(-36f, -18f);

            TextMeshProUGUI slotText = slotTextObject.AddComponent<TextMeshProUGUI>();
            slotText.font = Resources.Load<TMP_FontAsset>("LiberationSans SDF TMP_Font");
            slotText.alignment = TextAlignmentOptions.Left;
            slotText.fontSize = 20;
            slotText.color = new Color(0.93f, 0.95f, 0.98f, 1f);
            slotText.text = BuildLoadSlotPreviewText(slotIndex + 1);

            slotText.raycastTarget = false;

            _loadGameSlotTexts[slotIndex] = slotText;
            loadGameSlotTexts[slotIndex] = slotText;
        }

        loadGameModalRoot.SetActive(false);
    }

    private Transform ResolveCanvasTransform()
    {
        // 설정 모달의 부모 캔버스를 우선 사용하고, 없으면 씬의 첫 Canvas를 fallback으로 쓴다.
        if (settingsModalRoot != null && settingsModalRoot.transform.parent != null)
        {
            return settingsModalRoot.transform.parent;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (canvases.Length <= 0)
        {
            return null;
        }

        return canvases[0].transform;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
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

    // 오디오 매니저가 있으면 전역 볼륨값을 다시 적용한다.
    private void ApplyAudioSettings()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return;
        }

        audioManager.ApplyFromGlobalSettings();
    }
}
