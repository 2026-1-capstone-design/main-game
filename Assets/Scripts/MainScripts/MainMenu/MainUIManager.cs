using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainUIManager : MonoBehaviour
{
    [Serializable]
    private sealed class MainMenuTooltipEntry
    {
        [SerializeField]
        private Button targetButton;

        [SerializeField]
        private string title;

        [SerializeField]
        [TextArea]
        private string detail;

        public Button TargetButton => targetButton;
        public string Title => title;
        public string Detail => detail;
    }

    [Header("Main Buttons")]
    [SerializeField]
    private Button gladiatorButton;

    [SerializeField]
    private Button squadButton;

    [SerializeField]
    private Button battleButton;

    [SerializeField]
    private Button inventoryButton;

    [SerializeField]
    private Button marketButton;

    [SerializeField]
    private Button eodButton;

    [SerializeField]
    private Button accountbookButton;

    [SerializeField]
    private Button stampButton;

    [Header("Main Menu Tooltip")]
    [SerializeField]
    private GameObject mainMenuTooltipRoot;

    [SerializeField]
    private TMP_Text mainMenuTooltipTitleText;

    [SerializeField]
    private TMP_Text mainMenuTooltipDetailText;

    [SerializeField]
    private Vector2 mainMenuTooltipOffset = new Vector2(24f, -24f);

    [SerializeField]
    private MainMenuTooltipEntry[] mainMenuTooltipEntries = Array.Empty<MainMenuTooltipEntry>();

    [Header("Account Panel")]
    [SerializeField]
    private GameObject accountPanelRoot;

    [SerializeField]
    private TMP_Text teamNameText;

    [SerializeField]
    private TMP_Text gladiatorStatisticsText;

    [SerializeField]
    private TMP_Text goldStatisticsText;

    [SerializeField]
    private Button accountBackButton;

    [SerializeField]
    private int projectedExpenseDays = 3;

    [Header("EOD Panel")]
    [SerializeField]
    private GameObject eodPanelRoot;

    [SerializeField]
    private Button eodBackgroundButton;

    [SerializeField]
    private TMP_Text eodTitleText;

    [SerializeField]
    private TMP_Text eodGoldStatisticsText;

    [Header("Game Over Panel")]
    [SerializeField]
    private GameObject gameOverPanelRoot;

    [SerializeField]
    private GameObject gameOverPanel1Root;

    [SerializeField]
    private Button gameOverPanel1Button;

    [SerializeField]
    private GameObject gameOverPanel2Root;

    [SerializeField]
    private Button gameOverPanel2BackgroundButton;

    [SerializeField]
    private TMP_Text gameOverPanel2StoryText;

    [Header("Escape Menu")]
    [SerializeField]
    private GameObject escapeMenuRoot;

    [SerializeField]
    private Button continueButton;

    [SerializeField]
    private Button settingsButton;

    [SerializeField]
    private Button escapeSaveButton;

    [SerializeField]
    private Button escapeTitleButton;

    [Header("Save Modal")]
    [SerializeField]
    private GameObject savePanelRoot;

    [SerializeField]
    private Button saveCloseButton;

    [SerializeField]
    private Button[] saveSlotButtons = new Button[5];

    [SerializeField]
    private TMP_Text[] saveSlotTexts = new TMP_Text[5];

    [Header("Settings Modal")]
    [SerializeField]
    private GameObject settingsPanelRoot;

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

    [Header("Optional Labels")]
    [SerializeField]
    private TMP_Text dayTitleText;

    [SerializeField]
    private TMP_Text currentDayText;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private MainFlowManager _flow; // 메인 메뉴 버튼 입력을 실제 게임 흐름 처리 함수로 넘김
    private SessionManager _sessionManager;
    private ResourceManager _resourceManager;
    private GladiatorManager _gladiatorManager;
    private bool _initialized;
    private bool _mainMenuInteractable = true;
    private Button _saveBackdropButton;
    private Button _settingsBackdropButton;
    private RectTransform _mainMenuTooltipRectTransform;
    private RectTransform _mainMenuTooltipParentRectTransform;
    private Canvas _mainMenuTooltipCanvas;

    // 메인 버튼들을 모두 MainFlowManager 핸들러에 연결하고,
    // !!DayChanged 이벤트를 구독해!! 날짜 UI를 동기화
    public void Initialize(
        MainFlowManager flow,
        SessionManager sessionManager,
        ResourceManager resourceManager,
        GladiatorManager gladiatorManager
    )
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _sessionManager = sessionManager;
        _resourceManager = resourceManager;
        _gladiatorManager = gladiatorManager;

        BindButton(gladiatorButton, OnGladiatorClicked);
        BindButton(squadButton, OnSquadClicked);
        BindButton(battleButton, OnBattleClicked);
        BindButton(inventoryButton, OnInventoryClicked);
        BindButton(marketButton, OnMarketClicked);
        BindButton(eodButton, OnEodClicked);
        BindButton(accountbookButton, OnAccountbookClicked);
        BindButton(continueButton, OnContinueClicked);
        BindButton(settingsButton, OnSettingsClicked);
        BindButton(escapeSaveButton, OnSaveClicked);
        BindButton(escapeTitleButton, OnTitleClicked);
        BindButton(accountBackButton, OnAccountBackClicked);
        BindButton(eodBackgroundButton, OnEodPanelClicked);
        BindButton(gameOverPanel1Button, OnGameOverPanel1Clicked);
        BindButton(gameOverPanel2BackgroundButton, OnGameOverPanel2BackgroundClicked);
        CacheMainMenuTooltipReferences();
        RegisterMainMenuTooltips();

        CacheSaveModalControls();
        CacheSettingsModalControls();
        BindSaveModalControls();
        BindSettingsModalControls();
        SyncSettingsControlsFromGlobalValues();
        RefreshSaveSlotPreviews();

        if (escapeMenuRoot != null)
        {
            escapeMenuRoot.SetActive(false);
        }

        if (savePanelRoot != null)
        {
            savePanelRoot.SetActive(false);
        }

        if (settingsPanelRoot != null)
        {
            settingsPanelRoot.SetActive(false);
        }

        if (accountPanelRoot != null)
        {
            accountPanelRoot.SetActive(false);
        }

        HideMainMenuTooltip();

        if (eodPanelRoot != null)
        {
            eodPanelRoot.SetActive(false);
        }

        CloseGameOverPanel();

        if (_sessionManager != null)
        {
            _sessionManager.DayChanged += OnDayChanged;
            RefreshDayText(_sessionManager.CurrentDay);
        }

        if (verboseLog)
        {
            Debug.Log(
                "[MainUIManager] Save UI init: "
                    + $"escapeSaveButton={(escapeSaveButton != null ? escapeSaveButton.name : "null")}, "
                    + $"savePanelRoot={(savePanelRoot != null ? savePanelRoot.name : "null")}, "
                    + $"saveCloseButton={(saveCloseButton != null ? saveCloseButton.name : "null")}",
                this
            );

            for (int i = 0; i < saveSlotButtons.Length; i++)
            {
                Button slotButton = saveSlotButtons[i];
                TMP_Text slotText = saveSlotTexts[i];
                Debug.Log(
                    "[MainUIManager] Save slot bind: "
                        + $"index={i + 1}, "
                        + $"button={(slotButton != null ? slotButton.name : "null")}, "
                        + $"text={(slotText != null ? slotText.name : "null")}",
                    this
                );
            }
        }

        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        UpdateMainMenuTooltipPosition();

        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        HandleEscapePressed();
    }

    private void OnDestroy()
    {
        if (_sessionManager != null)
        {
            _sessionManager.DayChanged -= OnDayChanged;
        }
    }

    public void SetMainMenuInteractable(bool value)
    {
        _mainMenuInteractable = value;
        if (!value)
        {
            HideMainMenuTooltip();
        }

        SetButtonInteractable(gladiatorButton, value);
        SetButtonInteractable(squadButton, value);
        SetButtonInteractable(battleButton, value);
        SetButtonInteractable(inventoryButton, value);
        SetButtonInteractable(marketButton, value);
        SetButtonInteractable(eodButton, value);
        SetButtonInteractable(accountbookButton, value);
        SetButtonInteractable(stampButton, value);
    }

    public void SetBattleButtonInteractable(bool value)
    {
        SetButtonInteractable(battleButton, value);
    }

    public void SetEodButtonInteractable(bool value)
    {
        SetButtonInteractable(eodButton, value);
    }

    // 현재 날짜를 메인 화면 텍스트에 반영
    public void RefreshDayText(int currentDay)
    {
        if (dayTitleText != null)
        {
            dayTitleText.text = "DAY";
        }

        if (currentDayText == null)
        {
            return;
        }

        currentDayText.text = currentDay.ToString(CultureInfo.InvariantCulture);
    }

    private void OnDayChanged(int currentDay)
    {
        RefreshDayText(currentDay);
    }

    private void OnTitleClicked()
    {
        if (_flow != null)
        {
            CloseEscapeMenu();
            _flow.HandleReturnToTitleRequested();
        }
    }

    private void OnGladiatorClicked()
    {
        HideMainMenuTooltip();
        if (_flow != null)
        {
            _flow.HandleGladiatorMenuRequested();
        }
    }

    private void OnSquadClicked()
    {
        HideMainMenuTooltip();
        if (_flow != null)
        {
            _flow.HandleSquadMenuRequested();
        }
    }

    private void OnBattleClicked()
    {
        HideMainMenuTooltip();
        if (_flow != null)
        {
            _flow.HandleBattleMenuRequested();
        }
    }

    private void OnInventoryClicked()
    {
        HideMainMenuTooltip();
        if (_flow != null)
        {
            _flow.HandleInventoryMenuRequested();
        }
    }

    private void OnMarketClicked()
    {
        HideMainMenuTooltip();
        if (_flow != null)
        {
            _flow.HandleMarketMenuRequested();
        }
    }

    private void OnEodClicked()
    {
        HideMainMenuTooltip();
        if (eodPanelRoot != null)
        {
            OpenEodPanel();
            return;
        }

        AdvanceDayAfterEod();
    }

    private void OpenEodPanel()
    {
        if (eodTitleText != null)
        {
            eodTitleText.text = $"{GetCurrentDay()}일차 정산";
        }

        if (eodGoldStatisticsText != null)
        {
            int currentGold = GetCurrentGold();
            int eodExpense = GetTotalUpkeep();
            int remainingGold = currentGold - eodExpense;
            string remainingColor = remainingGold >= 0 ? "#3366FF" : "#FF0000";

            eodGoldStatisticsText.text =
                $"{currentGold}G\n\n" + $"{eodExpense}G\n\n" + $"<color={remainingColor}>{remainingGold}G</color>";
        }

        eodPanelRoot.SetActive(true);
    }

    private void OnEodPanelClicked()
    {
        if (eodPanelRoot != null)
        {
            eodPanelRoot.SetActive(false);
        }

        int eodExpense = GetTotalUpkeep();
        int remainingGold = GetCurrentGold() - eodExpense;
        if (remainingGold < 0 || _resourceManager == null || !_resourceManager.TrySpendGold(eodExpense))
        {
            OpenGameOverPanel();
            return;
        }

        AdvanceDayAfterEod();
    }

    private void AdvanceDayAfterEod()
    {
        if (_flow != null)
        {
            _flow.HandleEodRequested();
        }
    }

    private void OpenGameOverPanel()
    {
        if (gameOverPanelRoot != null)
        {
            gameOverPanelRoot.SetActive(true);
        }

        if (gameOverPanel1Root != null)
        {
            gameOverPanel1Root.SetActive(true);
        }

        if (gameOverPanel2Root != null)
        {
            gameOverPanel2Root.SetActive(false);
        }
    }

    private void CloseGameOverPanel()
    {
        if (gameOverPanelRoot != null)
        {
            gameOverPanelRoot.SetActive(false);
        }

        if (gameOverPanel1Root != null)
        {
            gameOverPanel1Root.SetActive(false);
        }

        if (gameOverPanel2Root != null)
        {
            gameOverPanel2Root.SetActive(false);
        }
    }

    private void OnGameOverPanel1Clicked()
    {
        if (gameOverPanel1Root != null)
        {
            gameOverPanel1Root.SetActive(false);
        }

        if (gameOverPanel2Root != null)
        {
            gameOverPanel2Root.SetActive(true);
        }

        if (gameOverPanel2StoryText != null)
        {
            gameOverPanel2StoryText.text = $"제 {GetCurrentDay()}일,\n{GetTeamNameForStory()}은 역사 속으로 사라졌다.";
        }
    }

    private void OnGameOverPanel2BackgroundClicked()
    {
        if (_flow != null)
        {
            _flow.HandleReturnToTitleRequested();
        }
    }

    private void OnAccountbookClicked()
    {
        HideMainMenuTooltip();
        if (accountPanelRoot == null)
        {
            return;
        }

        RefreshAccountPanel();
        accountPanelRoot.SetActive(true);
    }

    private void OnAccountBackClicked()
    {
        if (accountPanelRoot == null)
        {
            return;
        }

        accountPanelRoot.SetActive(false);
    }

    private void OnSaveClicked()
    {
        // 저장 버튼 클릭 시 최신 슬롯 프리뷰를 다시 그린 뒤 모달을 연다.
        if (savePanelRoot == null)
        {
            return;
        }

        RefreshSaveSlotPreviews();
        savePanelRoot.SetActive(true);
    }

    private void OnContinueClicked()
    {
        CloseEscapeMenu();
    }

    private void OnSettingsClicked()
    {
        if (settingsPanelRoot == null)
        {
            return;
        }

        SyncSettingsControlsFromGlobalValues();
        settingsPanelRoot.SetActive(true);
    }

    private void OnCloseSettingsClicked()
    {
        if (settingsPanelRoot == null)
        {
            return;
        }

        settingsPanelRoot.SetActive(false);
    }

    private void OnCloseSaveClicked()
    {
        // 닫기 버튼/배경 클릭 공통: 저장 모달만 닫는다.
        if (savePanelRoot == null)
        {
            return;
        }

        savePanelRoot.SetActive(false);
    }

    private void OnSaveSlotClicked(int slotIndex)
    {
        if (verboseLog)
        {
            Debug.Log($"[MainUIManager] Slot{slotIndex} clicked.", this);
        }

        if (_flow == null)
        {
            return;
        }

        _flow.HandleSaveToSlotRequested(slotIndex);
        RefreshSaveSlotPreviews();
    }

    public void RefreshSaveSlotPreviews()
    {
        // 슬롯 1~5를 순회하며 저장 유무/날짜/골드 표시 텍스트를 동기화한다.
        if (saveSlotTexts == null)
        {
            return;
        }

        for (int i = 0; i < saveSlotTexts.Length; i++)
        {
            TMP_Text slotText = saveSlotTexts[i];
            if (slotText == null)
            {
                continue;
            }

            int slotIndex = i + 1;
            slotText.text = BuildSlotPreviewText(slotIndex);
        }
    }

    private void CacheSaveModalControls()
    {
        // 인스펙터 미할당 상황을 대비해 모달/닫기/슬롯 참조를 씬에서 캐싱한다.
        if (savePanelRoot == null)
        {
            savePanelRoot = ResolveSavePanelRootFromScene();
        }

        if (savePanelRoot == null)
        {
            return;
        }

        Transform modalRootTransform = savePanelRoot.transform;

        if (saveCloseButton == null)
        {
            saveCloseButton = FindChildComponent<Button>(modalRootTransform, "CloseButton");
        }

        Transform backdropTransform = FindChildTransform(modalRootTransform, "DimBackground");
        if (backdropTransform != null)
        {
            Image backdropImage = backdropTransform.GetComponent<Image>();
            _saveBackdropButton = backdropTransform.GetComponent<Button>();

            if (_saveBackdropButton == null)
            {
                _saveBackdropButton = backdropTransform.gameObject.AddComponent<Button>();
            }

            _saveBackdropButton.transition = Selectable.Transition.None;
            _saveBackdropButton.targetGraphic = backdropImage;
        }

        if (saveSlotButtons == null || saveSlotButtons.Length != 5)
        {
            saveSlotButtons = new Button[5];
        }

        if (saveSlotTexts == null || saveSlotTexts.Length != 5)
        {
            saveSlotTexts = new TMP_Text[5];
        }

        for (int i = 0; i < 5; i++)
        {
            int slotIndex = i + 1;

            if (saveSlotButtons[i] == null)
            {
                saveSlotButtons[i] = FindSlotButton(modalRootTransform, slotIndex);
            }

            if (saveSlotTexts[i] == null)
            {
                saveSlotTexts[i] = FindChildComponent<TMP_Text>(modalRootTransform, $"Slot{slotIndex}Text");
            }
        }
    }

    private void CacheSettingsModalControls()
    {
        if (settingsPanelRoot == null)
        {
            settingsPanelRoot = FindByNameInScene("SettingsPanel") ?? FindByNameInScene("SettingsModalRoot");
        }

        if (settingsPanelRoot == null)
        {
            return;
        }

        Transform settingsRootTransform = settingsPanelRoot.transform;

        if (settingsCloseButton == null)
        {
            settingsCloseButton = FindChildComponent<Button>(settingsRootTransform, "CloseButton");
        }

        if (languageDropdown == null)
        {
            languageDropdown = FindChildComponent<Dropdown>(settingsRootTransform, "LanguageDropdown");
        }

        if (bgmVolumeSlider == null)
        {
            bgmVolumeSlider = FindChildComponent<Slider>(settingsRootTransform, "BgmSlider");
        }

        if (sfxVolumeSlider == null)
        {
            sfxVolumeSlider = FindChildComponent<Slider>(settingsRootTransform, "SfxSlider");
        }

        if (brightnessSlider == null)
        {
            brightnessSlider = FindChildComponent<Slider>(settingsRootTransform, "BrightnessSlider");
        }

        Transform backdropTransform = FindChildTransform(settingsRootTransform, "DimBackground");
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

    private void BindSettingsModalControls()
    {
        BindButton(settingsCloseButton, OnCloseSettingsClicked);

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

    private void OnLanguageChanged(int selectedIndex)
    {
        GameSettings.SetLanguage((GameLanguage)selectedIndex);
    }

    private void OnBgmVolumeChanged(float value)
    {
        GameSettings.SetBgmVolume(value);
        ApplyAudioSettings();
    }

    private void OnSfxVolumeChanged(float value)
    {
        GameSettings.SetSfxVolume(value);
        ApplyAudioSettings();
    }

    private void OnBrightnessChanged(float value)
    {
        GameSettings.SetBrightness(value);
        GameSettings.ApplyBrightnessToCurrentScene();
    }

    private static void ApplyAudioSettings()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.ApplyFromGlobalSettings();
        }
    }

    private void BindSaveModalControls()
    {
        // 모달 내부 버튼 이벤트를 단일 진입점으로 재바인딩한다.
        BindButton(saveCloseButton, OnCloseSaveClicked);

        if (_saveBackdropButton != null)
        {
            _saveBackdropButton.onClick.RemoveListener(OnCloseSaveClicked);
            _saveBackdropButton.onClick.AddListener(OnCloseSaveClicked);
        }

        if (saveSlotButtons == null)
        {
            return;
        }

        for (int i = 0; i < saveSlotButtons.Length; i++)
        {
            Button slotButton = saveSlotButtons[i];
            if (slotButton == null)
            {
                continue;
            }

            int slotNumber = i + 1;
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => OnSaveSlotClicked(slotNumber));

            if (verboseLog)
            {
                Debug.Log($"[MainUIManager] Bound save slot button: Slot{slotNumber} -> {slotButton.name}", this);
            }
        }
    }

    private void HandleEscapePressed()
    {
        if (gameOverPanelRoot != null && gameOverPanelRoot.activeSelf)
        {
            return;
        }

        if (eodPanelRoot != null && eodPanelRoot.activeSelf)
        {
            eodPanelRoot.SetActive(false);
            return;
        }

        if (accountPanelRoot != null && accountPanelRoot.activeSelf)
        {
            OnAccountBackClicked();
            return;
        }

        if (settingsPanelRoot != null && settingsPanelRoot.activeSelf)
        {
            OnCloseSettingsClicked();
            return;
        }

        if (savePanelRoot != null && savePanelRoot.activeSelf)
        {
            OnCloseSaveClicked();
            return;
        }

        if (escapeMenuRoot != null && escapeMenuRoot.activeSelf)
        {
            CloseEscapeMenu();
            return;
        }

        if (!_mainMenuInteractable)
        {
            return;
        }

        OpenEscapeMenu();
    }

    private void OpenEscapeMenu()
    {
        HideMainMenuTooltip();
        if (escapeMenuRoot != null)
        {
            escapeMenuRoot.SetActive(true);
        }
    }

    private void CloseEscapeMenu()
    {
        if (escapeMenuRoot != null)
        {
            escapeMenuRoot.SetActive(false);
        }
    }

    private void RefreshAccountPanel()
    {
        // 팀 이름은 에디터에서 고정 텍스트로 관리하므로 여기서는 통계 텍스트만 갱신한다.
        if (gladiatorStatisticsText != null)
        {
            gladiatorStatisticsText.text =
                $"총원 : {GetOwnedGladiatorCount()}\n\n"
                + $"치른 전투 수 : {(_sessionManager != null ? _sessionManager.TotalBattleCount : 0)}\n\n"
                + $"승리 전투 수 : {(_sessionManager != null ? _sessionManager.VictoryBattleCount : 0)}\n\n"
                + $"처치 적 수 : {(_sessionManager != null ? _sessionManager.DefeatedEnemyCount : 0)}";
        }

        if (goldStatisticsText != null)
        {
            int currentGold = _resourceManager != null ? _resourceManager.CurrentGold : 0;
            int totalUpkeep = GetTotalUpkeep();
            int expectedExpense = totalUpkeep * Mathf.Max(1, projectedExpenseDays);

            goldStatisticsText.text =
                $"<color=#62461E>보유 골드 : {currentGold} G\n\n"
                + $"유지비 : {totalUpkeep} G</color>\n\n"
                + $"<color=#FF0000>예상 지출 골드 : {expectedExpense} G</color>";
        }
    }

    private int GetOwnedGladiatorCount()
    {
        return _gladiatorManager != null ? _gladiatorManager.GetOwnedGladiatorCount() : 0;
    }

    private int GetCurrentGold()
    {
        return _resourceManager != null ? _resourceManager.CurrentGold : 0;
    }

    private int GetCurrentDay()
    {
        return _sessionManager != null ? _sessionManager.CurrentDay : 1;
    }

    private string GetTeamNameForStory()
    {
        string teamName = teamNameText != null ? teamNameText.text : string.Empty;
        return string.IsNullOrWhiteSpace(teamName) ? "검투사단" : teamName.Trim();
    }

    private int GetTotalUpkeep()
    {
        if (_gladiatorManager == null || _gladiatorManager.OwnedGladiators == null)
        {
            return 0;
        }

        int total = 0;
        IReadOnlyList<OwnedGladiatorData> ownedGladiators = _gladiatorManager.OwnedGladiators;
        for (int i = 0; i < ownedGladiators.Count; i++)
        {
            OwnedGladiatorData gladiator = ownedGladiators[i];
            if (gladiator != null)
            {
                total += Mathf.Max(0, gladiator.Upkeep);
            }
        }

        return total;
    }

    private static string BuildSlotPreviewText(int slotIndex)
    {
        // 세이브 데이터가 없으면 Empty Slot, 있으면 핵심 프리뷰 문자열을 구성한다.
        SaveGameService.SaveSlotPreview preview = SaveGameService.GetSlotPreview(slotIndex);
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

        return $"슬롯 {slotIndex}  |  일차: {preview.day}  |  골드: {preview.gold}  |  저장일시: {savedTimeText}";
    }

    private static Button FindSlotButton(Transform modalRootTransform, int slotIndex)
    {
        string slotButtonName = $"Slot{slotIndex}Button";
        Button button = FindChildComponent<Button>(modalRootTransform, slotButtonName);
        if (button != null)
        {
            return button;
        }

        return FindChildComponent<Button>(modalRootTransform, $"Slot{slotIndex}");
    }

    private GameObject ResolveSavePanelRootFromScene()
    {
        GameObject resolved = FindByNameInScene("SavePanel");
        if (resolved != null)
        {
            return resolved;
        }

        return FindByNameInScene("SaveModalRoot");
    }

    private GameObject FindByNameInScene(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject rootObject = rootObjects[i];
            if (rootObject == null)
            {
                continue;
            }

            Transform found = FindChildTransform(rootObject.transform, objectName);
            if (found != null)
            {
                return found.gameObject;
            }
        }

        return null;
    }

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

    private void RegisterMainMenuTooltips()
    {
        if (mainMenuTooltipEntries == null)
        {
            return;
        }

        for (int i = 0; i < mainMenuTooltipEntries.Length; i++)
        {
            MainMenuTooltipEntry entry = mainMenuTooltipEntries[i];
            if (entry == null || entry.TargetButton == null)
            {
                continue;
            }

            EventTrigger trigger = entry.TargetButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = entry.TargetButton.gameObject.AddComponent<EventTrigger>();
            }

            MainMenuTooltipEntry capturedEntry = entry;
            AddEventTriggerListener(trigger, EventTriggerType.PointerEnter, _ => ShowMainMenuTooltip(capturedEntry));
            AddEventTriggerListener(trigger, EventTriggerType.PointerExit, _ => HideMainMenuTooltip());
        }
    }

    private void CacheMainMenuTooltipReferences()
    {
        if (mainMenuTooltipRoot == null)
        {
            return;
        }

        _mainMenuTooltipRectTransform = mainMenuTooltipRoot.GetComponent<RectTransform>();
        _mainMenuTooltipParentRectTransform = mainMenuTooltipRoot.transform.parent as RectTransform;
        _mainMenuTooltipCanvas = mainMenuTooltipRoot.GetComponentInParent<Canvas>();
    }

    private void ShowMainMenuTooltip(MainMenuTooltipEntry entry)
    {
        if (!_mainMenuInteractable || entry == null || mainMenuTooltipRoot == null)
        {
            return;
        }

        if (mainMenuTooltipTitleText != null)
        {
            mainMenuTooltipTitleText.text = entry.Title;
        }

        if (mainMenuTooltipDetailText != null)
        {
            mainMenuTooltipDetailText.text = entry.Detail;
        }

        mainMenuTooltipRoot.SetActive(true);
        UpdateMainMenuTooltipPosition();
    }

    private void HideMainMenuTooltip()
    {
        if (mainMenuTooltipRoot != null)
        {
            mainMenuTooltipRoot.SetActive(false);
        }
    }

    private void UpdateMainMenuTooltipPosition()
    {
        if (
            mainMenuTooltipRoot == null
            || !mainMenuTooltipRoot.activeSelf
            || _mainMenuTooltipRectTransform == null
            || _mainMenuTooltipParentRectTransform == null
            || Mouse.current == null
        )
        {
            return;
        }

        Camera eventCamera =
            _mainMenuTooltipCanvas != null && _mainMenuTooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _mainMenuTooltipCanvas.worldCamera
                : null;
        Vector2 screenPosition = Mouse.current.position.ReadValue() + mainMenuTooltipOffset;
        if (
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _mainMenuTooltipParentRectTransform,
                screenPosition,
                eventCamera,
                out Vector3 worldPosition
            )
        )
        {
            _mainMenuTooltipRectTransform.position = worldPosition;
        }
    }

    private static void AddEventTriggerListener(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> action
    )
    {
        if (trigger == null || action == null)
        {
            return;
        }

        EventTrigger.Entry triggerEntry = new EventTrigger.Entry { eventID = eventType };
        triggerEntry.callback.AddListener(action);
        trigger.triggers.Add(triggerEntry);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetButtonInteractable(Button button, bool value)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = value;
    }
}
