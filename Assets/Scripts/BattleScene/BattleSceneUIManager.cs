using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleScene 전담 UI manager. 전투 종료 패널, 배속 UI 처리.
// 책임:
// - 전투 종료 풀스크린 패널 표시 (승리/패배 텍스트, 보상 gold)
// - confirm 버튼 처리 → MainScene 복귀 시작
// - preset speed 버튼 처리 → BattleSimulationManager.SetSimulationSpeedMultiplier(...)로 조절
// - 현재 선택된 speed 버튼 텍스트 색상 갱신
// ※ 속도는 minSimulationSpeed ~ maxSimulationSpeed 범위로 clamp
[DisallowMultipleComponent]
public sealed class BattleSceneUIManager : MonoBehaviour
{
    [Header("Result Panel")]
    [SerializeField]
    private GameObject battleEndPanelRoot;

    [SerializeField]
    private Button victoryConfirmButton;

    [SerializeField]
    private Button defeatConfirmButton;

    [Header("Victory Panel")]
    [SerializeField]
    private GameObject victoryPanelRoot;

    [SerializeField]
    private RawImage victoryBackgroundImage;

    [SerializeField]
    private RawImage victoryHeaderImage;

    [SerializeField]
    private TMP_Text victoryBattleResultTitleText;

    [SerializeField]
    private TMP_Text victoryRewardText;

    [SerializeField]
    private Button victoryBackToMainButton;

    [Header("Defeat Panel")]
    [SerializeField]
    private GameObject defeatPanelRoot;

    [SerializeField]
    private RawImage defeatBackgroundImage;

    [SerializeField]
    private RawImage defeatHeaderImage;

    [SerializeField]
    private TMP_Text defeatBattleResultTitleText;

    [SerializeField]
    private TMP_Text defeatRewardText;

    [SerializeField]
    private Button defeatBackToMainButton;

    [Header("Speed UI")]
    [SerializeField]
    private BattleSimulationManager battleSimulationManager;

    [SerializeField]
    private Button speed025Button;

    [SerializeField]
    private Button speed05Button;

    [SerializeField]
    private Button speed1Button;

    [SerializeField]
    private Button speed2Button;

    [SerializeField]
    private Button speed3Button;

    [Header("Header Panel")]
    [SerializeField]
    private RawImage headerBackgroundImage;

    [SerializeField]
    private TMP_Text enemySquadNameText;

    [SerializeField]
    private TMP_Text allySquadNameText;

    [SerializeField]
    private TMP_Text enemyAliveCountText;

    [SerializeField]
    private TMP_Text allyAliveCountText;

    [SerializeField]
    private TMP_Text clockText;

    [SerializeField]
    private TMP_Text dayDifficultyText;

    [Header("Surrender UI")]
    [SerializeField]
    private Button surrenderButton;

    [SerializeField]
    private GameObject surrenderMaskRoot;

    [SerializeField]
    private GameObject surrenderPanelRoot;

    [SerializeField]
    private Button surrenderYesButton;

    [SerializeField]
    private Button surrenderNoButton;

    [Header("Orders UI")]
    [SerializeField]
    private Button ordersButton;

    [SerializeField]
    private GameObject ordersPanelRoot;

    [SerializeField]
    private GameObject orderChatBackgroundRoot;

    [SerializeField]
    private TMP_InputField currentOrderInputField;

    [SerializeField]
    private GameObject[] allyOrderResponsePanelRoots = new GameObject[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] allyOrderResponseTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [Header("Orders Routing")]
    [SerializeField]
    private BattleOrdersManager battleOrdersManager;

    [Header("Command Mode")]
    [SerializeField]
    [Min(0.01f)]
    private float orderInputFixedSpeedMultiplier = 0.1f;

    [SerializeField]
    [Min(0f)]
    private float allyResponseVisibleSeconds = 6f;

    [Header("Scene Navigation")]
    [SerializeField]
    private string mainSceneName = "MainScene";

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private enum ModalState
    {
        None,
        Surrender,
        Orders,
    }

    private enum OrderTargetMode
    {
        None,
        Global,
    }

    private SceneLoader _sceneLoader;
    private BattleSimulationManager _subscribedSimulationManager;
    private BattleOrdersManager _subscribedBattleOrdersManager;
    private bool _initialized;
    private bool _isNavigating;

    private ModalState _activeModalState = ModalState.None;
    private OrderTargetMode _currentOrderTargetMode = OrderTargetMode.Global;
    private float _cachedOrderInputSpeedMultiplier = 1f;
    private bool _hasCachedOrderInputSpeedMultiplier;
    private bool _isOrderInputSpeedApplied;
    private BattleStartPayload _headerPayload;
    private IReadOnlyList<BattleRuntimeUnit> _headerRuntimeUnits;
    private readonly Coroutine[] _allyResponseHideCoroutines = new Coroutine[BattleTeamConstants.MaxUnitsPerTeam];
    private BattleResolution _lastResolution;
    private bool _hasLastResolution;

    public bool IsBattleEndPanelOpen =>
        (battleEndPanelRoot != null && battleEndPanelRoot.activeSelf)
        || (victoryPanelRoot != null && victoryPanelRoot.activeSelf)
        || (defeatPanelRoot != null && defeatPanelRoot.activeSelf);
    public bool IsSurrenderPanelOpen => surrenderPanelRoot != null && surrenderPanelRoot.activeSelf;
    public bool IsOrdersPanelOpen => orderChatBackgroundRoot == null || orderChatBackgroundRoot.activeSelf;

    public void Initialize()
    {
        if (_initialized)
        {
            RefreshSpeedText();
            RefreshButtonStates();
            return;
        }

        _sceneLoader = SceneLoader.Instance;
        EnsureBattleSimulationManager();

        BindButton(victoryConfirmButton, OnVictoryConfirmClicked);
        BindButton(defeatConfirmButton, OnDefeatConfirmClicked);
        BindButton(victoryBackToMainButton, ReturnToMainScene);
        BindButton(defeatBackToMainButton, ReturnToMainScene);
        BindSpeedPresetButtons();

        BindButton(surrenderButton, OnSurrenderClicked);
        BindButton(surrenderYesButton, OnSurrenderYesClicked);
        BindButton(surrenderNoButton, OnSurrenderNoClicked);

        BindButton(ordersButton, OnOrdersClicked);
        BindCurrentOrderInputField();
        EnsureBattleOrdersManager();
        RebindBattleOrdersManagerEvents();

        HideAll();
        ClearAllyOrderResponses();
        RefreshSpeedText();
        RefreshButtonStates();

        _initialized = true;
    }

    private void Update()
    {
        RefreshHeader();
    }

    private void OnDestroy()
    {
        if (currentOrderInputField != null)
        {
            currentOrderInputField.onSubmit.RemoveListener(OnOrderInputSubmitted);
            currentOrderInputField.onSelect.RemoveListener(HandleOrderInputSelected);
            currentOrderInputField.onDeselect.RemoveListener(HandleOrderInputDeselected);
        }

        UnbindBattleOrdersManagerEvents();
        UnbindSimulationEvents();
    }

    public void ShowBattleEndPanel(BattleResolution resolution)
    {
        if (!_initialized)
        {
            Initialize();
        }

        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: false);

        _lastResolution = resolution;
        _hasLastResolution = true;

        SetActive(battleEndPanelRoot, true);
        SetActive(victoryPanelRoot, false);
        SetActive(defeatPanelRoot, false);
        SetComponentActive(victoryConfirmButton, resolution.WasWin);
        SetComponentActive(defeatConfirmButton, !resolution.WasWin);
        SetButtonInteractable(victoryConfirmButton, resolution.WasWin);
        SetButtonInteractable(defeatConfirmButton, !resolution.WasWin);
        SetButtonInteractable(victoryBackToMainButton, true);
        SetButtonInteractable(defeatBackToMainButton, true);
        RefreshResultDetailTexts(resolution);

        RefreshSpeedText();
        RefreshButtonStates();

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneUIManager] Battle end panel opened. WasWin={resolution.WasWin}, Reward={resolution.PendingReward}",
                this
            );
        }
    }

    public void HideAll()
    {
        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: true);
        _hasLastResolution = false;
        SetActive(battleEndPanelRoot, false);
        SetActive(victoryPanelRoot, false);
        SetActive(defeatPanelRoot, false);
        EnsureOrderInputVisible();
        SetGlobalOrderTarget();
        SetButtonInteractable(victoryConfirmButton, true);
        SetButtonInteractable(defeatConfirmButton, true);
        SetButtonInteractable(victoryBackToMainButton, true);
        SetButtonInteractable(defeatBackToMainButton, true);

        RefreshSpeedText();
        RefreshButtonStates();
        RefreshHeader();
    }

    public void ConfigureHeader(BattleStartPayload payload, IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        _headerPayload = payload;
        _headerRuntimeUnits = runtimeUnits;
        RefreshHeaderStaticTexts();
        RefreshHeader();
    }

    public void RefreshHeader()
    {
        if (_headerPayload == null && battleSimulationManager != null)
        {
            _headerPayload = battleSimulationManager.InitialPayload;
        }

        if (_headerRuntimeUnits == null && battleSimulationManager != null)
        {
            _headerRuntimeUnits = battleSimulationManager.RuntimeUnits;
        }

        RefreshHeaderStaticTexts();
        RefreshAliveCounts();
        RefreshClockText();
    }

    public void RefreshSpeedText()
    {
        EnsureBattleSimulationManager();
        RefreshSpeedButtonTextColors();
    }

    private void BindSpeedPresetButtons()
    {
        Button[] buttons = GetSpeedPresetButtons();
        float[] speedValues = GetSpeedPresetValues();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            button.onClick.RemoveAllListeners();
            float speed = speedValues[i];
            button.onClick.AddListener(() => OnSpeedPresetClicked(speed));
        }
    }

    private void OnSpeedPresetClicked(float speedMultiplier)
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed preset blocked. BattleSimulationManager not found.", this);
            return;
        }

        if (_activeModalState != ModalState.None || battleSimulationManager.IsTemporarilyPaused)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed preset blocked. Modal UI is active.", this);
            return;
        }

        battleSimulationManager.SetSimulationSpeedMultiplier(speedMultiplier);
        RefreshSpeedText();
        RefreshButtonStates();
    }

    private void RefreshSpeedButtonTextColors()
    {
        float selectedSpeed = battleSimulationManager != null ? battleSimulationManager.SimulationSpeedMultiplier : -1f;
        Button[] buttons = GetSpeedPresetButtons();
        float[] speedValues = GetSpeedPresetValues();

        for (int i = 0; i < buttons.Length; i++)
        {
            TMP_Text buttonText = buttons[i] != null ? buttons[i].GetComponentInChildren<TMP_Text>(true) : null;
            if (buttonText != null)
            {
                buttonText.color = Mathf.Approximately(selectedSpeed, speedValues[i]) ? Color.red : Color.white;
            }
        }
    }

    private void OnSurrenderClicked()
    {
        EnsureBattleSimulationManager();

        if (!CanUseBattleUiAction("Surrender"))
        {
            return;
        }

        if (_activeModalState != ModalState.None)
        {
            Debug.LogWarning("[BattleSceneUIManager] Surrender blocked. Another modal UI is already open.", this);
            return;
        }

        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: false);
        DisableEntireTeam(disableEnemies: false);

        if (verboseLog)
        {
            Debug.Log("[BattleSceneUIManager] Surrender applied directly.", this);
        }
    }

    private void OnSurrenderYesClicked()
    {
        EnsureBattleSimulationManager();

        if (!CanUseBattleUiAction("Surrender Yes"))
        {
            return;
        }

        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: false);
        DisableEntireTeam(disableEnemies: false);
    }

    private void OnSurrenderNoClicked()
    {
        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: false);

        if (verboseLog)
        {
            Debug.Log("[BattleSceneUIManager] Surrender canceled.", this);
        }
    }

    private void OnOrdersClicked()
    {
        OnOrderInputSubmitted(currentOrderInputField != null ? currentOrderInputField.text : string.Empty);
    }

    public void OpenGlobalOrderPanel()
    {
        SetGlobalOrderTarget();
        FocusCurrentOrderInput();
    }

    public void OpenSingleOrderPanel(BattleRuntimeUnit targetUnit)
    {
        SetGlobalOrderTarget();
        FocusCurrentOrderInput();

        if (verboseLog)
        {
            Debug.Log(
                "[BattleSceneUIManager] Single order is deprecated. Opening global order input instead.",
                this
            );
        }
    }

    private void SetGlobalOrderTarget()
    {
        _currentOrderTargetMode = OrderTargetMode.Global;
        EnsureOrderInputVisible();
    }

    private void EnsureOrderInputVisible()
    {
        SetActive(ordersPanelRoot, true);
        SetActive(orderChatBackgroundRoot, true);
    }

    private void FocusCurrentOrderInput()
    {
        if (currentOrderInputField != null)
        {
            currentOrderInputField.ActivateInputField();
            currentOrderInputField.Select();
        }
    }

    private void OnOrderInputSubmitted(string rawInput)
    {
        if (!CanUseBattleUiAction("Orders"))
        {
            return;
        }

        EnsureOrderInputVisible();
        SetGlobalOrderTarget();
        EnsureBattleOrdersManager();

        if (battleOrdersManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Order send blocked. BattleOrdersManager not found.", this);
            return;
        }

        string sanitizedInput = SanitizeOrderInput(rawInput);
        if (string.IsNullOrWhiteSpace(sanitizedInput))
        {
            ClearAndRefocusCurrentOrderInput();
            return;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneUIManager] CurrentOrderText submitted. Mode={_currentOrderTargetMode}, Text=\"{sanitizedInput}\"",
                this
            );
        }

        battleOrdersManager.SubmitGlobalOrder(sanitizedInput);

        ClearAndRefocusCurrentOrderInput();
    }

    private void OnVictoryConfirmClicked()
    {
        if (!_hasLastResolution)
        {
            return;
        }

        SetActive(battleEndPanelRoot, false);
        SetActive(victoryPanelRoot, true);
        SetActive(defeatPanelRoot, false);
        RefreshResultDetailTexts(_lastResolution);
        RefreshButtonStates();
    }

    private void OnDefeatConfirmClicked()
    {
        if (!_hasLastResolution)
        {
            return;
        }

        SetActive(battleEndPanelRoot, false);
        SetActive(victoryPanelRoot, false);
        SetActive(defeatPanelRoot, true);
        RefreshResultDetailTexts(_lastResolution);
        RefreshButtonStates();
    }

    private void RefreshResultDetailTexts(BattleResolution resolution)
    {
        SetText(victoryRewardText, $"Reward : {resolution.PendingReward} Gold");
        SetText(defeatRewardText, "Reward : 0 Gold");
        SetText(victoryBattleResultTitleText, "Victory");
        SetText(defeatBattleResultTitleText, "Defeat");
    }

    private void ReturnToMainScene()
    {
        if (_isNavigating)
        {
            return;
        }

        if (_sceneLoader == null)
        {
            _sceneLoader = SceneLoader.Instance;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError("[BattleSceneUIManager] SceneLoader.Instance is null.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogError("[BattleSceneUIManager] mainSceneName is empty.", this);
            return;
        }

        _isNavigating = true;

        SetButtonInteractable(victoryConfirmButton, false);
        SetButtonInteractable(defeatConfirmButton, false);
        SetButtonInteractable(victoryBackToMainButton, false);
        SetButtonInteractable(defeatBackToMainButton, false);

        RefreshButtonStates();

        bool started = _sceneLoader.TryLoadMainScene(mainSceneName);

        if (!started)
        {
            _isNavigating = false;
            SetButtonInteractable(victoryConfirmButton, true);
            SetButtonInteractable(defeatConfirmButton, true);
            SetButtonInteractable(victoryBackToMainButton, true);
            SetButtonInteractable(defeatBackToMainButton, true);

            RefreshButtonStates();
            Debug.LogWarning("[BattleSceneUIManager] Failed to start MainScene load.", this);
        }
    }

    private void RefreshHeaderStaticTexts()
    {
        if (_headerPayload == null)
        {
            SetText(enemySquadNameText, string.Empty);
            SetText(allySquadNameText, string.Empty);
            SetText(dayDifficultyText, string.Empty);
            return;
        }

        SetText(enemySquadNameText, "Enemy Squad");
        SetText(allySquadNameText, $"Squad {_headerPayload.PlayerSquadTeamIndex + 1}");
        SetText(
            dayDifficultyText,
            $"Day {_headerPayload.CurrentDay} {GetDifficultyHeaderText(_headerPayload.Difficulty)}"
        );
    }

    private void RefreshAliveCounts()
    {
        int allyAliveCount = 0;
        int enemyAliveCount = 0;
        IReadOnlyList<BattleRuntimeUnit> units = ResolveHeaderRuntimeUnits();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                BattleRuntimeUnit unit = units[i];
                if (unit == null || unit.IsCombatDisabled)
                {
                    continue;
                }

                if (unit.IsPlayerOwned)
                {
                    allyAliveCount++;
                }
                else
                {
                    enemyAliveCount++;
                }
            }
        }

        SetText(allyAliveCountText, allyAliveCount.ToString());
        SetText(enemyAliveCountText, enemyAliveCount.ToString());
    }

    private void RefreshClockText()
    {
        EnsureBattleSimulationManager();

        float elapsedSeconds =
            battleSimulationManager != null
                ? battleSimulationManager.BattleTickCount * battleSimulationManager.TickInterval
                : 0f;
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(elapsedSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        SetText(clockText, $"{minutes:00}:{seconds:00}");
    }

    private IReadOnlyList<BattleRuntimeUnit> ResolveHeaderRuntimeUnits()
    {
        if (_headerRuntimeUnits != null)
        {
            return _headerRuntimeUnits;
        }

        EnsureBattleSimulationManager();
        _headerRuntimeUnits = battleSimulationManager != null ? battleSimulationManager.RuntimeUnits : null;
        return _headerRuntimeUnits;
    }

    private static string GetDifficultyHeaderText(BattleEncounterDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BattleEncounterDifficulty.VeryLow:
                return "ROOKIE";
            case BattleEncounterDifficulty.Low:
                return "EASY";
            case BattleEncounterDifficulty.Medium:
                return "NORMAL";
            case BattleEncounterDifficulty.High:
                return "HARD";
            default:
                return difficulty.ToString().ToUpperInvariant();
        }
    }

    private void DisableEntireTeam(bool disableEnemies)
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Team disable skipped. BattleSimulationManager not found.", this);
            return;
        }

        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = battleSimulationManager.RuntimeUnits;
        if (runtimeUnits == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Team disable skipped. RuntimeUnits is null.", this);
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

            if (unit.IsPlayerOwned == disableEnemies)
            {
                continue;
            }

            unit.ApplyDamage(unit.CurrentHealth + unit.MaxHealth + 999999f);
            affectedCount++;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneUIManager] Surrender applied. Team={(disableEnemies ? "Enemy" : "Ally")}, Affected={affectedCount}",
                this
            );
        }
    }

    private bool CanUseBattleUiAction(string actionName)
    {
        if (IsBattleEndPanelOpen)
        {
            Debug.LogWarning($"[BattleSceneUIManager] {actionName} blocked. Battle end panel is open.", this);
            return false;
        }

        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            Debug.LogWarning($"[BattleSceneUIManager] {actionName} blocked. BattleSimulationManager not found.", this);
            return false;
        }

        if (battleSimulationManager.IsBattleFinished)
        {
            Debug.LogWarning($"[BattleSceneUIManager] {actionName} blocked. Battle is already finished.", this);
            return false;
        }

        return true;
    }

    private void CloseTransientUi(bool restoreOrderSpeed, bool clearOrderInput)
    {
        EnsureBattleSimulationManager();

        if (_activeModalState == ModalState.Surrender && battleSimulationManager != null)
        {
            battleSimulationManager.SetTemporaryPause(false);
        }

        RestoreOrderInputSpeedIfNeeded();

        _activeModalState = ModalState.None;
        SetGlobalOrderTarget();

        SetActive(surrenderMaskRoot, false);
        SetActive(surrenderPanelRoot, false);

        if (clearOrderInput)
        {
            ClearCurrentOrderInput();
        }

        RefreshSpeedText();
        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        EnsureBattleSimulationManager();

        bool modalOpen = _activeModalState != ModalState.None;
        bool paused = battleSimulationManager != null && battleSimulationManager.IsTemporarilyPaused;
        bool blockSpeedButtons = modalOpen || paused || IsBattleEndPanelOpen || _isNavigating;
        bool blockCommandButtons = modalOpen || IsBattleEndPanelOpen || _isNavigating;
        bool blockOrderInput = modalOpen || IsBattleEndPanelOpen || _isNavigating;

        Button[] speedButtons = GetSpeedPresetButtons();
        for (int i = 0; i < speedButtons.Length; i++)
        {
            if (speedButtons[i] != null)
            {
                speedButtons[i].interactable = !blockSpeedButtons;
            }
        }

        if (surrenderButton != null)
        {
            surrenderButton.interactable = !blockCommandButtons;
        }

        if (ordersButton != null)
        {
            ordersButton.interactable = !blockOrderInput;
        }

        if (currentOrderInputField != null)
        {
            currentOrderInputField.interactable = !blockOrderInput;
        }

        if (surrenderYesButton != null)
            surrenderYesButton.interactable = _activeModalState == ModalState.Surrender;

        if (surrenderNoButton != null)
            surrenderNoButton.interactable = _activeModalState == ModalState.Surrender;

        RefreshSpeedButtonTextColors();
    }

    private void EnsureBattleSimulationManager()
    {
        if (battleSimulationManager == null)
        {
            battleSimulationManager = FindFirstObjectByType<BattleSimulationManager>();
        }

        RebindSimulationEvents();
    }

    private void RebindSimulationEvents()
    {
        if (_subscribedSimulationManager == battleSimulationManager)
            return;

        UnbindSimulationEvents();
        _subscribedSimulationManager = battleSimulationManager;

        if (_subscribedSimulationManager == null)
            return;

        _subscribedSimulationManager.OnBattleFinished += HandleBattleFinished;
    }

    private void UnbindSimulationEvents()
    {
        if (_subscribedSimulationManager == null)
            return;

        _subscribedSimulationManager.OnBattleFinished -= HandleBattleFinished;
        _subscribedSimulationManager = null;
    }

    private void HandleBattleFinished(BattleOutcome outcome)
    {
        ShowBattleEndPanel(outcome.Resolution);
    }

    private void EnsureBattleOrdersManager()
    {
        if (battleOrdersManager == null)
        {
            battleOrdersManager = FindFirstObjectByType<BattleOrdersManager>();
        }

        RebindBattleOrdersManagerEvents();
    }

    private void BindCurrentOrderInputField()
    {
        if (currentOrderInputField == null)
        {
            return;
        }

        currentOrderInputField.lineType = TMP_InputField.LineType.SingleLine;
        currentOrderInputField.onSubmit.RemoveAllListeners();
        currentOrderInputField.onSubmit.AddListener(OnOrderInputSubmitted);
        currentOrderInputField.onSelect.RemoveListener(HandleOrderInputSelected);
        currentOrderInputField.onSelect.AddListener(HandleOrderInputSelected);
        currentOrderInputField.onDeselect.RemoveListener(HandleOrderInputDeselected);
        currentOrderInputField.onDeselect.AddListener(HandleOrderInputDeselected);
    }

    private void HandleOrderInputSelected(string _)
    {
        ApplyOrderInputSpeedIfNeeded();
    }

    private void HandleOrderInputDeselected(string _)
    {
        RestoreOrderInputSpeedIfNeeded();
    }

    private void ApplyOrderInputSpeedIfNeeded()
    {
        EnsureBattleSimulationManager();

        if (
            _isOrderInputSpeedApplied
            || battleSimulationManager == null
            || IsBattleEndPanelOpen
            || battleSimulationManager.IsBattleFinished
        )
        {
            return;
        }

        _cachedOrderInputSpeedMultiplier = battleSimulationManager.SimulationSpeedMultiplier;
        _hasCachedOrderInputSpeedMultiplier = true;
        _isOrderInputSpeedApplied = true;
        battleSimulationManager.SetSimulationSpeedMultiplier(orderInputFixedSpeedMultiplier);
        RefreshSpeedText();
    }

    private void RestoreOrderInputSpeedIfNeeded()
    {
        if (!_isOrderInputSpeedApplied)
        {
            return;
        }

        EnsureBattleSimulationManager();

        if (battleSimulationManager != null)
        {
            float restoreSpeed = _hasCachedOrderInputSpeedMultiplier ? _cachedOrderInputSpeedMultiplier : 1f;
            battleSimulationManager.SetSimulationSpeedMultiplier(restoreSpeed);
        }

        _isOrderInputSpeedApplied = false;
        _hasCachedOrderInputSpeedMultiplier = false;
        RefreshSpeedText();
    }

    private void ClearAndRefocusCurrentOrderInput()
    {
        if (currentOrderInputField == null)
        {
            return;
        }

        ClearCurrentOrderInput();
        currentOrderInputField.ActivateInputField();
        currentOrderInputField.Select();
    }

    private void ClearCurrentOrderInput()
    {
        if (currentOrderInputField == null)
        {
            return;
        }

        currentOrderInputField.SetTextWithoutNotify(string.Empty);
        currentOrderInputField.ForceLabelUpdate();
    }

    private void RebindBattleOrdersManagerEvents()
    {
        if (_subscribedBattleOrdersManager == battleOrdersManager)
        {
            return;
        }

        UnbindBattleOrdersManagerEvents();
        _subscribedBattleOrdersManager = battleOrdersManager;

        if (_subscribedBattleOrdersManager == null)
        {
            return;
        }

        _subscribedBattleOrdersManager.OnAllyOrderResponseReceived += HandleAllyOrderResponseReceived;
    }

    private void UnbindBattleOrdersManagerEvents()
    {
        if (_subscribedBattleOrdersManager == null)
        {
            return;
        }

        _subscribedBattleOrdersManager.OnAllyOrderResponseReceived -= HandleAllyOrderResponseReceived;
        _subscribedBattleOrdersManager = null;
    }

    private void HandleAllyOrderResponseReceived(BattleRuntimeUnit allyUnit, string responseText)
    {
        if (!TryGetAllyResponseIndex(allyUnit, out int allyIndex))
        {
            Debug.LogWarning(
                $"[BattleSceneUIManager] Ally response ignored. Could not resolve ally panel. Ally={allyUnit?.DisplayName ?? "(null)"}",
                this
            );
            return;
        }

        if (!TryGetAllyResponseText(allyIndex, out TMP_Text targetText))
        {
            Debug.LogWarning(
                $"[BattleSceneUIManager] Ally response ignored. AllyResponseText {allyIndex + 1} is not assigned.",
                this
            );
            return;
        }

        targetText.text = responseText ?? string.Empty;
        SetAllyResponseVisible(allyIndex, true);

        if (_allyResponseHideCoroutines[allyIndex] != null)
        {
            StopCoroutine(_allyResponseHideCoroutines[allyIndex]);
        }

        _allyResponseHideCoroutines[allyIndex] = StartCoroutine(HideAllyResponseAfterDelay(allyIndex));
    }

    private IEnumerator HideAllyResponseAfterDelay(int allyIndex)
    {
        yield return new WaitForSeconds(allyResponseVisibleSeconds);
        SetAllyResponseVisible(allyIndex, false);
        _allyResponseHideCoroutines[allyIndex] = null;
    }

    private void ClearAllyOrderResponses()
    {
        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            if (_allyResponseHideCoroutines[i] != null)
            {
                StopCoroutine(_allyResponseHideCoroutines[i]);
                _allyResponseHideCoroutines[i] = null;
            }

            SetAllyResponseVisible(i, false);
        }
    }

    private bool TryGetAllyResponseIndex(BattleRuntimeUnit allyUnit, out int allyIndex)
    {
        allyIndex = -1;

        if (allyUnit == null)
        {
            return false;
        }

        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = ResolveHeaderRuntimeUnits();
        if (runtimeUnits == null)
        {
            return false;
        }

        int playerUnitIndex = 0;

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null || !unit.IsPlayerOwned)
            {
                continue;
            }

            if (ReferenceEquals(unit, allyUnit))
            {
                if (playerUnitIndex < 0 || playerUnitIndex >= BattleTeamConstants.MaxUnitsPerTeam)
                {
                    allyIndex = -1;
                    return false;
                }

                allyIndex = playerUnitIndex;
                return true;
            }

            playerUnitIndex++;
        }

        return false;
    }

    private bool TryGetAllyResponseText(int allyIndex, out TMP_Text responseText)
    {
        responseText = null;

        if (allyOrderResponseTexts == null || allyIndex < 0 || allyIndex >= allyOrderResponseTexts.Length)
        {
            return false;
        }

        responseText = allyOrderResponseTexts[allyIndex];
        return responseText != null;
    }

    private void SetAllyResponseVisible(int allyIndex, bool isVisible)
    {
        if (
            allyOrderResponsePanelRoots == null
            || allyIndex < 0
            || allyIndex >= allyOrderResponsePanelRoots.Length
            || allyOrderResponsePanelRoots[allyIndex] == null
        )
        {
            return;
        }

        allyOrderResponsePanelRoots[allyIndex].SetActive(isVisible);
    }

    private static string SanitizeOrderInput(string rawInput)
    {
        return string.IsNullOrEmpty(rawInput) ? string.Empty : rawInput.Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private Button[] GetSpeedPresetButtons()
    {
        return new[] { speed025Button, speed05Button, speed1Button, speed2Button, speed3Button };
    }

    private static float[] GetSpeedPresetValues()
    {
        return new[] { 0.25f, 0.5f, 1f, 2f, 3f };
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

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }

    private static void SetComponentActive(Component target, bool value)
    {
        if (target != null)
        {
            target.gameObject.SetActive(value);
        }
    }

    private static void SetButtonInteractable(Button button, bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
