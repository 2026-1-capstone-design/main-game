using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleUIManager : MonoBehaviour
{
    [Header("Battle Preparation Panel")]
    [SerializeField] private GameObject battlePanelRoot;
    [SerializeField] private TMP_Text battleBodyText;

    [Header("Very Low Row")]
    [SerializeField] private Image[] veryLowEnemyImages = new Image[6];
    [SerializeField] private TMP_Text veryLowSummaryText;
    [SerializeField] private Button veryLowRowButton;
    [SerializeField] private GameObject veryLowSelectedOverlay;

    [Header("Low Row")]
    [SerializeField] private Image[] lowEnemyImages = new Image[6];
    [SerializeField] private TMP_Text lowSummaryText;
    [SerializeField] private Button lowRowButton;
    [SerializeField] private GameObject lowSelectedOverlay;

    [Header("Medium Row")]
    [SerializeField] private Image[] mediumEnemyImages = new Image[6];
    [SerializeField] private TMP_Text mediumSummaryText;
    [SerializeField] private Button mediumRowButton;
    [SerializeField] private GameObject mediumSelectedOverlay;

    [Header("High Row")]
    [SerializeField] private Image[] highEnemyImages = new Image[6];
    [SerializeField] private TMP_Text highSummaryText;
    [SerializeField] private Button highRowButton;
    [SerializeField] private GameObject highSelectedOverlay;

    [Header("Preparation Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton;

    

    private MainFlowManager _flow;
    private BattleManager _battleManager;
    private bool _initialized;

    public void Initialize(MainFlowManager flow, BattleManager battleManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _battleManager = battleManager;

        BindButton(veryLowRowButton, OnVeryLowRowClicked);
        BindButton(lowRowButton, OnLowRowClicked);
        BindButton(mediumRowButton, OnMediumRowClicked);
        BindButton(highRowButton, OnHighRowClicked);
        BindButton(startButton, OnStartClicked);
        BindButton(backButton, OnBackClicked);


        CloseAll();

        _initialized = true;
    }

    public void OpenBattlePanel()
    {
        IReadOnlyList<BattleEncounterPreview> encounters =
            _battleManager != null ? _battleManager.DailyEncounters : null;

        int selectedIndex =
            _battleManager != null ? _battleManager.SelectedEncounterIndex : -1;

        OpenBattlePanel(encounters, selectedIndex);
    }

    public void OpenBattlePanel(IReadOnlyList<BattleEncounterPreview> encounters, int selectedIndex)
    {
        SetActive(battlePanelRoot, true);

        if (battleBodyText != null)
        {
            battleBodyText.text = "Select an opponent row.";
        }

        RenderEncounterRow(GetEncounterOrNull(encounters, 0), veryLowEnemyImages, veryLowSummaryText, veryLowRowButton);
        RenderEncounterRow(GetEncounterOrNull(encounters, 1), lowEnemyImages, lowSummaryText, lowRowButton);
        RenderEncounterRow(GetEncounterOrNull(encounters, 2), mediumEnemyImages, mediumSummaryText, mediumRowButton);
        RenderEncounterRow(GetEncounterOrNull(encounters, 3), highEnemyImages, highSummaryText, highRowButton);

        RefreshSelection(selectedIndex);

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        if (backButton != null)
        {
            backButton.interactable = true;
        }

    }

    public void RefreshSelection(int selectedIndex)
    {
        SetActive(veryLowSelectedOverlay, selectedIndex == 0);
        SetActive(lowSelectedOverlay, selectedIndex == 1);
        SetActive(mediumSelectedOverlay, selectedIndex == 2);
        SetActive(highSelectedOverlay, selectedIndex == 3);
    }

    public void CloseAll()
    {
        SetActive(battlePanelRoot, false);

        SetActive(veryLowSelectedOverlay, false);
        SetActive(lowSelectedOverlay, false);
        SetActive(mediumSelectedOverlay, false);
        SetActive(highSelectedOverlay, false);

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        if (backButton != null)
        {
            backButton.interactable = true;
        }

    }

    private void RenderEncounterRow(
        BattleEncounterPreview encounter,
        Image[] slotImages,
        TMP_Text summaryText,
        Button rowButton)
    {
        bool hasEncounter = encounter != null;

        if (rowButton != null)
        {
            rowButton.interactable = hasEncounter;
        }

        if (summaryText != null)
        {
            summaryText.text = hasEncounter
                ? $"Avg Lv {encounter.AverageLevel:0.0} / Gold {encounter.PreviewRewardGold}"
                : "Unavailable";
        }

        if (slotImages == null)
        {
            return;
        }

        for (int i = 0; i < slotImages.Length; i++)
        {
            Image slotImage = slotImages[i];
            if (slotImage == null)
            {
                continue;
            }

            BattleUnitSnapshot unit = null;
            if (hasEncounter && i < encounter.EnemyUnits.Count)
            {
                unit = encounter.EnemyUnits[i];
            }

            bool hasUnit = unit != null;
            slotImage.enabled = hasUnit;

            if (hasUnit && unit.PortraitSprite != null)
            {
                slotImage.sprite = unit.PortraitSprite;
            }
        }
    }

    private static BattleEncounterPreview GetEncounterOrNull(IReadOnlyList<BattleEncounterPreview> encounters, int index)
    {
        if (encounters == null)
        {
            return null;
        }

        if (index < 0 || index >= encounters.Count)
        {
            return null;
        }

        return encounters[index];
    }

    private void OnVeryLowRowClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleEncounterSelected(0);
        }
    }

    private void OnLowRowClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleEncounterSelected(1);
        }
    }

    private void OnMediumRowClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleEncounterSelected(2);
        }
    }

    private void OnHighRowClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleEncounterSelected(3);
        }
    }

    private void OnStartClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleStartRequested();
        }
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattlePreparationBackRequested();
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

    private static void ClearButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }
}