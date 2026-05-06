using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryUIManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button weaponTabButton;

    [SerializeField]
    private Button artifactTabButton;

    [Header("Tab Canvas Groups")]
    [SerializeField]
    private CanvasGroup weaponTabGroup;

    [SerializeField]
    private CanvasGroup artifactTabGroup;

    [Header("Viewers")]
    [SerializeField]
    private OwnedItemGridViewer weaponViewer;

    [SerializeField]
    private OwnedItemGridViewer artifactViewer;

    [Header("Detail Modal")]
    [SerializeField]
    private GameObject detailPanelRoot;

    [SerializeField]
    private Button detailCloseButton;

    [SerializeField]
    private Image detailIcon;

    [SerializeField]
    private TMP_Text detailNameText;

    [SerializeField]
    private TMP_Text detailDescriptionText;

    [Header("Optional Labels")]
    [SerializeField]
    private TMP_Text statusText;

    private readonly List<OwnedItemViewData> _weaponViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _artifactViewBuffer = new List<OwnedItemViewData>();
    private readonly StringBuilder _sb = new();

    private MainFlowManager _flow;
    private InventoryManager _inventoryManager;
    private ResearchManager _researchManager;
    private bool _initialized;

    public void Initialize(MainFlowManager flow, InventoryManager inventoryManager, ResearchManager researchManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _inventoryManager = inventoryManager;
        _researchManager = researchManager;

        BindButton(backButton, OnBackClicked);
        BindButton(weaponTabButton, OnWeaponTabClicked);
        BindButton(artifactTabButton, OnArtifactTabClicked);
        BindButton(detailCloseButton, OnDetailCloseClicked);

        SetDetailPanelActive(false);
        SetPanelActive(false);

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPanelActive(true);
        SetDetailPanelActive(false);
        ShowWeaponTab();
    }

    public void ClosePanel()
    {
        SetDetailPanelActive(false);
        SetPanelActive(false);
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleInventoryBackRequested();
        }
    }

    private void OnWeaponTabClicked()
    {
        SetDetailPanelActive(false);
        ShowWeaponTab();
    }

    private void OnArtifactTabClicked()
    {
        SetDetailPanelActive(false);
        ShowArtifactTab();
    }

    private void OnDetailCloseClicked()
    {
        SetDetailPanelActive(false);
    }

    private void ShowWeaponTab()
    {
        SetTabGroupVisible(weaponTabGroup, true);
        SetTabGroupVisible(artifactTabGroup, false);
        RefreshWeaponViewer();
    }

    private void ShowArtifactTab()
    {
        SetTabGroupVisible(weaponTabGroup, false);
        SetTabGroupVisible(artifactTabGroup, true);
        RefreshArtifactViewer();
    }

    private void RefreshWeaponViewer()
    {
        if (weaponViewer == null)
        {
            return;
        }

        _weaponViewBuffer.Clear();

        if (_inventoryManager != null)
        {
            IReadOnlyList<OwnedWeaponData> weapons = _inventoryManager.OwnedWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                OwnedWeaponData weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                _weaponViewBuffer.Add(new OwnedItemViewData(
                    weapon.Weapon?.icon,
                    weapon.DisplayName,
                    weapon
                ));
            }
        }

        Canvas.ForceUpdateCanvases();
        weaponViewer.SetItems(_weaponViewBuffer, OnWeaponCellClicked);

        if (statusText != null)
        {
            statusText.text = $"장비 {_weaponViewBuffer.Count}개";
        }
    }

    private void RefreshArtifactViewer()
    {
        if (artifactViewer == null)
        {
            return;
        }

        _artifactViewBuffer.Clear();

        if (_researchManager != null)
        {
            IReadOnlyList<PerkSO> artifacts = _researchManager.UnlockedPerks;
            for (int i = 0; i < artifacts.Count; i++)
            {
                PerkSO artifact = artifacts[i];
                if (artifact == null)
                {
                    continue;
                }

                _artifactViewBuffer.Add(new OwnedItemViewData(artifact.icon, artifact.perkName, artifact));
            }
        }

        Canvas.ForceUpdateCanvases();
        artifactViewer.SetItems(_artifactViewBuffer, OnArtifactCellClicked);

        if (statusText != null)
        {
            statusText.text = $"장신구 {_artifactViewBuffer.Count}개";
        }
    }

    private void OnWeaponCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedWeaponData weapon)
        {
            return;
        }

        ShowWeaponDetail(weapon);
    }

    private void OnArtifactCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not PerkSO artifact)
        {
            return;
        }

        ShowArtifactDetail(artifact);
    }

    private void ShowWeaponDetail(OwnedWeaponData weapon)
    {
        if (detailPanelRoot == null)
        {
            return;
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = weapon.Weapon?.icon;
            detailIcon.enabled = detailIcon.sprite != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = weapon.DisplayName;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = BuildWeaponDescription(weapon);
        }

        SetDetailPanelActive(true);
    }

    private void ShowArtifactDetail(PerkSO artifact)
    {
        if (detailPanelRoot == null)
        {
            return;
        }

        if (detailIcon != null)
        {
            detailIcon.sprite = artifact.icon;
            detailIcon.enabled = detailIcon.sprite != null;
        }

        if (detailNameText != null)
        {
            detailNameText.text = artifact.perkName;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = string.IsNullOrWhiteSpace(artifact.description)
                ? "-"
                : artifact.description;
        }

        SetDetailPanelActive(true);
    }

    private string BuildWeaponDescription(OwnedWeaponData weapon)
    {
        _sb.Clear();

        if (weapon.Weapon != null)
        {
            _sb.AppendLine($"무기군: {weapon.Weapon.weaponType}");
        }

        _sb.AppendLine($"레벨: {weapon.Level}");

        if (weapon.WeaponSkill != null)
        {
            _sb.AppendLine($"스킬: {weapon.WeaponSkill.skillId}");
        }

        if (weapon.CachedAttackBonus != 0f)
        {
            _sb.AppendLine($"추가공격력: {weapon.CachedAttackBonus:+0.#;-0.#}");
        }

        if (weapon.CachedHealthBonus != 0f)
        {
            _sb.AppendLine($"추가체력: {weapon.CachedHealthBonus:+0.#;-0.#}");
        }

        if (weapon.CachedAttackSpeedBonus != 0f)
        {
            _sb.AppendLine($"추가공격속도: {weapon.CachedAttackSpeedBonus:+0.#;-0.#}");
        }

        if (weapon.CachedMoveSpeedBonus != 0f)
        {
            _sb.AppendLine($"추가이동속도: {weapon.CachedMoveSpeedBonus:+0.#;-0.#}");
        }

        if (weapon.CachedAttackRangeBonus != 0f)
        {
            _sb.AppendLine($"추가사거리: {weapon.CachedAttackRangeBonus:+0.#;-0.#}");
        }

        return _sb.ToString().TrimEnd();
    }

    private void SetDetailPanelActive(bool value)
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(value);
        }
    }

    private static void SetTabGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
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
