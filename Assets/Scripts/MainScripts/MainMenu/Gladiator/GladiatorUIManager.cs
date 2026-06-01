using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GladiatorUIManager : MonoBehaviour
{
    private enum GladiatorListSortMode
    {
        RecentAcquired,
        Level,
    }

    private enum DetailInventoryMode
    {
        Weapon,
        Artifact,
    }

    [Header("List Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [Header("List Buttons")]
    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button recentAcquiredSortButton;

    [SerializeField]
    private Button levelSortButton;

    [SerializeField]
    private RectTransform gladiatorBackground;

    [Header("List Viewer")]
    [SerializeField]
    private OwnedItemGridViewer gladiatorViewer;

    [Header("Detail Layer")]
    [SerializeField]
    private GameObject detailPanelRoot;

    [Header("Detail Text")]
    [SerializeField]
    private TMP_Text detailText;

    [SerializeField]
    private Button detailBackButton;

    [Header("Detail Gladiator Icon")]
    [SerializeField]
    private Image detailGladiatorIconImage;

    [SerializeField]
    private GladiatorModelPreviewView detailGladiatorPreviewView;

    [Header("Detail Weapon Slot")]
    [SerializeField]
    private Button weaponSlotButton;

    [SerializeField]
    private Image weaponOverlayImage;

    [SerializeField]
    private WeaponModelPreviewView weaponOverlayPreviewView;

    [Header("Detail Artifact Slots")]
    [SerializeField]
    private Button[] artifactSlotButtons = new Button[3];

    [SerializeField]
    private Image[] artifactOverlayImages = new Image[3];

    [Header("Inventory Layer")]
    [SerializeField]
    private GameObject inventoryPanelRoot;

    [Header("Inventory Viewer")]
    [SerializeField]
    private OwnedItemGridViewer inventoryWeaponViewer;

    [Header("Inventory Labels")]
    [SerializeField]
    private TMP_Text inventoryHeaderText;

    [Header("Weapon Detail Layer")]
    [SerializeField]
    private TMP_Text equipmentHeaderText;

    [SerializeField]
    private TMP_Text equipmentKindText;

    [SerializeField]
    private TMP_Text equipmentSkillText;

    [SerializeField]
    private TMP_Text equipmentDetailText;

    [SerializeField]
    private Image selectedWeaponIconImage;

    [SerializeField]
    private WeaponModelPreviewView selectedWeaponPreviewView;

    [SerializeField]
    private TMP_Text helpText;

    [SerializeField]
    private Button weaponDetailEquipButton;

    [SerializeField]
    private Button weaponDetailUnequipButton;

    [Header("Already Equipped Popup")]
    [SerializeField]
    private GameObject alreadyEquippedPanelRoot;

    [SerializeField]
    private TMP_Text alreadyEquippedText;

    [SerializeField]
    private Button alreadyEquippedEquipButton;

    [SerializeField]
    private Button alreadyEquippedCancelButton;

    private readonly List<OwnedGladiatorData> _sortedGladiatorBuffer = new List<OwnedGladiatorData>();
    private readonly List<OwnedItemViewData> _gladiatorViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _weaponViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _artifactViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private OwnedGladiatorData _currentDetailGladiator;
    private OwnedWeaponData _currentSelectedWeapon;
    private OwnedArtifactData _currentSelectedArtifact;
    private int _currentArtifactSlotIndex;
    private DetailInventoryMode _inventoryMode = DetailInventoryMode.Weapon;
    private bool _showWeaponLore;
    private GladiatorListSortMode _currentSortMode = GladiatorListSortMode.RecentAcquired;
    private Transform[] _sortButtonOriginalParents;
    private int[] _sortButtonOriginalSiblingIndices;
    private bool _initialized;
    private WeaponDetailLoreToggleInput _weaponDetailLoreToggleInput;

    private void ToggleWeaponDetailLore()
    {
        if (_currentSelectedWeapon == null || !IsWeaponDetailActive())
        {
            return;
        }

        Debug.Log(
            $"Left Alt 입력 감지됨: {_currentSelectedWeapon.DisplayName} 무기 상세 표시를 {(_showWeaponLore ? "스탯" : "로어")}로 전환"
        );
        _showWeaponLore = !_showWeaponLore;
        RefreshEquipmentDetailText();
    }

    public void Initialize(MainFlowManager flow, GladiatorManager gladiatorManager, InventoryManager inventoryManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _gladiatorManager = gladiatorManager;
        _inventoryManager = inventoryManager;

        ResolveMissingListReferences();
        CaptureSortButtonLayout();
        BindButton(backButton, OnBackClicked);
        BindButton(recentAcquiredSortButton, OnRecentAcquiredSortClicked);
        BindButton(levelSortButton, OnLevelSortClicked);
        CacheDetailPresetControls();
        BindButton(detailBackButton, OnDetailBackClicked);
        BindButton(weaponSlotButton, OnWeaponSlotClicked);
        BindArtifactSlotButtons();

        BindButton(weaponDetailEquipButton, OnWeaponDetailEquipClicked);
        BindButton(weaponDetailUnequipButton, OnWeaponDetailUnequipClicked);

        BindButton(alreadyEquippedEquipButton, OnAlreadyEquippedEquipClicked);
        BindButton(alreadyEquippedCancelButton, OnAlreadyEquippedCancelClicked);

        ConfigureOverlayImage(detailGladiatorIconImage);
        ConfigureOverlayImage(weaponOverlayImage);
        ConfigureOverlayImage(selectedWeaponIconImage);
        ConfigureOverlayImages(artifactOverlayImages);
        CacheWeaponPreviewViews();
        EnsureWeaponDetailLoreToggleInput();

        SetPanelActive(false);
        SetDetailActive(false);
        SetInventoryActive(false);
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPanelActive(true);
        SetDetailActive(false);
        SetInventoryActive(false);
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);
        _currentSelectedWeapon = null;

        RefreshSortButtons();
        RefreshGladiatorViewer();
    }

    public void ClosePanel()
    {
        _currentSelectedWeapon = null;
        SetAlreadyEquippedPopupActive(false);
        SetWeaponDetailActive(false);
        SetInventoryActive(false);
        SetDetailActive(false);
        SetPanelActive(false);
    }

    private void RefreshGladiatorViewer()
    {
        if (gladiatorViewer == null)
        {
            Debug.LogWarning("[GladiatorUIManager] gladiatorViewer is not assigned.", this);
            return;
        }

        _gladiatorViewBuffer.Clear();

        if (_gladiatorManager != null)
        {
            IReadOnlyList<OwnedGladiatorData> gladiators = _gladiatorManager.OwnedGladiators;
            BuildSortedGladiatorBuffer(gladiators);

            for (int i = 0; i < _sortedGladiatorBuffer.Count; i++)
            {
                OwnedGladiatorData gladiator = _sortedGladiatorBuffer[i];
                if (gladiator == null || gladiator.GladiatorClass == null)
                {
                    continue;
                }

                _gladiatorViewBuffer.Add(
                    new OwnedItemViewData(
                        gladiator.GladiatorClass.previewModelPrefab,
                        gladiator.CustomizeIndicates,
                        gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
                        gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab,
                        gladiator.GladiatorClass.icon,
                        gladiator.DisplayName,
                        $"Lv.{gladiator.Level}",
                        string.Empty,
                        gladiator
                    )
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        gladiatorViewer.SetItems(_gladiatorViewBuffer, OnGladiatorCellClicked);
    }

    private void RefreshInventoryWeaponViewer()
    {
        if (inventoryWeaponViewer == null)
        {
            Debug.LogWarning("[GladiatorUIManager] inventoryWeaponViewer is not assigned.", this);
            return;
        }

        _weaponViewBuffer.Clear();

        if (_inventoryManager != null)
        {
            IReadOnlyList<OwnedWeaponData> weapons = _inventoryManager.OwnedWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                OwnedWeaponData weapon = weapons[i];
                if (weapon == null || weapon.Weapon == null)
                {
                    continue;
                }

                OwnedGladiatorData owner =
                    _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null;

                _weaponViewBuffer.Add(
                    new OwnedItemViewData(
                        weapon.Weapon.icon,
                        weapon.DisplayName,
                        $"Lv.{weapon.Level}",
                        owner != null ? "E" : string.Empty,
                        weapon
                    )
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        inventoryWeaponViewer.SetItems(_weaponViewBuffer, OnInventoryWeaponCellClicked);

        if (inventoryHeaderText != null)
        {
            inventoryHeaderText.text = $"무기";
        }

        ClearEquipmentDetailTexts();
    }

    private void RefreshInventoryArtifactViewer()
    {
        if (inventoryWeaponViewer == null)
        {
            Debug.LogWarning("[GladiatorUIManager] inventoryWeaponViewer is not assigned.", this);
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

                OwnedGladiatorData owner =
                    _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) : null;

                _artifactViewBuffer.Add(
                    new OwnedItemViewData(
                        artifact.Artifact.icon,
                        artifact.DisplayName,
                        string.Empty,
                        owner != null ? "E" : string.Empty,
                        artifact
                    )
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        inventoryWeaponViewer.SetItems(_artifactViewBuffer, OnInventoryArtifactCellClicked);

        if (inventoryHeaderText != null)
        {
            inventoryHeaderText.text = "장신구";
        }

        ClearEquipmentDetailTexts();
    }

    private void BuildSortedGladiatorBuffer(IReadOnlyList<OwnedGladiatorData> gladiators)
    {
        _sortedGladiatorBuffer.Clear();

        if (gladiators == null)
        {
            return;
        }

        for (int i = 0; i < gladiators.Count; i++)
        {
            if (gladiators[i] != null)
            {
                _sortedGladiatorBuffer.Add(gladiators[i]);
            }
        }

        _sortedGladiatorBuffer.Sort(CompareGladiatorsForCurrentSort);
    }

    private int CompareGladiatorsForCurrentSort(OwnedGladiatorData left, OwnedGladiatorData right)
    {
        if (left == right)
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        if (_currentSortMode == GladiatorListSortMode.Level)
        {
            int levelCompare = right.Level.CompareTo(left.Level);
            if (levelCompare != 0)
            {
                return levelCompare;
            }
        }

        return right.RuntimeId.CompareTo(left.RuntimeId);
    }

    private void OnRecentAcquiredSortClicked()
    {
        SetSortMode(GladiatorListSortMode.RecentAcquired);
    }

    private void OnLevelSortClicked()
    {
        SetSortMode(GladiatorListSortMode.Level);
    }

    private void SetSortMode(GladiatorListSortMode sortMode)
    {
        _currentSortMode = sortMode;
        RefreshSortButtons();
        RefreshGladiatorViewer();
    }

    private void RefreshSortButtons()
    {
        Button activeButton =
            _currentSortMode == GladiatorListSortMode.Level ? levelSortButton : recentAcquiredSortButton;
        Button inactiveButton =
            _currentSortMode == GladiatorListSortMode.Level ? recentAcquiredSortButton : levelSortButton;

        MoveSortButtonAroundBackground(inactiveButton, false);
        MoveSortButtonAroundBackground(activeButton, true);
    }

    private void MoveSortButtonAroundBackground(Button sortButton, bool isActive)
    {
        if (sortButton == null || gladiatorBackground == null)
        {
            return;
        }

        sortButton.interactable = true;

        RectTransform buttonTransform = sortButton.transform as RectTransform;
        if (buttonTransform == null)
        {
            return;
        }

        int buttonIndex = GetSortButtonIndex(sortButton);
        if (!isActive)
        {
            RestoreSortButtonParent(buttonTransform, buttonIndex);
            return;
        }

        Transform backgroundParent = gladiatorBackground.parent;
        if (backgroundParent == null)
        {
            return;
        }

        // GladiatorBackground와 정렬 버튼 컨테이너가 형제인 배치에서 선택 버튼만 배경 위로 올린다.
        buttonTransform.SetParent(backgroundParent, true);
        int backgroundIndex = gladiatorBackground.GetSiblingIndex();
        buttonTransform.SetSiblingIndex(Mathf.Min(backgroundIndex + 1, backgroundParent.childCount - 1));
    }

    private void RestoreSortButtonParent(RectTransform buttonTransform, int buttonIndex)
    {
        if (buttonTransform == null || _sortButtonOriginalParents == null || _sortButtonOriginalSiblingIndices == null)
        {
            return;
        }

        if (buttonIndex < 0 || buttonIndex >= _sortButtonOriginalParents.Length)
        {
            return;
        }

        Transform originalParent = _sortButtonOriginalParents[buttonIndex];
        if (originalParent == null)
        {
            return;
        }

        if (buttonTransform.parent != originalParent)
        {
            buttonTransform.SetParent(originalParent, true);
        }

        int siblingIndex = Mathf.Clamp(
            _sortButtonOriginalSiblingIndices[buttonIndex],
            0,
            originalParent.childCount - 1
        );
        buttonTransform.SetSiblingIndex(siblingIndex);
    }

    private int GetSortButtonIndex(Button sortButton)
    {
        if (sortButton == recentAcquiredSortButton)
        {
            return 0;
        }

        if (sortButton == levelSortButton)
        {
            return 1;
        }

        return -1;
    }

    private void OnGladiatorCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedGladiatorData gladiator)
        {
            return;
        }

        OpenDetail(gladiator);
    }

    private void OpenDetail(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return;
        }

        _currentDetailGladiator = gladiator;
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;

        RefreshDetail(gladiator);
        SetDetailActive(true);
        SetInventoryActive(false);
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);
    }

    private void RefreshDetail(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return;
        }

        Sprite gladiatorIcon = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
        GameObject gladiatorModelPrefab =
            gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null;
        OwnedWeaponData equippedWeapon = gladiator.EquippedWeapon;

        if (detailGladiatorPreviewView != null && gladiatorModelPrefab != null)
        {
            detailGladiatorPreviewView.Show(
                gladiatorModelPrefab,
                gladiator.CustomizeIndicates,
                gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
                gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab
            );
            SetComponentGameObjectActive(detailGladiatorIconImage, false);
        }
        else
        {
            if (detailGladiatorPreviewView != null)
            {
                detailGladiatorPreviewView.Clear();
            }

            SetPassiveImage(detailGladiatorIconImage, gladiatorIcon);
        }

        SetWeaponSlotVisual(weaponSlotButton, weaponOverlayImage, weaponOverlayPreviewView, equippedWeapon);
        RefreshArtifactSlots(gladiator);

        string detailDescription = BuildGladiatorDetailDescription(gladiator);

        if (detailText != null)
        {
            detailText.text = detailDescription;
        }
    }

    private void OnInventoryWeaponCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedWeaponData weapon)
        {
            return;
        }

        OpenWeaponDetail(weapon);
    }

    private void OnInventoryArtifactCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedArtifactData artifact)
        {
            return;
        }

        OpenArtifactDetail(artifact);
    }

    private void OpenWeaponDetail(OwnedWeaponData weapon)
    {
        if (weapon == null || _currentDetailGladiator == null)
        {
            return;
        }

        _currentSelectedWeapon = weapon;
        _currentSelectedArtifact = null;
        _showWeaponLore = false;
        EnsureWeaponDetailLoreToggleInput();
        RefreshWeaponDetail(weapon);
        SetWeaponDetailActive(true);
        SetAlreadyEquippedPopupActive(false);
    }

    private void OpenArtifactDetail(OwnedArtifactData artifact)
    {
        if (artifact == null || artifact.Artifact == null || _currentDetailGladiator == null)
        {
            return;
        }

        _currentSelectedWeapon = null;
        _currentSelectedArtifact = artifact;
        _showWeaponLore = false;
        RefreshArtifactDetail(artifact);
        SetWeaponDetailActive(true);
        SetAlreadyEquippedPopupActive(false);
    }

    private void RefreshArtifactDetail(OwnedArtifactData artifact)
    {
        OwnedGladiatorData owner =
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) : null;

        bool equippedByCurrent = owner != null && owner == _currentDetailGladiator;

        if (equipmentHeaderText != null)
        {
            equipmentHeaderText.text = artifact.DisplayName;
        }

        if (equipmentKindText != null)
        {
            equipmentKindText.text = artifact.Artifact.ArtifactPerkId.ToString();
        }

        if (equipmentSkillText != null)
        {
            equipmentSkillText.text = string.Empty;
        }

        if (equipmentDetailText != null)
        {
            equipmentDetailText.text = string.IsNullOrWhiteSpace(artifact.Artifact.artifactLore)
                ? "-"
                : artifact.Artifact.artifactLore;
        }

        SetWeaponPreview(selectedWeaponIconImage, selectedWeaponPreviewView, null, artifact.Artifact.icon);

        if (weaponDetailEquipButton != null)
        {
            weaponDetailEquipButton.gameObject.SetActive(!equippedByCurrent);
        }

        if (weaponDetailUnequipButton != null)
        {
            weaponDetailUnequipButton.gameObject.SetActive(equippedByCurrent);
        }
    }

    private void RefreshWeaponDetail(OwnedWeaponData weapon)
    {
        if (weapon == null)
        {
            return;
        }

        OwnedGladiatorData owner =
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null;

        bool equippedByCurrent = owner != null && owner == _currentDetailGladiator;
        string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
        string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";

        if (equipmentHeaderText != null)
        {
            equipmentHeaderText.text = weapon.DisplayName;
        }

        if (equipmentKindText != null)
        {
            equipmentKindText.text = weaponTypeText;
        }

        if (equipmentSkillText != null)
        {
            equipmentSkillText.text = skillName;
        }

        SetWeaponPreview(
            selectedWeaponIconImage,
            selectedWeaponPreviewView,
            weapon,
            weapon.Weapon != null ? weapon.Weapon.icon : null
        );
        RefreshEquipmentDetailText();

        if (weaponDetailEquipButton != null)
        {
            weaponDetailEquipButton.gameObject.SetActive(true);
        }

        if (weaponDetailUnequipButton != null)
        {
            weaponDetailUnequipButton.gameObject.SetActive(false);
        }
    }

    private void RefreshEquipmentDetailText()
    {
        if (equipmentDetailText == null)
        {
            return;
        }

        equipmentDetailText.text = _showWeaponLore
            ? BuildWeaponLoreText(_currentSelectedWeapon)
            : BuildWeaponComparisonText(_currentDetailGladiator, _currentSelectedWeapon);
    }

    private bool IsWeaponDetailActive()
    {
        return equipmentDetailText != null && equipmentDetailText.gameObject.activeInHierarchy;
    }

    private static string BuildWeaponLoreText(OwnedWeaponData weapon)
    {
        string lore = weapon != null && weapon.Weapon != null ? weapon.Weapon.lore : string.Empty;
        return string.IsNullOrWhiteSpace(lore) ? "로어가 없습니다." : lore;
    }

    private static string BuildWeaponComparisonText(OwnedGladiatorData gladiator, OwnedWeaponData nextWeapon)
    {
        if (gladiator == null || nextWeapon == null)
        {
            return string.Empty;
        }

        OwnedWeaponData currentWeapon = gladiator.EquippedWeapon;

        float nextHealth = SwapWeaponBonus(
            gladiator.CachedMaxHealth,
            GetWeaponHealthBonus(currentWeapon),
            GetWeaponHealthBonus(nextWeapon)
        );
        float nextAttack = SwapWeaponBonus(
            gladiator.CachedAttack,
            GetWeaponAttackBonus(currentWeapon),
            GetWeaponAttackBonus(nextWeapon)
        );
        float nextAttackSpeed = SwapWeaponBonus(
            gladiator.CachedAttackSpeed,
            GetWeaponAttackSpeedBonus(currentWeapon),
            GetWeaponAttackSpeedBonus(nextWeapon)
        );
        float nextMoveSpeed = SwapWeaponBonus(
            gladiator.CachedMoveSpeed,
            GetWeaponMoveSpeedBonus(currentWeapon),
            GetWeaponMoveSpeedBonus(nextWeapon)
        );
        float nextAttackRange = SwapWeaponBonus(
            gladiator.CachedAttackRange,
            GetWeaponAttackRangeBonus(currentWeapon),
            GetWeaponAttackRangeBonus(nextWeapon)
        );

        return BuildStatComparisonLine("체력", nextHealth, nextHealth - gladiator.CachedMaxHealth)
            + "\n"
            + BuildStatComparisonLine("공격력", nextAttack, nextAttack - gladiator.CachedAttack)
            + "\n"
            + BuildStatComparisonLine("공격속도", nextAttackSpeed, nextAttackSpeed - gladiator.CachedAttackSpeed)
            + "\n"
            + BuildStatComparisonLine("이동속도", nextMoveSpeed, nextMoveSpeed - gladiator.CachedMoveSpeed)
            + "\n"
            + BuildStatComparisonLine("공격 사거리", nextAttackRange, nextAttackRange - gladiator.CachedAttackRange);
    }

    private static string BuildStatComparisonLine(string label, float value, float delta)
    {
        string deltaText = FormatStatDelta(delta);
        return string.IsNullOrEmpty(deltaText) ? $"{label} : {value:0.##}" : $"{label} : {value:0.##} {deltaText}";
    }

    private static string FormatStatDelta(float delta)
    {
        if (Mathf.Abs(delta) < 0.005f)
        {
            return string.Empty;
        }

        string color = delta > 0f ? "#FF3333" : "#3366FF";
        string sign = delta > 0f ? "+" : string.Empty;
        return $"<color={color}>({sign}{delta:0.##})</color>";
    }

    private static float SwapWeaponBonus(float currentValue, float currentWeaponBonus, float nextWeaponBonus)
    {
        return Mathf.Max(0f, currentValue - currentWeaponBonus + nextWeaponBonus);
    }

    private static float GetWeaponHealthBonus(OwnedWeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0f, weapon.CachedHealthBonus) : 0f;
    }

    private static float GetWeaponAttackBonus(OwnedWeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0f, weapon.CachedAttackBonus) : 0f;
    }

    private static float GetWeaponAttackSpeedBonus(OwnedWeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0f, weapon.CachedAttackSpeedBonus) : 0f;
    }

    private static float GetWeaponMoveSpeedBonus(OwnedWeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0f, weapon.CachedMoveSpeedBonus) : 0f;
    }

    private static float GetWeaponAttackRangeBonus(OwnedWeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0f, weapon.CachedAttackRangeBonus) : 0f;
    }

    private void ClearEquipmentDetailTexts()
    {
        if (equipmentHeaderText != null)
        {
            equipmentHeaderText.text = string.Empty;
        }

        if (equipmentKindText != null)
        {
            equipmentKindText.text = string.Empty;
        }

        if (equipmentSkillText != null)
        {
            equipmentSkillText.text = string.Empty;
        }

        if (equipmentDetailText != null)
        {
            equipmentDetailText.text = string.Empty;
        }

        SetWeaponPreview(selectedWeaponIconImage, selectedWeaponPreviewView, null, null);
    }

    private void OnWeaponDetailEquipClicked()
    {
        if (_inventoryMode == DetailInventoryMode.Artifact)
        {
            TryEquipSelectedArtifactAndCloseInventory();
            return;
        }

        if (_currentDetailGladiator == null || _currentSelectedWeapon == null || _gladiatorManager == null)
        {
            return;
        }

        OwnedGladiatorData owner = _gladiatorManager.FindOwnerOfEquippedWeapon(_currentSelectedWeapon);
        if (owner != null && owner == _currentDetailGladiator)
        {
            TryUnequipSelectedWeapon();
            return;
        }

        if (owner != null && owner != _currentDetailGladiator)
        {
            OpenAlreadyEquippedPopup();
            return;
        }

        TryEquipSelectedWeaponAndCloseInventory();
    }

    private void OnWeaponDetailUnequipClicked()
    {
        if (_currentDetailGladiator == null || _gladiatorManager == null)
        {
            return;
        }

        string failReason;
        bool succeeded =
            _inventoryMode == DetailInventoryMode.Artifact
                ? _gladiatorManager.TryUnequipArtifact(
                    _currentDetailGladiator,
                    _currentSelectedArtifact,
                    out failReason
                )
                : _gladiatorManager.TryUnequipWeapon(_currentDetailGladiator, out failReason);

        if (!succeeded)
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[GladiatorUIManager] " + failReason, this);
            }
            return;
        }

        CloseInventoryPanel();
    }

    private void TryEquipSelectedArtifactAndCloseInventory()
    {
        if (_currentDetailGladiator == null || _currentSelectedArtifact == null || _gladiatorManager == null)
        {
            return;
        }

        string failReason;
        bool succeeded = _gladiatorManager.TryEquipArtifact(
            _currentDetailGladiator,
            _currentSelectedArtifact,
            out failReason
        );

        if (!succeeded)
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[GladiatorUIManager] " + failReason, this);
            }
            return;
        }

        CloseInventoryPanel();
    }

    private void TryUnequipSelectedWeapon()
    {
        if (_currentDetailGladiator == null || _gladiatorManager == null)
        {
            return;
        }

        string failReason;
        bool succeeded = _gladiatorManager.TryUnequipWeapon(_currentDetailGladiator, out failReason);

        if (!succeeded)
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[GladiatorUIManager] " + failReason, this);
            }
            return;
        }

        _currentSelectedWeapon = null;
        SetWeaponDetailActive(false);
        RefreshDetail(_currentDetailGladiator);
    }

    private void OpenAlreadyEquippedPopup()
    {
        if (alreadyEquippedText != null)
        {
            OwnedGladiatorData owner =
                _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(_currentSelectedWeapon) : null;
            string ownerName = owner != null ? owner.DisplayName : "다른 검투사";
            string weaponName = _currentSelectedWeapon != null ? _currentSelectedWeapon.DisplayName : "선택한 장비";
            string subjectParticle = HasFinalConsonant(ownerName) ? "이" : "가";
            string weaponParticle = HasFinalConsonant(weaponName) ? "은" : "는";

            alreadyEquippedText.text =
                $"{weaponName}{weaponParticle} 현재 {ownerName}{subjectParticle} 장착중입니다.\n"
                + $"해당 장비를 장착 시 {ownerName}의 {weaponName}{weaponParticle} 해제됩니다.";
        }

        SetAlreadyEquippedPopupActive(true);
    }

    private void OnAlreadyEquippedEquipClicked()
    {
        SetAlreadyEquippedPopupActive(false);
        TryEquipSelectedWeaponAndCloseInventory();
    }

    private void OnAlreadyEquippedCancelClicked()
    {
        SetAlreadyEquippedPopupActive(false);
    }

    private static bool HasFinalConsonant(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        char lastCharacter = value[value.Length - 1];
        if (lastCharacter < '가' || lastCharacter > '힣')
        {
            return false;
        }

        return (lastCharacter - '가') % 28 != 0;
    }

    private void TryEquipSelectedWeaponAndCloseInventory()
    {
        if (_currentDetailGladiator == null || _currentSelectedWeapon == null || _gladiatorManager == null)
        {
            return;
        }

        string failReason;
        bool succeeded = _gladiatorManager.TryEquipWeapon(
            _currentDetailGladiator,
            _currentSelectedWeapon,
            out failReason
        );

        if (!succeeded)
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[GladiatorUIManager] " + failReason, this);
            }
            return;
        }

        CloseInventoryPanel();
    }

    private void CloseWeaponDetail()
    {
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;
        _showWeaponLore = false;
        SetWeaponDetailActive(false);
    }

    private void OnWeaponSlotClicked()
    {
        if (_currentDetailGladiator == null)
        {
            return;
        }

        OpenInventoryPanel(DetailInventoryMode.Weapon);
    }

    private void OnArtifactSlotClicked(int slotIndex)
    {
        if (_currentDetailGladiator == null)
        {
            return;
        }

        _currentArtifactSlotIndex = slotIndex;
        OpenInventoryPanel(DetailInventoryMode.Artifact);
    }

    private void OpenInventoryPanel(DetailInventoryMode mode)
    {
        _inventoryMode = mode;
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;
        _showWeaponLore = false;
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);

        if (_currentDetailGladiator != null)
        {
            RefreshDetail(_currentDetailGladiator);
        }

        if (_inventoryMode == DetailInventoryMode.Artifact)
        {
            RefreshInventoryArtifactViewer();
        }
        else
        {
            RefreshInventoryWeaponViewer();
        }

        SetInventoryActive(true);
        OpenEquippedItemDetailForCurrentInventoryMode();
    }

    private void OpenEquippedItemDetailForCurrentInventoryMode()
    {
        if (_currentDetailGladiator == null)
        {
            return;
        }

        if (_inventoryMode == DetailInventoryMode.Artifact)
        {
            OwnedArtifactData equippedArtifact = _currentDetailGladiator.GetEquippedArtifact(_currentArtifactSlotIndex);
            if (equippedArtifact != null && equippedArtifact.Artifact != null)
            {
                OpenArtifactDetail(equippedArtifact);
            }

            return;
        }

        OwnedWeaponData equippedWeapon = _currentDetailGladiator.EquippedWeapon;
        if (equippedWeapon != null && equippedWeapon.Weapon != null)
        {
            OpenWeaponDetail(equippedWeapon);
        }
    }

    //이미 장착/탈착 직후 RefreshDetail()을 호출하고 있어서 아이콘은 뜬느데 혹시 모르니
    private void CloseInventoryPanel()
    {
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;
        SetAlreadyEquippedPopupActive(false);
        SetWeaponDetailActive(false);
        SetInventoryActive(false);

        if (_currentDetailGladiator != null)
        {
            RefreshDetail(_currentDetailGladiator);
        }
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleGladiatorBackRequested();
        }
    }

    private void OnDetailBackClicked()
    {
        CloseDetail();
    }

    private void CloseDetail()
    {
        _currentDetailGladiator = null;
        _currentSelectedWeapon = null;
        _currentSelectedArtifact = null;

        SetAlreadyEquippedPopupActive(false);
        SetWeaponDetailActive(false);
        SetInventoryActive(false);
        SetDetailActive(false);
    }

    private void SetPanelActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
    }

    private void SetDetailActive(bool value)
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(value);
        }
    }

    private void SetInventoryActive(bool value)
    {
        if (inventoryPanelRoot != null)
        {
            inventoryPanelRoot.SetActive(value);
        }
    }

    private void SetWeaponDetailActive(bool value)
    {
        SetComponentGameObjectActive(equipmentHeaderText, value);
        SetComponentGameObjectActive(equipmentKindText, value);
        SetComponentGameObjectActive(equipmentSkillText, value);
        SetComponentGameObjectActive(equipmentDetailText, value);
        SetComponentGameObjectActive(selectedWeaponIconImage, value);
        SetComponentGameObjectActive(helpText, value);

        if (!value)
        {
            SetComponentGameObjectActive(weaponDetailEquipButton, false);
            SetComponentGameObjectActive(weaponDetailUnequipButton, false);
        }
    }

    private void SetAlreadyEquippedPopupActive(bool value)
    {
        if (alreadyEquippedPanelRoot != null)
        {
            alreadyEquippedPanelRoot.SetActive(value);
        }

        SetComponentGameObjectActive(alreadyEquippedText, value);
        SetComponentGameObjectActive(alreadyEquippedEquipButton, value);
        SetComponentGameObjectActive(alreadyEquippedCancelButton, value);

        if (selectedWeaponIconImage != null)
        {
            selectedWeaponIconImage.gameObject.SetActive(!value && IsWeaponDetailActive());
        }
    }

    private static void SetComponentGameObjectActive(Component component, bool value)
    {
        if (component != null)
        {
            component.gameObject.SetActive(value);
        }
    }

    private static void SetSlotVisual(Button slotButton, Image overlayImage, Sprite icon)
    {
        if (slotButton != null)
        {
            slotButton.interactable = true;
        }

        if (overlayImage != null)
        {
            overlayImage.sprite = icon;
            overlayImage.enabled = icon != null;
            overlayImage.preserveAspect = true;
            overlayImage.raycastTarget = false;
        }
    }

    private static void SetWeaponSlotVisual(
        Button slotButton,
        Image overlayImage,
        WeaponModelPreviewView previewView,
        OwnedWeaponData weapon
    )
    {
        if (slotButton != null)
        {
            slotButton.interactable = true;
        }

        SetWeaponPreview(overlayImage, previewView, weapon, weapon?.Weapon?.icon);
    }

    private static void SetWeaponPreview(
        Image fallbackImage,
        WeaponModelPreviewView previewView,
        OwnedWeaponData weapon,
        Sprite fallbackIcon
    )
    {
        if (previewView != null)
        {
            previewView.Clear();
        }

        SetPassiveImage(fallbackImage, fallbackIcon);
    }

    private static void SetPassiveImage(Image image, Sprite icon)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = icon;
        image.enabled = icon != null;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void ConfigureOverlayImage(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.raycastTarget = false;
        image.enabled = image.sprite != null;
        image.preserveAspect = true;
    }

    private static void ConfigureOverlayImages(Image[] images)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            ConfigureOverlayImage(images[i]);
        }
    }

    private void CacheWeaponPreviewViews()
    {
        if (weaponOverlayPreviewView == null && weaponOverlayImage != null)
        {
            weaponOverlayPreviewView = weaponOverlayImage.GetComponentInChildren<WeaponModelPreviewView>(true);
        }

        if (selectedWeaponPreviewView == null && selectedWeaponIconImage != null)
        {
            selectedWeaponPreviewView = selectedWeaponIconImage.GetComponentInChildren<WeaponModelPreviewView>(true);
        }
    }

    private void ResolveMissingListReferences()
    {
        if (panelRoot == null)
        {
            return;
        }

        Transform root = panelRoot.transform;

        if (gladiatorBackground == null)
        {
            gladiatorBackground = FindChildTransform(root, "GladiatorBackground") as RectTransform;
        }

        if (recentAcquiredSortButton == null)
        {
            recentAcquiredSortButton = FindChildComponent<Button>(root, "SortButton1");
        }

        if (levelSortButton == null)
        {
            levelSortButton = FindChildComponent<Button>(root, "SortButton2");
        }
    }

    private void CaptureSortButtonLayout()
    {
        _sortButtonOriginalParents = new Transform[2];
        _sortButtonOriginalSiblingIndices = new int[2];

        CaptureSortButtonLayout(recentAcquiredSortButton, 0);
        CaptureSortButtonLayout(levelSortButton, 1);
    }

    private void CaptureSortButtonLayout(Button sortButton, int index)
    {
        if (sortButton == null || index < 0 || index >= _sortButtonOriginalParents.Length)
        {
            return;
        }

        Transform buttonTransform = sortButton.transform;
        _sortButtonOriginalParents[index] = buttonTransform.parent;
        _sortButtonOriginalSiblingIndices[index] = buttonTransform.GetSiblingIndex();
    }

    private void EnsureWeaponDetailLoreToggleInput()
    {
        if (equipmentDetailText == null)
        {
            return;
        }

        _weaponDetailLoreToggleInput = equipmentDetailText.GetComponent<WeaponDetailLoreToggleInput>();
        if (_weaponDetailLoreToggleInput == null)
        {
            _weaponDetailLoreToggleInput = equipmentDetailText.gameObject.AddComponent<WeaponDetailLoreToggleInput>();
        }

        _weaponDetailLoreToggleInput.Initialize(ToggleWeaponDetailLore);
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

    private static void BindButtons(Button[] buttons, UnityEngine.Events.UnityAction action)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            BindButton(buttons[i], action);
        }
    }

    private void BindArtifactSlotButtons()
    {
        if (artifactSlotButtons == null)
        {
            return;
        }

        for (int i = 0; i < artifactSlotButtons.Length; i++)
        {
            int slotIndex = i;
            BindButton(artifactSlotButtons[i], () => OnArtifactSlotClicked(slotIndex));
        }
    }

    private void CacheDetailPresetControls()
    {
        if (detailPanelRoot == null)
        {
            return;
        }

        Transform root = detailPanelRoot.transform;

        if (detailBackButton == null)
        {
            detailBackButton = FindChildComponent<Button>(root, "DetailBackButton");
        }

        if (detailBackButton == null)
        {
            detailBackButton = FindChildComponent<Button>(root, "CloseButton");
        }

        if (detailGladiatorIconImage == null)
        {
            detailGladiatorIconImage = FindChildComponent<Image>(root, "DetailIcon");
        }
    }

    private void RefreshArtifactSlots(OwnedGladiatorData gladiator)
    {
        int slotCount = Mathf.Max(GetArrayLength(artifactSlotButtons), GetArrayLength(artifactOverlayImages));

        for (int i = 0; i < slotCount; i++)
        {
            Sprite slotIcon = gladiator.GetEquippedArtifact(i)?.Artifact?.icon;
            Button slotButton =
                artifactSlotButtons != null && i < artifactSlotButtons.Length ? artifactSlotButtons[i] : null;
            Image overlayImage =
                artifactOverlayImages != null && i < artifactOverlayImages.Length ? artifactOverlayImages[i] : null;

            SetSlotVisual(slotButton, overlayImage, slotIcon);
        }
    }

    private static int GetArrayLength<T>(T[] values)
    {
        return values != null ? values.Length : 0;
    }

    private static string BuildGladiatorDetailDescription(OwnedGladiatorData gladiator)
    {
        string personalityName =
            gladiator.Personality != null && !string.IsNullOrWhiteSpace(gladiator.Personality.personalityName)
                ? gladiator.Personality.personalityName
                : "성격 없음";

        return $"<size=64><color=#FFFFFF>{personalityName}</color> <color=#000000>{gladiator.DisplayName}</color></size>\r\n"
            + $"레벨: {gladiator.Level}\r\n"
            + $"경험치: {gladiator.Exp}\r\n"
            + $"충성도: {gladiator.Loyalty}\r\n"
            + $"유지비: {gladiator.Upkeep}\r\n"
            + $"최대체력: {gladiator.CachedMaxHealth:0.##}\r\n"
            + $"공격력: {gladiator.CachedAttack:0.##}\r\n"
            + $"공격속도: {gladiator.CachedAttackSpeed:0.##}\r\n"
            + $"이동속도: {gladiator.CachedMoveSpeed:0.##}\r\n"
            + $"사거리: {gladiator.CachedAttackRange:0.##}";
    }

    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindChildTransform(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    private static T FindChildComponent<T>(Transform parent, string childName)
        where T : Component
    {
        Transform child = FindChildTransform(parent, childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }
}

// Equipment detail text가 실제로 활성화된 동안 Left Alt 입력을 감지해 스탯/lore 표시 전환을 요청한다.
[DisallowMultipleComponent]
public sealed class WeaponDetailLoreToggleInput : MonoBehaviour
{
    private Action _toggleRequested;

    public void Initialize(Action toggleRequested)
    {
        _toggleRequested = toggleRequested;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.leftAltKey.wasPressedThisFrame)
        {
            return;
        }

        Debug.Log("Left Alt 입력 감지됨: 무기 상세 lore 토글 요청");
        _toggleRequested?.Invoke();
    }
}
