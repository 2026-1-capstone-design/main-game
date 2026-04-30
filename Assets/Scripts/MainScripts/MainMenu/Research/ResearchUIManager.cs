using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResearchUIManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField]
    private Button testButton;

    [SerializeField]
    private Button backButton;

    [Header("Viewer")]
    [SerializeField]
    private OwnedItemGridViewer artifactViewer;

    [Header("Optional Labels")]
    [SerializeField]
    private TMP_Text headerText;

    [SerializeField]
    private TMP_Text statusText;

    private readonly List<OwnedItemViewData> _artifactViewBuffer = new List<OwnedItemViewData>();

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
        RefreshArtifactViewer();
    }

    public void ClosePanel()
    {
        SetPanelActive(false);
    }

    private void RefreshArtifactViewer()
    {
        if (artifactViewer == null)
        {
            Debug.LogWarning("[ResearchUIManager] artifactViewer is not assigned.", this);
            return;
        }

        _artifactViewBuffer.Clear();

        if (_researchManager != null)
        {
            IReadOnlyList<ArtifactSO> artifacts = _researchManager.UnlockedArtifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                ArtifactSO artifact = artifacts[i];
                if (artifact == null)
                {
                    continue;
                }

                _artifactViewBuffer.Add(new OwnedItemViewData(artifact.icon, artifact.artifactName, artifact));
            }
        }

        Canvas.ForceUpdateCanvases();
        artifactViewer.SetItems(_artifactViewBuffer, OnArtifactCellClicked);

        if (statusText != null)
        {
            statusText.text = $"Num of Artifacts: {_artifactViewBuffer.Count}";
        }
    }

    private void OnArtifactCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not ArtifactSO artifact)
        {
            return;
        }

        Debug.Log($"[ResearchUIManager] Artifact clicked: {artifact.artifactName}", this);

        if (statusText != null)
        {
            statusText.text = $"Selected Artifact : {artifact.artifactName}";
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
            int artifactCount = _researchManager != null ? _researchManager.GetUnlockedArtifactCount() : 0;
            headerText.text = $"Research (Unlocked Artifacts : {artifactCount})";
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
