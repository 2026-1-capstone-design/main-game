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
    private WeaponModelPreviewView weaponPreviewView;

    [SerializeField]
    private GameObject levelMaskRoot;

    [SerializeField]
    private TMP_Text levelText;

    [SerializeField]
    private TMP_Text equippedMarkText;

    private OwnedItemViewData _data;
    private Action<OwnedItemViewData> _clickCallback;

    private const float LevelInfoHeight = 20f;
    private const float LevelTextHorizontalPadding = 4f;

    public void Setup(OwnedItemViewData data, Action<OwnedItemViewData> clickCallback)
    {
        ResolvePreviewViews();

        _data = data;
        _clickCallback = clickCallback;

        if (modelPreviewView != null)
        {
            if (data.ModelPrefab != null && !data.IsPlaceholder)
            {
                modelPreviewView.Show(
                    data.ModelPrefab,
                    data.ModelCustomizeIndicates,
                    data.LeftWeaponPrefab,
                    data.RightWeaponPrefab
                );
            }
            else
            {
                modelPreviewView.Clear();
            }
        }

        if (weaponPreviewView != null)
        {
            if (data.IsWeaponPreview && !data.IsPlaceholder)
            {
                weaponPreviewView.Show(data.LeftWeaponPrefab, data.RightWeaponPrefab);
            }
            else
            {
                weaponPreviewView.Clear();
            }
        }

        bool useModelPreview =
            !data.IsPlaceholder
            && (
                (modelPreviewView != null && data.ModelPrefab != null)
                || (weaponPreviewView != null && data.IsWeaponPreview && HasWeaponPreviewPrefab(data))
            );

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

        bool showGladiatorInfo = ShouldShowGladiatorInfo(data);
        SetLevelInfoVisible(showGladiatorInfo);

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

        if (weaponPreviewView != null)
        {
            weaponPreviewView.Clear();
        }

        if (levelText != null)
        {
            levelText.text = string.Empty;
        }

        SetLevelInfoVisible(false);

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

    private void ResolvePreviewViews()
    {
        if (modelPreviewView == null)
        {
            modelPreviewView = GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        if (weaponPreviewView == null)
        {
            weaponPreviewView = GetComponentInChildren<WeaponModelPreviewView>(true);
        }
    }

    private static bool ShouldShowGladiatorInfo(OwnedItemViewData data)
    {
        return !data.IsPlaceholder
            && !data.IsWeaponPreview
            && data.ModelPrefab != null
            && !string.IsNullOrEmpty(data.LevelText);
    }

    private static bool HasWeaponPreviewPrefab(OwnedItemViewData data)
    {
        return data.LeftWeaponPrefab != null || data.RightWeaponPrefab != null;
    }

    private void SetLevelInfoVisible(bool visible)
    {
        NormalizeLevelInfoLayout();

        if (levelMaskRoot != null)
        {
            levelMaskRoot.SetActive(visible);
        }

        if (levelText != null)
        {
            levelText.gameObject.SetActive(visible);
            levelText.text = visible ? _data.LevelText : string.Empty;
        }
    }

    private void NormalizeLevelInfoLayout()
    {
        if (levelMaskRoot != null && levelMaskRoot.TryGetComponent(out RectTransform maskRect))
        {
            StretchToBottom(maskRect, 0f);
        }

        if (levelText != null && levelText.TryGetComponent(out RectTransform textRect))
        {
            StretchToBottom(textRect, LevelTextHorizontalPadding);
        }
    }

    private static void StretchToBottom(RectTransform rectTransform, float horizontalPadding)
    {
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0f);
        rectTransform.offsetMin = new Vector2(horizontalPadding, 0f);
        rectTransform.offsetMax = new Vector2(-horizontalPadding, LevelInfoHeight);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }
}
