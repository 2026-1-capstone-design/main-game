using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OwnedItemGridCell : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button rootButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text equippedMarkText;

    private OwnedItemViewData _data;
    private Action<OwnedItemViewData> _clickCallback;

    public void Setup(OwnedItemViewData data, Action<OwnedItemViewData> clickCallback)
    {
        _data = data;
        _clickCallback = clickCallback;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
            iconImage.preserveAspect = true;
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
            rootButton.onClick.AddListener(OnClicked);
            rootButton.interactable = true;
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
