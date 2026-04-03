using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GladiatorUIManager : MonoBehaviour
{
    [Header("List Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("List Buttons")]
    [SerializeField] private Button backButton;

    [Header("List Viewer")]
    [SerializeField] private OwnedItemGridViewer gladiatorViewer;

    [Header("List Labels")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text statusText;

    [Header("Detail Layer")]
    [SerializeField] private Button detailMaskButton;
    [SerializeField] private GameObject detailPanelRoot;

    [Header("Detail Text")]
    [SerializeField] private TMP_Text detailText;

    [Header("Detail Gladiator Icon")]
    [SerializeField] private Image detailGladiatorIconImage;

    [Header("Detail Trait Slot")]
    [SerializeField] private Button traitSlotButton;
    [SerializeField] private Image traitOverlayImage;

    [Header("Detail Weapon Slot")]
    [SerializeField] private Button weaponSlotButton;
    [SerializeField] private Image weaponOverlayImage;

    [Header("Detail Perk Slot")]
    [SerializeField] private Button perkSlotButton;
    [SerializeField] private Image perkOverlayImage;

    [Header("Inventory Layer")]
    [SerializeField] private Button inventoryMaskButton;
    [SerializeField] private GameObject inventoryPanelRoot;

    [Header("Inventory Viewer")]
    [SerializeField] private OwnedItemGridViewer inventoryWeaponViewer;

    [Header("Inventory Labels")]
    [SerializeField] private TMP_Text inventoryHeaderText;
    [SerializeField] private TMP_Text inventoryStatusText;

    [Header("Weapon Detail Layer")]
    [SerializeField] private Button weaponDetailMaskButton;
    [SerializeField] private GameObject weaponDetailPanelRoot;
    [SerializeField] private TMP_Text weaponDetailText;
    [SerializeField] private Button weaponDetailBackButton;
    [SerializeField] private Button weaponDetailEquipButton;
    [SerializeField] private Button weaponDetailUnequipButton;

    [Header("Already Equipped Popup")]
    [SerializeField] private Button alreadyEquippedMaskButton;
    [SerializeField] private GameObject alreadyEquippedPanelRoot;
    [SerializeField] private TMP_Text alreadyEquippedText;
    [SerializeField] private Button alreadyEquippedOkButton;

    private readonly List<OwnedItemViewData> _gladiatorViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _weaponViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private OwnedGladiatorData _currentDetailGladiator;
    private OwnedWeaponData _currentSelectedWeapon;
    private bool _initialized;

    public void Initialize(MainFlowManager flow, GladiatorManager gladiatorManager, InventoryManager inventoryManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _gladiatorManager = gladiatorManager;
        _inventoryManager = inventoryManager;

        BindButton(backButton, OnBackClicked);
        BindButton(detailMaskButton, OnDetailMaskClicked);
        BindButton(inventoryMaskButton, OnInventoryMaskClicked);
        BindButton(weaponSlotButton, OnWeaponSlotClicked);

        BindButton(weaponDetailMaskButton, OnWeaponDetailBackClicked);
        BindButton(weaponDetailBackButton, OnWeaponDetailBackClicked);
        BindButton(weaponDetailEquipButton, OnWeaponDetailEquipClicked);
        BindButton(weaponDetailUnequipButton, OnWeaponDetailUnequipClicked);

        BindButton(alreadyEquippedMaskButton, OnAlreadyEquippedOkClicked);
        BindButton(alreadyEquippedOkButton, OnAlreadyEquippedOkClicked);

        ConfigureOverlayImage(detailGladiatorIconImage);
        ConfigureOverlayImage(traitOverlayImage);
        ConfigureOverlayImage(weaponOverlayImage);
        ConfigureOverlayImage(perkOverlayImage);

        SetPanelActive(false);
        SetDetailActive(false);
        SetInventoryActive(false);
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);

        RefreshTexts();

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

        RefreshTexts();
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
            for (int i = 0; i < gladiators.Count; i++)
            {
                OwnedGladiatorData gladiator = gladiators[i];
                if (gladiator == null || gladiator.GladiatorClass == null)
                {
                    continue;
                }

                _gladiatorViewBuffer.Add(new OwnedItemViewData(
                    gladiator.GladiatorClass.icon,
                    gladiator.DisplayName,
                    $"Lv.{gladiator.Level}",
                    string.Empty,
                    gladiator
                ));
            }
        }

        Canvas.ForceUpdateCanvases();
        gladiatorViewer.SetItems(_gladiatorViewBuffer, OnGladiatorCellClicked);

        if (statusText != null)
        {
            statusText.text = $"Owned Gladiators: {_gladiatorViewBuffer.Count}";
        }
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

                OwnedGladiatorData owner = _gladiatorManager != null
                    ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon)
                    : null;

                _weaponViewBuffer.Add(new OwnedItemViewData(
                    weapon.Weapon.icon,
                    weapon.DisplayName,
                    $"Lv.{weapon.Level}",
                    owner != null ? "E" : string.Empty,
                    weapon
                ));
            }
        }

        Canvas.ForceUpdateCanvases();
        inventoryWeaponViewer.SetItems(_weaponViewBuffer, OnInventoryWeaponCellClicked);

        if (inventoryHeaderText != null)
        {
            inventoryHeaderText.text = $"Weapons";
        }

        if (inventoryStatusText != null)
        {
            inventoryStatusText.text = $"Owned Weapons: {_weaponViewBuffer.Count}";
        }
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

        RefreshDetail(gladiator);
        SetDetailActive(true);
        SetInventoryActive(false);
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);

        if (statusText != null)
        {
            statusText.text = $"Selected Gladiator: {gladiator.DisplayName}";
        }
    }

    private void RefreshDetail(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return;
        }

        Sprite gladiatorIcon = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
        Sprite traitIcon = gladiator.Trait != null ? gladiator.Trait.icon : null;
        Sprite weaponIcon = gladiator.EquippedWeapon != null && gladiator.EquippedWeapon.Weapon != null
            ? gladiator.EquippedWeapon.Weapon.icon
            : null;
        Sprite perkIcon = gladiator.EquippedPerk != null ? gladiator.EquippedPerk.icon : null;

        SetPassiveImage(detailGladiatorIconImage, gladiatorIcon);
        SetSlotVisual(traitSlotButton, traitOverlayImage, traitIcon);
        SetSlotVisual(weaponSlotButton, weaponOverlayImage, weaponIcon);
        SetSlotVisual(perkSlotButton, perkOverlayImage, perkIcon);

        if (detailText != null)
        {
            string className = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.className : "(None)";

            detailText.text =
                $"Name: {gladiator.DisplayName}\r\n" +
                $"Level: {gladiator.Level}\r\n" +
                $"Experience: {gladiator.Exp}\r\n" +
                $"Loyalty: {gladiator.Loyalty}\r\n" +
                $"Upkeep: {gladiator.Upkeep}\r\n" +
                $"Health: {gladiator.CurrentHealth:0.##} / {gladiator.CachedMaxHealth:0.##}\r\n" +
                $"Attack: {gladiator.CachedAttack:0.##}\r\n" +
                $"Attack Speed: {gladiator.CachedAttackSpeed:0.##}\r\n" +
                $"Move Speed: {gladiator.CachedMoveSpeed:0.##}\r\n" +
                $"Range: {gladiator.CachedAttackRange:0.##}";
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

    private void OpenWeaponDetail(OwnedWeaponData weapon)
    {
        if (weapon == null || _currentDetailGladiator == null)
        {
            return;
        }

        _currentSelectedWeapon = weapon;
        RefreshWeaponDetail(weapon);
        SetWeaponDetailActive(true);
        SetAlreadyEquippedPopupActive(false);
    }

    private void RefreshWeaponDetail(OwnedWeaponData weapon)
    {
        if (weapon == null)
        {
            return;
        }

        OwnedGladiatorData owner = _gladiatorManager != null
            ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon)
            : null;

        bool equippedByCurrent = owner != null && owner == _currentDetailGladiator;
        string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
        string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";
        string ownerText = owner != null ? owner.DisplayName : "(None)";

        if (weaponDetailText != null)
        {
            weaponDetailText.text =
                $"Name: {weapon.DisplayName}\n" +
                $"Type: {weaponTypeText}\n" +
                $"Skill: {skillName}\n" +
                $"Equipped By: {ownerText}\n" +
                $"Level: {weapon.Level}\n" +
                $"Attack Bonus: {weapon.CachedAttackBonus:0.##}\n" +
                $"Health Bonus: {weapon.CachedHealthBonus:0.##}\n" +
                $"Attack Speed Bonus: {weapon.CachedAttackSpeedBonus:0.##}\n" +
                $"Move Speed Bonus: {weapon.CachedMoveSpeedBonus:0.##}\n" +
                $"Range Bonus: {weapon.CachedAttackRangeBonus:0.##}";
        }

        if (weaponDetailEquipButton != null)
        {
            weaponDetailEquipButton.gameObject.SetActive(!equippedByCurrent);
        }

        if (weaponDetailUnequipButton != null)
        {
            weaponDetailUnequipButton.gameObject.SetActive(equippedByCurrent);
        }
    }

    private void OnWeaponDetailBackClicked()
    {
        CloseWeaponDetail();
    }

    private void OnWeaponDetailEquipClicked()
    {
        if (_currentDetailGladiator == null || _currentSelectedWeapon == null || _gladiatorManager == null)
        {
            return;
        }

        OwnedGladiatorData owner = _gladiatorManager.FindOwnerOfEquippedWeapon(_currentSelectedWeapon);
        if (owner != null && owner != _currentDetailGladiator)
        {
            OpenAlreadyEquippedPopup();
            return;
        }

        string failReason;
        bool succeeded = _gladiatorManager.TryEquipWeapon(_currentDetailGladiator, _currentSelectedWeapon, out failReason);

        if (!succeeded)
        {
            if (!string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning("[GladiatorUIManager] " + failReason, this);
            }
            return;
        }

        RefreshDetail(_currentDetailGladiator);
        RefreshInventoryWeaponViewer();
        CloseWeaponDetail();
    }

    private void OnWeaponDetailUnequipClicked()
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

        RefreshDetail(_currentDetailGladiator);
        RefreshInventoryWeaponViewer();
        CloseWeaponDetail();
    }

    private void OpenAlreadyEquippedPopup()
    {
        if (alreadyEquippedText != null)
        {
            alreadyEquippedText.text = "already equipped to somebody!";
        }

        SetAlreadyEquippedPopupActive(true);
    }

    private void OnAlreadyEquippedOkClicked()
    {
        SetAlreadyEquippedPopupActive(false);
        CloseWeaponDetail();
    }

    private void CloseWeaponDetail()
    {
        _currentSelectedWeapon = null;
        SetWeaponDetailActive(false);
    }

    private void OnWeaponSlotClicked()
    {
        if (_currentDetailGladiator == null)
        {
            return;
        }

        OpenInventoryPanel();
    }

    private void OpenInventoryPanel()
    {
        _currentSelectedWeapon = null;
        SetWeaponDetailActive(false);
        SetAlreadyEquippedPopupActive(false);

        if (_currentDetailGladiator != null)
        {
            RefreshDetail(_currentDetailGladiator);
        }

        RefreshInventoryWeaponViewer();
        SetInventoryActive(true);
    }
    //이미 장착/탈착 직후 RefreshDetail()을 호출하고 있어서 아이콘은 뜬느데 혹시 모르니 
    private void CloseInventoryPanel()
    {
        _currentSelectedWeapon = null;
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

    private void OnDetailMaskClicked()
    {
        CloseDetail();
    }

    private void OnInventoryMaskClicked()
    {
        CloseInventoryPanel();
    }

    private void CloseDetail()
    {
        _currentDetailGladiator = null;
        _currentSelectedWeapon = null;

        SetAlreadyEquippedPopupActive(false);
        SetWeaponDetailActive(false);
        SetInventoryActive(false);
        SetDetailActive(false);

        if (statusText != null)
        {
            int gladiatorCount = _gladiatorManager != null ? _gladiatorManager.GetOwnedGladiatorCount() : 0;
            statusText.text = $"Owned Gladiators: {gladiatorCount}";
        }
    }

    private void RefreshTexts()
    {
        if (headerText != null)
        {
            int gladiatorCount = _gladiatorManager != null ? _gladiatorManager.GetOwnedGladiatorCount() : 0;
            headerText.text = $"Gladiators (Owned: {gladiatorCount})";
        }

        if (statusText != null && string.IsNullOrEmpty(statusText.text))
        {
            statusText.text = "Entered Gladiator Panel";
        }
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
        if (detailMaskButton != null)
        {
            detailMaskButton.gameObject.SetActive(value);
        }

        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(value);
        }
    }

    private void SetInventoryActive(bool value)
    {
        if (inventoryMaskButton != null)
        {
            inventoryMaskButton.gameObject.SetActive(value);
        }

        if (inventoryPanelRoot != null)
        {
            inventoryPanelRoot.SetActive(value);
        }
    }

    private void SetWeaponDetailActive(bool value)
    {
        if (weaponDetailMaskButton != null)
        {
            weaponDetailMaskButton.gameObject.SetActive(value);
        }

        if (weaponDetailPanelRoot != null)
        {
            weaponDetailPanelRoot.SetActive(value);
        }
    }

    private void SetAlreadyEquippedPopupActive(bool value)
    {
        if (alreadyEquippedMaskButton != null)
        {
            alreadyEquippedMaskButton.gameObject.SetActive(value);
        }

        if (alreadyEquippedPanelRoot != null)
        {
            alreadyEquippedPanelRoot.SetActive(value);
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