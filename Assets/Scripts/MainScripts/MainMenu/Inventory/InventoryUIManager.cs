using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryUIManager : MonoBehaviour
{
    private enum InventoryTabMode
    {
        Weapon,
        Artifact,
    }

    [Header("Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [SerializeField]
    private RectTransform inventoryBackground;

    [Header("Buttons")]
    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button weaponTabButton;

    [SerializeField]
    private Button artifactTabButton;

    [Header("Viewers")]
    [SerializeField]
    private OwnedItemGridViewer itemViewer;

    [Header("Tab Labels")]
    [SerializeField]
    private TMP_Text selectedTabText;

    [Header("Detail Panel")]
    [SerializeField]
    private TMP_Text equipmentHeaderText;

    [SerializeField]
    private TMP_Text equipmentKindText;

    [SerializeField]
    private TMP_Text equipmentSkillText;

    [SerializeField]
    private TMP_Text equipmentDetailText;

    [SerializeField]
    private Image selectedItemIconImage;

    [SerializeField]
    private WeaponModelPreviewView selectedItemWeaponPreviewView;

    [SerializeField]
    private TMP_Text helpText;

    [SerializeField]
    private TMP_Text equippedGladiatorText;

    [SerializeField]
    private RawImage equippedGladiatorIcon;

    [SerializeField]
    private GladiatorModelPreviewView equippedGladiatorModelPreviewView;

    [SerializeField]
    private TMP_Text equippedGladiatorName;

    [SerializeField]
    private TMP_Text equippedGladiatorLevel;

    private readonly List<OwnedItemViewData> _weaponViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _artifactViewBuffer = new List<OwnedItemViewData>();
    private readonly StringBuilder _sb = new();

    private MainFlowManager _flow;
    private InventoryManager _inventoryManager;
    private GladiatorManager _gladiatorManager;
    private InventoryTabMode _currentDetailMode = InventoryTabMode.Weapon;
    private OwnedWeaponData _currentSelectedWeapon;
    private OwnedArtifactData _currentSelectedArtifact;
    private InventoryTabMode _currentTabMode = InventoryTabMode.Weapon;
    private bool _showLore;
    private bool _hasEquippedGladiatorDetail;
    private WeaponDetailLoreToggleInput _detailLoreToggleInput;
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

        ResolveMissingReferences();
        BindButton(backButton, OnBackClicked);
        BindButton(weaponTabButton, OnWeaponTabClicked);
        BindButton(artifactTabButton, OnArtifactTabClicked);
        EnsureDetailLoreToggleInput();
        CacheSelectedItemPreviewView();
        CacheEquippedGladiatorPreviewView();

        SetDetailPanelActive(false);
        SetPanelActive(false);

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPanelActive(true);
        ClearDetailSelection();
        ShowWeaponTab();
    }

    public void ClosePanel()
    {
        ClearDetailSelection();
        SetPanelActive(false);
    }

    public void SetPlannedEquipGladiator(OwnedGladiatorData gladiator)
    {
        // InventoryPanel은 장착/해제 흐름을 갖지 않는다. 기존 호출부 호환을 위해 메서드만 유지한다.
    }

    public void ClearPlannedEquipGladiator()
    {
        // InventoryPanel은 장착 예정 검투사를 표시하지 않는다.
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
        _currentTabMode = InventoryTabMode.Weapon;
        RefreshTabButtonLayering();
        RefreshSelectedTabText();
        RefreshWeaponViewer();
    }

    private void ShowArtifactTab()
    {
        _currentTabMode = InventoryTabMode.Artifact;
        RefreshTabButtonLayering();
        RefreshSelectedTabText();
        RefreshArtifactViewer();
    }

    private void RefreshWeaponViewer()
    {
        OwnedItemGridViewer viewer = GetActiveViewer(InventoryTabMode.Weapon);
        if (viewer == null)
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

                _weaponViewBuffer.Add(
                    new OwnedItemViewData(
                        weapon.Weapon?.leftWeaponPrefab,
                        weapon.Weapon?.rightWeaponPrefab,
                        weapon.Weapon?.icon,
                        weapon.DisplayName,
                        $"Lv.{weapon.Level}",
                        string.Empty,
                        weapon
                    )
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        viewer.SetItems(_weaponViewBuffer, OnWeaponCellClicked);
    }

    private void RefreshArtifactViewer()
    {
        OwnedItemGridViewer viewer = GetActiveViewer(InventoryTabMode.Artifact);
        if (viewer == null)
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
        viewer.SetItems(_artifactViewBuffer, OnArtifactCellClicked);
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
        _currentDetailMode = InventoryTabMode.Weapon;
        _currentSelectedWeapon = weapon;
        _currentSelectedArtifact = null;
        _showLore = false;

        SetDetailTexts(
            weapon.DisplayName,
            weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : string.Empty,
            weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : string.Empty,
            BuildWeaponStatsText(weapon)
        );
        SetSelectedWeaponPreview(weapon);
        SetEquippedGladiator(_gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null);
        EnsureDetailLoreToggleInput();
        SetDetailPanelActive(true);
    }

    private void ShowArtifactDetail(OwnedArtifactData artifact)
    {
        _currentDetailMode = InventoryTabMode.Artifact;
        _currentSelectedArtifact = artifact;
        _currentSelectedWeapon = null;
        _showLore = false;

        SetDetailTexts(artifact.DisplayName, "장신구", string.Empty, BuildArtifactLoreText(artifact));
        SetSelectedIcon(artifact.Artifact != null ? artifact.Artifact.icon : null);
        SetEquippedGladiator(
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) : null
        );
        EnsureDetailLoreToggleInput();
        SetDetailPanelActive(true);
    }

    private void ClearDetailSelection()
    {
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;
        _showLore = false;

        SetDetailTexts(string.Empty, string.Empty, string.Empty, string.Empty);
        SetSelectedIcon(null);
        SetEquippedGladiator(null);
        SetDetailPanelActive(false);
    }

    private void SetDetailTexts(string header, string kind, string skill, string detail)
    {
        SetText(equipmentHeaderText, header);
        SetText(equipmentKindText, kind);
        SetText(equipmentSkillText, skill);
        SetText(equipmentDetailText, detail);
    }

    private void SetSelectedIcon(Sprite icon)
    {
        ClearSelectedItemPreview();
        if (selectedItemIconImage == null)
        {
            return;
        }

        selectedItemIconImage.sprite = icon;
        selectedItemIconImage.enabled = icon != null;
        selectedItemIconImage.preserveAspect = true;
    }

    private void SetSelectedWeaponPreview(OwnedWeaponData weapon)
    {
        GameObject leftPrefab = weapon?.Weapon?.leftWeaponPrefab;
        GameObject rightPrefab = weapon?.Weapon?.rightWeaponPrefab;
        bool usePreview = selectedItemWeaponPreviewView != null && (leftPrefab != null || rightPrefab != null);

        if (selectedItemWeaponPreviewView != null)
        {
            if (usePreview)
            {
                selectedItemWeaponPreviewView.Show(leftPrefab, rightPrefab);
            }
            else
            {
                selectedItemWeaponPreviewView.Clear();
            }
        }

        SetSelectedImageFallback(usePreview ? null : weapon?.Weapon?.icon);
    }

    private void SetEquippedGladiator(OwnedGladiatorData gladiator)
    {
        bool hasGladiator = gladiator != null;
        _hasEquippedGladiatorDetail = hasGladiator;
        SetComponentGameObjectActive(equippedGladiatorText, hasGladiator);
        SetComponentGameObjectActive(equippedGladiatorIcon, hasGladiator);
        SetComponentGameObjectActive(equippedGladiatorName, hasGladiator);
        SetComponentGameObjectActive(equippedGladiatorLevel, hasGladiator);

        if (!hasGladiator)
        {
            ClearEquippedGladiatorPreview();
            SetRawImage(equippedGladiatorIcon, null);

            SetText(equippedGladiatorName, string.Empty);
            SetText(equippedGladiatorLevel, string.Empty);
            return;
        }

        SetText(equippedGladiatorName, gladiator.DisplayName);
        SetText(equippedGladiatorLevel, $"Lv. {gladiator.Level}");

        if (equippedGladiatorIcon != null)
        {
            Sprite icon = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
            GameObject modelPrefab =
                gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null;
            bool useModelPreview = equippedGladiatorModelPreviewView != null && modelPrefab != null;

            if (useModelPreview)
            {
                equippedGladiatorModelPreviewView.Show(
                    modelPrefab,
                    gladiator.CustomizeIndicates,
                    gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
                    gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab
                );
            }
            else
            {
                ClearEquippedGladiatorPreview();
            }

            if (useModelPreview)
            {
                if (!equippedGladiatorModelPreviewView.UsesTargetImage(equippedGladiatorIcon))
                {
                    SetRawImage(equippedGladiatorIcon, null);
                }
            }
            else
            {
                SetRawImage(equippedGladiatorIcon, icon);
            }
        }
    }

    private void ToggleDetailLore()
    {
        if (equipmentDetailText == null || !equipmentDetailText.gameObject.activeInHierarchy)
        {
            return;
        }

        if (_currentSelectedWeapon == null && _currentSelectedArtifact == null)
        {
            return;
        }

        _showLore = !_showLore;
        RefreshDetailTextBody();
    }

    private void RefreshDetailTextBody()
    {
        if (equipmentDetailText == null)
        {
            return;
        }

        if (_currentDetailMode == InventoryTabMode.Artifact)
        {
            equipmentDetailText.text = BuildArtifactLoreText(_currentSelectedArtifact);
            return;
        }

        equipmentDetailText.text = _showLore
            ? BuildWeaponLoreText(_currentSelectedWeapon)
            : BuildWeaponStatsText(_currentSelectedWeapon);
    }

    private string BuildWeaponStatsText(OwnedWeaponData weapon)
    {
        _sb.Clear();

        if (weapon == null)
        {
            return string.Empty;
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

    private static string BuildWeaponLoreText(OwnedWeaponData weapon)
    {
        if (weapon == null || weapon.Weapon == null || string.IsNullOrWhiteSpace(weapon.Weapon.lore))
        {
            return "-";
        }

        return weapon.Weapon.lore;
    }

    private static string BuildArtifactLoreText(OwnedArtifactData artifact)
    {
        if (artifact == null || artifact.Artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.Artifact.artifactLore) ? "-" : artifact.Artifact.artifactLore;
        return lore;
    }

    private void SetDetailPanelActive(bool value)
    {
        SetComponentGameObjectActive(equipmentHeaderText, value);
        SetComponentGameObjectActive(equipmentKindText, value);
        SetComponentGameObjectActive(equipmentSkillText, value);
        SetComponentGameObjectActive(equipmentDetailText, value);
        SetSelectedItemVisualRootActive(value);
        SetComponentGameObjectActive(selectedItemWeaponPreviewView, value && selectedItemWeaponPreviewView != null);
        SetComponentGameObjectActive(helpText, value);
        bool hasEquippedGladiator = value && _hasEquippedGladiatorDetail;
        SetComponentGameObjectActive(equippedGladiatorText, hasEquippedGladiator);
        SetComponentGameObjectActive(equippedGladiatorIcon, hasEquippedGladiator);
        SetComponentGameObjectActive(equippedGladiatorModelPreviewView, hasEquippedGladiator);
        SetComponentGameObjectActive(equippedGladiatorName, hasEquippedGladiator);
        SetComponentGameObjectActive(equippedGladiatorLevel, hasEquippedGladiator);
    }

    private void SetPanelActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
    }

    private void SetSelectedItemVisualRootActive(bool value)
    {
        if (selectedItemIconImage == null)
        {
            return;
        }

        selectedItemIconImage.gameObject.SetActive(value);
        selectedItemIconImage.enabled = value && selectedItemIconImage.sprite != null;
    }

    private OwnedItemGridViewer GetActiveViewer(InventoryTabMode tabMode)
    {
        return itemViewer;
    }

    private void RefreshTabButtonLayering()
    {
        MoveTabButtonsAroundBackground();
    }

    private void RefreshSelectedTabText()
    {
        SetText(selectedTabText, _currentTabMode == InventoryTabMode.Weapon ? "무기" : "장신구");
    }

    private void MoveTabButtonsAroundBackground()
    {
        if (weaponTabButton == null || artifactTabButton == null || inventoryBackground == null)
        {
            return;
        }

        Transform backgroundParent = inventoryBackground.parent;
        if (
            backgroundParent == null
            || weaponTabButton.transform.parent != backgroundParent
            || artifactTabButton.transform.parent != backgroundParent
        )
        {
            return;
        }

        DisableSortingCanvas(inventoryBackground.gameObject);
        DisableSortingCanvas(weaponTabButton.gameObject);
        DisableSortingCanvas(artifactTabButton.gameObject);

        Button activeButton = _currentTabMode == InventoryTabMode.Weapon ? weaponTabButton : artifactTabButton;
        Button inactiveButton = _currentTabMode == InventoryTabMode.Weapon ? artifactTabButton : weaponTabButton;

        // 최종 렌더 순서: 비선택 탭 -> 배경 -> 선택 탭.
        int startIndex = Mathf.Min(
            inventoryBackground.GetSiblingIndex(),
            activeButton.transform.GetSiblingIndex(),
            inactiveButton.transform.GetSiblingIndex()
        );

        Transform[] orderedTransforms = { inactiveButton.transform, inventoryBackground, activeButton.transform };

        for (int i = 0; i < orderedTransforms.Length; i++)
        {
            orderedTransforms[i].SetSiblingIndex(Mathf.Clamp(startIndex + i, 0, backgroundParent.childCount - 1));
        }
    }

    private static void DisableSortingCanvas(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = false;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private void SetSelectedImageFallback(Sprite icon)
    {
        if (selectedItemIconImage == null)
        {
            return;
        }

        selectedItemIconImage.sprite = icon;
        selectedItemIconImage.enabled = icon != null;
        selectedItemIconImage.preserveAspect = true;
    }

    private static void SetRawImage(RawImage image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        if (sprite == null || sprite.texture == null)
        {
            image.texture = null;
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            image.enabled = false;
            return;
        }

        Rect textureRect = sprite.textureRect;
        image.texture = sprite.texture;
        image.uvRect = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height
        );
        image.enabled = true;
    }

    private static void SetComponentGameObjectActive(Component component, bool value)
    {
        if (component != null)
        {
            component.gameObject.SetActive(value);
        }
    }

    private void EnsureDetailLoreToggleInput()
    {
        if (equipmentDetailText == null)
        {
            return;
        }

        _detailLoreToggleInput = equipmentDetailText.GetComponent<WeaponDetailLoreToggleInput>();
        if (_detailLoreToggleInput == null)
        {
            _detailLoreToggleInput = equipmentDetailText.gameObject.AddComponent<WeaponDetailLoreToggleInput>();
        }

        _detailLoreToggleInput.Initialize(ToggleDetailLore);
    }

    private void CacheEquippedGladiatorPreviewView()
    {
        if (equippedGladiatorModelPreviewView == null && equippedGladiatorIcon != null)
        {
            equippedGladiatorModelPreviewView = equippedGladiatorIcon.GetComponentInChildren<GladiatorModelPreviewView>(
                true
            );
        }
    }

    private void CacheSelectedItemPreviewView()
    {
        if (selectedItemWeaponPreviewView == null && selectedItemIconImage != null)
        {
            selectedItemWeaponPreviewView = selectedItemIconImage.GetComponentInChildren<WeaponModelPreviewView>(true);
        }
    }

    private void ClearSelectedItemPreview()
    {
        if (selectedItemWeaponPreviewView != null)
        {
            selectedItemWeaponPreviewView.Clear();
        }
    }

    private void ClearEquippedGladiatorPreview()
    {
        if (equippedGladiatorModelPreviewView != null)
        {
            equippedGladiatorModelPreviewView.Clear();
        }
    }

    private void ResolveMissingReferences()
    {
        if (inventoryBackground == null && panelRoot != null)
        {
            inventoryBackground = FindChildTransform(panelRoot.transform, "InventoryBackground") as RectTransform;
        }
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildTransform(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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
