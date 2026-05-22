using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OwnedItemGridCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Button rootButton;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private RawImage rawIconImage;

    [SerializeField]
    private GladiatorModelPreviewView modelPreviewView;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private TMP_Text equippedMarkText;

    private OwnedItemViewData _data;
    private Action<OwnedItemViewData> _clickCallback;

    public void Setup(OwnedItemViewData data, Action<OwnedItemViewData> clickCallback)
    {
        _data = data;
        _clickCallback = clickCallback;

        if (modelPreviewView != null)
        {
            if (data.ModelPrefab != null && !data.IsPlaceholder)
            {
                modelPreviewView.Show(data.ModelPrefab, data.ModelCustomizeIndicates);
            }
            else
            {
                modelPreviewView.Clear();
            }
        }

        bool useModelPreview = modelPreviewView != null && data.ModelPrefab != null && !data.IsPlaceholder;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = !useModelPreview && data.Icon != null;
            iconImage.preserveAspect = true;
        }

        if (rawIconImage != null)
        {
            rawIconImage.texture = data.RawIcon;
            rawIconImage.enabled = !useModelPreview && data.RawIcon != null;
        }

        if (levelText != null)
        {
            levelText.text = data.LevelText;
        }

        if (equippedMarkText != null)
        {
            equippedMarkText.text = data.EquippedMarkText;
        }

        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
            if (!data.IsPlaceholder)
            {
                rootButton.onClick.AddListener(OnClicked);
            }
            rootButton.interactable = !data.IsPlaceholder;
        }
    }

    public void Clear()
    {
        _data = default;
        _clickCallback = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (rawIconImage != null)
        {
            rawIconImage.texture = null;
            rawIconImage.enabled = false;
        }

        if (modelPreviewView != null)
        {
            modelPreviewView.Clear();
        }

        if (levelText != null)
        {
            levelText.text = string.Empty;
        }

        if (equippedMarkText != null)
        {
            equippedMarkText.text = string.Empty;
        }

        if (rootButton != null)
        {
            rootButton.onClick.RemoveAllListeners();
        }
    }

    private void OnClicked()
    {
        _clickCallback?.Invoke(_data);
    }
}
