using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 팀 편성 패널의 슬롯 하나를 담당하는 셀.
// 비어 있으면 emptyStateRoot, 검투사가 배치되면 filledStateRoot를 활성화한다.
[DisallowMultipleComponent]
public sealed class SquadSlotCell : MonoBehaviour
{
    [SerializeField]
    private Button slotButton;

    [SerializeField]
    private GameObject emptyStateRoot;

    [SerializeField]
    private GameObject filledStateRoot;

    [SerializeField]
    private Image portraitImage;

    [SerializeField]
    private GladiatorModelPreviewView modelPreviewView;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private TMP_Text nameText;

    // 채워진 슬롯 위에 표시되는 제거 버튼 (X 버튼 등)
    [SerializeField]
    private Button clearButton;

    private int _slotIndex;
    private Action<int> _onSlotClicked;
    private Action<int> _onClearClicked;

    public void Setup(
        int slotIndex,
        OwnedGladiatorData gladiator,
        Action<int> onSlotClicked,
        Action<int> onClearClicked
    )
    {
        _slotIndex = slotIndex;
        _onSlotClicked = onSlotClicked;
        _onClearClicked = onClearClicked;

        ResolveMissingReferences();

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
        else
        {
            Debug.LogWarning($"[SquadSlotCell] slotButton is not assigned. Slot={slotIndex}", this);
        }

        if (clearButton != null)
        {
            clearButton.onClick.RemoveAllListeners();
            clearButton.onClick.AddListener(OnClearClicked);
        }

        Refresh(gladiator);
    }

    public void Refresh(OwnedGladiatorData gladiator)
    {
        bool filled = gladiator != null;

        if (emptyStateRoot != null)
        {
            emptyStateRoot.SetActive(!filled);
        }

        if (filledStateRoot != null)
        {
            filledStateRoot.SetActive(filled);
        }

        if (clearButton != null)
        {
            clearButton.gameObject.SetActive(filled);
        }

        GameObject modelPrefab =
            filled && gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null;
        bool useModelPreview = modelPreviewView != null && modelPrefab != null;

        if (modelPreviewView != null)
        {
            if (useModelPreview)
            {
                modelPreviewView.Show(modelPrefab, gladiator.CustomizeIndicates);
            }
            else
            {
                modelPreviewView.Clear();
            }
        }

        if (portraitImage != null)
        {
            Sprite portrait = filled ? gladiator.GladiatorClass?.icon : null;
            portraitImage.sprite = portrait;
            portraitImage.enabled = !useModelPreview && portrait != null;
        }

        if (levelText != null)
        {
            levelText.text = filled ? $"Lv. {gladiator.Level}" : string.Empty;
        }

        if (nameText != null)
        {
            nameText.text = filled ? gladiator.DisplayName : string.Empty;
        }
    }

    private void OnSlotClicked()
    {
        _onSlotClicked?.Invoke(_slotIndex);
    }

    private void OnClearClicked()
    {
        _onClearClicked?.Invoke(_slotIndex);
    }

    private void ResolveMissingReferences()
    {
        if (modelPreviewView == null)
        {
            modelPreviewView = GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
        }

        if (slotButton == null && emptyStateRoot != null)
        {
            slotButton = emptyStateRoot.GetComponentInChildren<Button>(true);
        }

        if (clearButton == null || slotButton != clearButton)
        {
            return;
        }

        clearButton = null;
    }
}
