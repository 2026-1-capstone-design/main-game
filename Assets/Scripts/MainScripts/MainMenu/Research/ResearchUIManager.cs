using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResearchUIManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField] private Button testButton;
    [SerializeField] private Button backButton;

    [Header("Viewer")]
    [SerializeField] private OwnedItemGridViewer perkViewer;

    [Header("Optional Labels")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text statusText;

    private readonly List<OwnedItemViewData> _perkViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private ResearchManager _researchManager;
    private bool _initialized;

    public void Initialize(MainFlowManager flow, ResearchManager researchManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _researchManager = researchManager;

        BindButton(testButton, OnTestClicked);
        BindButton(backButton, OnBackClicked);

        if (testButton != null)
        {
            testButton.interactable = true;
        }

        SetPanelActive(false);
        RefreshTexts();

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPanelActive(true);
        RefreshTexts();
        RefreshPerkViewer();
    }

    public void ClosePanel()
    {
        SetPanelActive(false);
    }

    private void RefreshPerkViewer()
    {
        if (perkViewer == null)
        {
            Debug.LogWarning("[ResearchUIManager] perkViewer is not assigned.", this);
            return;
        }

        _perkViewBuffer.Clear();

        if (_researchManager != null)
        {
            IReadOnlyList<PerkSO> perks = _researchManager.UnlockedPerks;
            for (int i = 0; i < perks.Count; i++)
            {
                PerkSO perk = perks[i];
                if (perk == null)
                {
                    continue;
                }

                _perkViewBuffer.Add(new OwnedItemViewData(
                    perk.icon,
                    perk.perkName,
                    perk
                ));
            }
        }

        Canvas.ForceUpdateCanvases();
        perkViewer.SetItems(_perkViewBuffer, OnPerkCellClicked);

        if (statusText != null)
        {
            statusText.text = $"Num of Perks: {_perkViewBuffer.Count}";
        }
    }

    private void OnPerkCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not PerkSO perk)
        {
            return;
        }

        Debug.Log($"[ResearchUIManager] Perk clicked: {perk.perkName}", this);

        if (statusText != null)
        {
            statusText.text = $"Selected Perk : {perk.perkName}";
        }
    }

    private void OnTestClicked()
    {
        Debug.Log("[ResearchUIManager] Research test pressed", this);

        if (statusText != null)
        {
            statusText.text = "Research test pressed";
        }
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleResearchBackRequested();
        }
    }

    private void RefreshTexts()
    {
        if (headerText != null)
        {
            int perkCount = _researchManager != null ? _researchManager.GetUnlockedPerkCount() : 0;
            headerText.text = $"Research (Unlocked Perks : {perkCount})";
        }

        if (statusText != null && string.IsNullOrEmpty(statusText.text))
        {
            statusText.text = "Research panel";
        }
    }

    private void SetPanelActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
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
}
