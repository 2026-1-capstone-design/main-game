using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MainUIManager : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField]
    private Button gladiatorButton;

    [SerializeField]
    private Button battleButton;

    [SerializeField]
    private Button researchButton;

    [SerializeField]
    private Button missionButton;

    [SerializeField]
    private Button marketButton;

    [SerializeField]
    private Button eodButton;

    [SerializeField]
    private Button saveButton;

    [Header("Save Modal")]
    [SerializeField]
    private GameObject savePanelRoot;

    [SerializeField]
    private Button saveCloseButton;

    [SerializeField]
    private Button[] saveSlotButtons = new Button[5];

    [SerializeField]
    private TMP_Text[] saveSlotTexts = new TMP_Text[5];

    [Header("Optional Labels")]
    [SerializeField]
    private TMP_Text currentDayText;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private MainFlowManager _flow; // 메인 메뉴 버튼 입력을 실제 게임 흐름 처리 함수로 넘김
    private SessionManager _sessionManager;
    private bool _initialized;
    private Button _saveBackdropButton;

    // 메인 버튼들을 모두 MainFlowManager 핸들러에 연결하고,
    // !!DayChanged 이벤트를 구독해!! 날짜 UI를 동기화
    public void Initialize(MainFlowManager flow, SessionManager sessionManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _sessionManager = sessionManager;

        if (saveButton == null)
        {
            saveButton = ResolveSaveButtonFromScene();
        }

        BindButton(gladiatorButton, OnGladiatorClicked);
        BindButton(battleButton, OnBattleClicked);
        BindButton(researchButton, OnResearchClicked);
        BindButton(missionButton, OnMissionClicked);
        BindButton(marketButton, OnMarketClicked);
        BindButton(eodButton, OnEodClicked);
        BindButton(saveButton, OnSaveClicked);

        CacheSaveModalControls();
        BindSaveModalControls();
        RefreshSaveSlotPreviews();

        if (savePanelRoot != null)
        {
            savePanelRoot.SetActive(false);
        }

        if (_sessionManager != null)
        {
            _sessionManager.DayChanged += OnDayChanged;
            RefreshDayText(_sessionManager.CurrentDay);
        }

        if (verboseLog)
        {
            Debug.Log(
                "[MainUIManager] Save UI init: "
                    + $"saveButton={(saveButton != null ? saveButton.name : "null")}, "
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

    private void OnDestroy()
    {
        if (_sessionManager != null)
        {
            _sessionManager.DayChanged -= OnDayChanged;
        }
    }

    public void SetMainMenuInteractable(bool value)
    {
        SetButtonInteractable(gladiatorButton, value);
        SetButtonInteractable(battleButton, value);
        SetButtonInteractable(researchButton, value);
        SetButtonInteractable(missionButton, value);
        SetButtonInteractable(marketButton, value);
        SetButtonInteractable(eodButton, value);
        SetButtonInteractable(saveButton, value);
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
        if (currentDayText == null)
        {
            return;
        }

        currentDayText.text = $"Day {currentDay}";
    }

    private void OnDayChanged(int currentDay)
    {
        RefreshDayText(currentDay);
    }

    private void OnGladiatorClicked()
    {
        if (_flow != null)
        {
            _flow.HandleGladiatorMenuRequested();
        }
    }

    private void OnBattleClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleMenuRequested();
        }
    }

    private void OnResearchClicked()
    {
        if (_flow != null)
        {
            _flow.HandleResearchMenuRequested();
        }
    }

    private void OnMissionClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMissionMenuRequested();
        }
    }

    private void OnMarketClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMarketMenuRequested();
        }
    }

    private void OnEodClicked()
    {
        if (_flow != null)
        {
            Debug.Log("EOd clicked");
            _flow.HandleEodRequested();
        }
    }

    private void OnSaveClicked()
    {
        if (savePanelRoot == null)
        {
            return;
        }

        RefreshSaveSlotPreviews();
        savePanelRoot.SetActive(true);
    }

    private void OnCloseSaveClicked()
    {
        if (savePanelRoot == null)
        {
            return;
        }

        savePanelRoot.SetActive(false);
    }

    private void OnSaveSlotClicked(int slotIndex)
    {
        Debug.Log($"[MainUIManager] Slot{slotIndex} clicked.", this);

        if (_flow == null)
        {
            return;
        }

        _flow.HandleSaveToSlotRequested(slotIndex);
        RefreshSaveSlotPreviews();
    }

    public void RefreshSaveSlotPreviews()
    {
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

    private void BindSaveModalControls()
    {
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

    private static string BuildSlotPreviewText(int slotIndex)
    {
        SaveGameService.SaveSlotPreview preview = SaveGameService.GetSlotPreview(slotIndex);
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

        return $"SLOT {slotIndex}  |  DAY: {preview.day}  |  GOLD: {preview.gold}  |  SAVED: {savedTimeText}";
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

    private Button ResolveSaveButtonFromScene()
    {
        GameObject saveButtonObject = FindByNameInScene("SaveButton");
        if (saveButtonObject == null)
        {
            return null;
        }

        return saveButtonObject.GetComponent<Button>();
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
