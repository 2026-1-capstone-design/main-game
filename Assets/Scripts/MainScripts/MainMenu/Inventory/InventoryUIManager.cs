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

    [Header("Detail Panel")]
    [SerializeField]
    private GameObject detailPanelRoot;

    [SerializeField]
    private TMP_Text detailNameText;

    [SerializeField]
    private TMP_Text detailDescriptionText;

    [Header("Equipped Character Panels")]
    [SerializeField]
    private Image currentEquippedGladiatorImage;

    [SerializeField]
    private TMP_Text currentEquippedGladiatorNameText;

    [SerializeField]
    private Image plannedEquippedGladiatorImage;

    [SerializeField]
    private TMP_Text plannedEquippedGladiatorNameText;

    [Header("Optional Labels")]
    [SerializeField]
    private TMP_Text statusText;

    private readonly List<OwnedItemViewData> _weaponViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _artifactViewBuffer = new List<OwnedItemViewData>();
    private readonly StringBuilder _sb = new();

    private MainFlowManager _flow;
    private InventoryManager _inventoryManager;
    private GladiatorManager _gladiatorManager;
    private OwnedGladiatorData _plannedEquipGladiator;
    private bool _initialized;

    public void Initialize(
        MainFlowManager flow,
        InventoryManager inventoryManager,
        ResearchManager researchManager,
        GladiatorManager gladiatorManager
    )
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _inventoryManager = inventoryManager;
        _gladiatorManager = gladiatorManager;

        BindButton(backButton, OnBackClicked);
        BindButton(weaponTabButton, OnWeaponTabClicked);
        BindButton(artifactTabButton, OnArtifactTabClicked);

        SetDetailPanelActive(false);
        ClearEquippedCharacterPanels();
        SetPanelActive(false);

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPanelActive(true);
        ClearPlannedEquipGladiator();
        ClearDetailSelection();
        ShowWeaponTab();
    }

    public void ClosePanel()
    {
        ClearPlannedEquipGladiator();
        ClearDetailSelection();
        SetPanelActive(false);
    }

    public void SetPlannedEquipGladiator(OwnedGladiatorData gladiator)
    {
        _plannedEquipGladiator = gladiator;
        RefreshPlannedEquippedCharacter();
    }

    public void ClearPlannedEquipGladiator()
    {
        _plannedEquipGladiator = null;
        RefreshPlannedEquippedCharacter();
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
        ClearDetailSelection();
        ShowWeaponTab();
    }

    private void OnArtifactTabClicked()
    {
        ClearDetailSelection();
        ShowArtifactTab();
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

                _weaponViewBuffer.Add(new OwnedItemViewData(weapon.Weapon?.icon, weapon.DisplayName, weapon));
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

        if (_inventoryManager != null)
        {
            IReadOnlyList<OwnedArtifactData> artifacts = _inventoryManager.OwnedArtifacts;
            for (int i = 0; i < artifacts.Count; i++)
            {
                OwnedArtifactData artifact = artifacts[i];
                if (artifact == null || artifact.Artifact == null)
                {
                    continue;
                }

                _artifactViewBuffer.Add(new OwnedItemViewData(artifact.Artifact.icon, artifact.DisplayName, artifact));
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
        if (data.Source is not OwnedArtifactData artifact)
        {
            return;
        }

        ShowArtifactDetail(artifact);
    }

    private void ShowWeaponDetail(OwnedWeaponData weapon)
    {
        if (detailNameText != null)
        {
            detailNameText.text = weapon.DisplayName;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = BuildWeaponDescription(weapon);
        }

        RefreshCurrentEquippedCharacter(
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null
        );
        RefreshPlannedEquippedCharacter();
        SetDetailPanelActive(true);
    }

    private void ShowArtifactDetail(OwnedArtifactData artifact)
    {
        if (detailNameText != null)
        {
            detailNameText.text = artifact.DisplayName;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = BuildArtifactDescription(artifact);
        }

        RefreshCurrentEquippedCharacter(
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) : null
        );
        RefreshPlannedEquippedCharacter();
        SetDetailPanelActive(true);
    }

    private void ClearDetailSelection()
    {
        if (detailNameText != null)
        {
            detailNameText.text = string.Empty;
        }

        if (detailDescriptionText != null)
        {
            detailDescriptionText.text = string.Empty;
        }

        RefreshCurrentEquippedCharacter(null);
        RefreshPlannedEquippedCharacter();
        SetDetailPanelActive(false);
    }

    private void ClearEquippedCharacterPanels()
    {
        RefreshCurrentEquippedCharacter(null);
        RefreshPlannedEquippedCharacter();
    }

    private void RefreshCurrentEquippedCharacter(OwnedGladiatorData gladiator)
    {
        SetGladiatorPreview(currentEquippedGladiatorImage, currentEquippedGladiatorNameText, gladiator, "미착용");
    }

    private void RefreshPlannedEquippedCharacter()
    {
        SetGladiatorPreview(
            plannedEquippedGladiatorImage,
            plannedEquippedGladiatorNameText,
            _plannedEquipGladiator,
            "-"
        );
    }

    private static void SetGladiatorPreview(
        Image image,
        TMP_Text nameText,
        OwnedGladiatorData gladiator,
        string fallbackName
    )
    {
        Sprite icon = gladiator != null ? gladiator.GladiatorClass?.icon : null;

        if (image != null)
        {
            image.sprite = icon;
            image.enabled = icon != null;
            image.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.text = gladiator != null ? gladiator.DisplayName : fallbackName;
        }
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

    private static string BuildArtifactDescription(OwnedArtifactData artifact)
    {
        if (artifact == null || artifact.Artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.Artifact.artifactLore) ? "-" : artifact.Artifact.artifactLore;
        return $"퍼크: {artifact.Artifact.ArtifactPerkId}\n{lore}";
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
