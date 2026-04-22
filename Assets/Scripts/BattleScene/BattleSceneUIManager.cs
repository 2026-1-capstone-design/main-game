using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleScene 전담 UI manager. 전투 종료 패널, 배속 UI 처리.
// 책임:
// - 전투 종료 풀스크린 패널 표시 (승리/패배 텍스트, 보상 gold)
// - confirm 버튼 처리 → MainScene 복귀 시작
// - speed up(×2) / speed down(×0.5) 버튼 처리 → BattleSimulationManager.MultiplySimulationSpeed(...)로 조절
// - current speed text 갱신 (결과 패널이 떠 있어도 갱신 가능)
// ※ 속도는 minSimulationSpeed ~ maxSimulationSpeed 범위로 clamp
[DisallowMultipleComponent]
public sealed class BattleSceneUIManager : MonoBehaviour
{
    [Header("Result Panel")]
    [SerializeField]
    private GameObject battleEndPanelRoot;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text resultRewardText;

    [SerializeField]
    private Button confirmButton;

    [Header("Speed UI")]
    [SerializeField]
    private BattleSimulationManager battleSimulationManager;

    [SerializeField]
    private Button speedUpButton;

    [SerializeField]
    private Button speedDownButton;

    [SerializeField]
    private TMP_Text currentSpeedText;

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
    private GameObject ordersMaskRoot;

    [SerializeField]
    private GameObject ordersPanelRoot;

    [SerializeField]
    private TMP_InputField ordersInputField;

    [SerializeField]
    private Button orderSendButton;

    [SerializeField]
    private Button orderBackButton;

    [Header("Orders Routing")]
    [SerializeField]
    private BattleOrdersManager battleOrdersManager;

    [Header("Command Mode")]
    [SerializeField]
    [Min(0.01f)]
    private float orderInputFixedSpeedMultiplier = 0.1f;

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
        SingleAlly,
    }

    private SceneLoader _sceneLoader;
    private BattleSimulationManager _subscribedSimulationManager;
    private bool _initialized;
    private bool _isNavigating;

    private ModalState _activeModalState = ModalState.None;
    private OrderTargetMode _currentOrderTargetMode = OrderTargetMode.None;
    private BattleRuntimeUnit _currentOrderTargetUnit;
    private float _cachedSpeedMultiplier = 1f;
    private bool _hasCachedSpeedMultiplier;

    public bool IsBattleEndPanelOpen => battleEndPanelRoot != null && battleEndPanelRoot.activeSelf;
    public bool IsSurrenderPanelOpen => surrenderPanelRoot != null && surrenderPanelRoot.activeSelf;
    public bool IsOrdersPanelOpen => ordersPanelRoot != null && ordersPanelRoot.activeSelf;

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

        BindButton(confirmButton, OnConfirmClicked);
        BindButton(speedUpButton, OnSpeedUpClicked);
        BindButton(speedDownButton, OnSpeedDownClicked);

        BindButton(surrenderButton, OnSurrenderClicked);
        BindButton(surrenderYesButton, OnSurrenderYesClicked);
        BindButton(surrenderNoButton, OnSurrenderNoClicked);

        BindButton(ordersButton, OnOrdersClicked);
        BindButton(orderSendButton, OnOrderSendClicked);
        BindButton(orderBackButton, OnOrderBackClicked);

        HideAll();
        RefreshSpeedText();
        RefreshButtonStates();

        _initialized = true;
    }

    private void OnDestroy()
    {
        UnbindSimulationEvents();
    }

    public void ShowBattleEndPanel(BattleResolution resolution)
    {
        if (!_initialized)
        {
            Initialize();
        }

        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: false);

        SetActive(battleEndPanelRoot, true);

        if (resultTitleText != null)
        {
            resultTitleText.text = resolution.WasWin ? "Victory" : "Defeat";
        }

        if (resultRewardText != null)
        {
            resultRewardText.text = resolution.WasWin ? $"Reward : {resolution.PendingReward} Gold" : "Reward : 0 Gold";
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }

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
        SetActive(battleEndPanelRoot, false);

        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }

        RefreshSpeedText();
        RefreshButtonStates();
    }

    public void RefreshSpeedText()
    {
        if (currentSpeedText == null)
        {
            return;
        }

        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            currentSpeedText.text = "Speed x0";
            return;
        }

        if (battleSimulationManager.IsTemporarilyPaused)
        {
            currentSpeedText.text = "Paused";
            return;
        }

        float speed = battleSimulationManager.SimulationSpeedMultiplier;
        currentSpeedText.text = $"Speed x{speed:0.##}";
    }

    private void OnSpeedUpClicked()
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed up blocked. BattleSimulationManager not found.", this);
            return;
        }

        if (_activeModalState != ModalState.None || battleSimulationManager.IsTemporarilyPaused)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed up blocked. Modal UI is active.", this);
            return;
        }

        battleSimulationManager.MultiplySimulationSpeed(2f);
        RefreshSpeedText();
        RefreshButtonStates();
    }

    private void OnSpeedDownClicked()
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed down blocked. BattleSimulationManager not found.", this);
            return;
        }

        if (_activeModalState != ModalState.None || battleSimulationManager.IsTemporarilyPaused)
        {
            Debug.LogWarning("[BattleSceneUIManager] Speed down blocked. Modal UI is active.", this);
            return;
        }

        battleSimulationManager.MultiplySimulationSpeed(0.5f);
        RefreshSpeedText();
        RefreshButtonStates();
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

        battleSimulationManager.SetTemporaryPause(true);
        _activeModalState = ModalState.Surrender;

        SetActive(surrenderMaskRoot, true);
        SetActive(surrenderPanelRoot, true);

        RefreshSpeedText();
        RefreshButtonStates();

        if (verboseLog)
        {
            Debug.Log("[BattleSceneUIManager] Surrender confirmation opened.", this);
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
        OpenGlobalOrderPanel();
    }

    public void OpenGlobalOrderPanel()
    {
        TryOpenOrdersPanel(OrderTargetMode.Global, null);
    }

    public void OpenSingleOrderPanel(BattleRuntimeUnit targetUnit)
    {
        if (targetUnit == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Single order open blocked. Target ally is null.", this);
            return;
        }

        if (targetUnit.IsEnemy)
        {
            Debug.LogWarning("[BattleSceneUIManager] Single order open blocked. Target is an enemy unit.", this);
            return;
        }

        if (targetUnit.IsCombatDisabled)
        {
            Debug.LogWarning("[BattleSceneUIManager] Single order open blocked. Target ally is disabled.", this);
            return;
        }

        TryOpenOrdersPanel(OrderTargetMode.SingleAlly, targetUnit);
    }

    private bool TryOpenOrdersPanel(OrderTargetMode targetMode, BattleRuntimeUnit targetUnit)
    {
        EnsureBattleSimulationManager();

        if (!CanUseBattleUiAction("Orders"))
        {
            return false;
        }

        if (_activeModalState != ModalState.None)
        {
            Debug.LogWarning("[BattleSceneUIManager] Orders blocked. Another modal UI is already open.", this);
            return false;
        }

        if (targetMode == OrderTargetMode.None)
        {
            Debug.LogWarning("[BattleSceneUIManager] Orders blocked. Order target mode is None.", this);
            return false;
        }

        CacheCurrentSpeedMultiplier();
        battleSimulationManager.SetSimulationSpeedMultiplier(orderInputFixedSpeedMultiplier);

        _activeModalState = ModalState.Orders;
        _currentOrderTargetMode = targetMode;
        _currentOrderTargetUnit = targetUnit;

        SetActive(ordersMaskRoot, true);
        SetActive(ordersPanelRoot, true);

        if (ordersInputField != null)
        {
            ordersInputField.text = string.Empty;
            ordersInputField.ActivateInputField();
            ordersInputField.Select();
        }

        RefreshSpeedText();
        RefreshButtonStates();

        if (verboseLog)
        {
            string targetText = targetUnit != null ? targetUnit.DisplayName : "All Allies";
            Debug.Log(
                $"[BattleSceneUIManager] Orders panel opened. Mode={targetMode}, Target={targetText}, ForcedSpeed={orderInputFixedSpeedMultiplier:0.##}, CachedPreviousSpeed={_cachedSpeedMultiplier:0.##}",
                this
            );
        }

        return true;
    }

    private void OnOrderSendClicked()
    {
        string rawInput = ordersInputField != null ? ordersInputField.text : string.Empty;

        EnsureBattleOrdersManager();

        if (battleOrdersManager == null)
        {
            Debug.LogWarning("[BattleSceneUIManager] Order send blocked. BattleOrdersManager not found.", this);
            CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: true);
            return;
        }

        switch (_currentOrderTargetMode)
        {
            case OrderTargetMode.Global:
                battleOrdersManager.SubmitGlobalOrder(rawInput);
                break;

            case OrderTargetMode.SingleAlly:
                if (_currentOrderTargetUnit == null)
                {
                    Debug.LogWarning("[BattleSceneUIManager] Order send blocked. Target ally is null.", this);
                }
                else
                {
                    battleOrdersManager.SubmitSingleOrder(_currentOrderTargetUnit, rawInput);
                }
                break;

            default:
                Debug.LogWarning("[BattleSceneUIManager] Order send blocked. Order target mode is None.", this);
                break;
        }

        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: true);
    }

    private void OnOrderBackClicked()
    {
        CloseTransientUi(restoreOrderSpeed: true, clearOrderInput: true);

        if (verboseLog)
        {
            Debug.Log("[BattleSceneUIManager] Orders canceled.", this);
        }
    }

    private void OnConfirmClicked()
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

        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }

        RefreshButtonStates();

        bool started = _sceneLoader.TryLoadMainScene(mainSceneName);

        if (!started)
        {
            _isNavigating = false;

            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }

            RefreshButtonStates();
            Debug.LogWarning("[BattleSceneUIManager] Failed to start MainScene load.", this);
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

        if (_activeModalState == ModalState.Orders && battleSimulationManager != null && restoreOrderSpeed)
        {
            RestoreCachedSpeedMultiplier();
        }

        _activeModalState = ModalState.None;
        _currentOrderTargetMode = OrderTargetMode.None;
        _currentOrderTargetUnit = null;

        SetActive(surrenderMaskRoot, false);
        SetActive(surrenderPanelRoot, false);

        SetActive(ordersMaskRoot, false);
        SetActive(ordersPanelRoot, false);

        if (clearOrderInput && ordersInputField != null)
        {
            ordersInputField.text = string.Empty;
        }

        RefreshSpeedText();
        RefreshButtonStates();
    }

    private void CacheCurrentSpeedMultiplier()
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            _cachedSpeedMultiplier = 1f;
            _hasCachedSpeedMultiplier = false;
            return;
        }

        _cachedSpeedMultiplier = battleSimulationManager.SimulationSpeedMultiplier;
        _hasCachedSpeedMultiplier = true;
    }

    private void RestoreCachedSpeedMultiplier()
    {
        EnsureBattleSimulationManager();

        if (battleSimulationManager == null)
        {
            _hasCachedSpeedMultiplier = false;
            return;
        }

        float restoreValue = _hasCachedSpeedMultiplier ? _cachedSpeedMultiplier : 1f;
        battleSimulationManager.SetSimulationSpeedMultiplier(restoreValue);
        _hasCachedSpeedMultiplier = false;
    }

    private void RefreshButtonStates()
    {
        EnsureBattleSimulationManager();

        bool modalOpen = _activeModalState != ModalState.None;
        bool paused = battleSimulationManager != null && battleSimulationManager.IsTemporarilyPaused;
        bool blockSpeedButtons = modalOpen || paused || IsBattleEndPanelOpen || _isNavigating;
        bool blockCommandButtons = modalOpen || IsBattleEndPanelOpen || _isNavigating;

        if (speedUpButton != null)
        {
            speedUpButton.interactable = !blockSpeedButtons;
        }

        if (speedDownButton != null)
        {
            speedDownButton.interactable = !blockSpeedButtons;
        }

        if (surrenderButton != null)
        {
            surrenderButton.interactable = !blockCommandButtons;
        }

        if (ordersButton != null)
        {
            ordersButton.interactable = !blockCommandButtons;
        }

        if (surrenderYesButton != null)
        {
            surrenderYesButton.interactable = _activeModalState == ModalState.Surrender;
        }

        if (surrenderNoButton != null)
        {
            surrenderNoButton.interactable = _activeModalState == ModalState.Surrender;
        }

        if (orderSendButton != null)
        {
            orderSendButton.interactable = _activeModalState == ModalState.Orders;
        }

        if (orderBackButton != null)
        {
            orderBackButton.interactable = _activeModalState == ModalState.Orders;
        }
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
}
