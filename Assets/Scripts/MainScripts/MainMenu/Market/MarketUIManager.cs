using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MarketUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField]
    private GameObject marketRootPanel;

    [SerializeField]
    private GameObject buyGladiatorPanel;

    [SerializeField]
    private GameObject buyEquipmentPanel;

    [SerializeField]
    private GameObject sellGladiatorPanel;

    [SerializeField]
    private GameObject sellEquipmentPanel;

    [Header("Root Buttons")]
    [SerializeField]
    private Button marketBackButton;

    [SerializeField]
    private Button buyGladiatorButton;

    [SerializeField]
    private Button buyEquipmentButton;

    [SerializeField]
    private Button sellGladiatorButton;

    [SerializeField]
    private Button sellEquipmentButton;

    [Header("Buy Gladiator Panel")]
    [SerializeField]
    private Button buyGladiatorBackButton;

    [SerializeField]
    private TMP_Text currentGoldText;

    [SerializeField]
    private Button[] buyGladiatorSlotButtons;

    [SerializeField]
    private GameObject[] buyGladiatorSlotRoots;

    [SerializeField]
    private Image[] buyGladiatorSlotIcons;

    [SerializeField]
    private TMP_Text[] buyGladiatorSlotTexts;

    [Header("Buy Equipment Panel")]
    [SerializeField]
    private Button buyEquipmentBackButton;

    [SerializeField]
    private TMP_Text buyEquipmentGoldText;

    [SerializeField]
    private Button[] buyEquipmentSlotButtons;

    [SerializeField]
    private GameObject[] buyEquipmentSlotRoots;

    [SerializeField]
    private Image[] buyEquipmentSlotIcons;

    [SerializeField]
    private TMP_Text[] buyEquipmentSlotTexts;

    [Header("Sell Gladiator Panel")]
    [SerializeField]
    private Button sellGladiatorBackButton;

    [SerializeField]
    private OwnedItemGridViewer sellGladiatorViewer;

    [SerializeField]
    private GameObject sellGladiatorConfirmMask;

    [SerializeField]
    private GameObject sellGladiatorConfirmPanel;

    [SerializeField]
    private TMP_Text sellGladiatorConfirmNameText;

    [SerializeField]
    private TMP_Text sellGladiatorConfirmPriceText;

    [SerializeField]
    private Button sellGladiatorConfirmYesButton;

    [SerializeField]
    private Button sellGladiatorConfirmNoButton;

    [Header("Sell Equipment Panel")]
    [SerializeField]
    private Button sellEquipmentBackButton;

    [SerializeField]
    private OwnedItemGridViewer sellEquipmentViewer;

    [SerializeField]
    private GameObject sellEquipmentConfirmMask;

    [SerializeField]
    private GameObject sellEquipmentConfirmPanel;

    [SerializeField]
    private TMP_Text sellEquipmentConfirmNameText;

    [SerializeField]
    private TMP_Text sellEquipmentConfirmPriceText;

    [SerializeField]
    private Button sellEquipmentConfirmYesButton;

    [SerializeField]
    private Button sellEquipmentConfirmNoButton;

    [Header("Cannot Sell Equipped Item")]
    [SerializeField]
    private GameObject cannotSellMask;

    [SerializeField]
    private GameObject cannotSellPanel;

    [SerializeField]
    private TMP_Text cannotSellText;

    [SerializeField]
    private Button cannotSellConfirmButton;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<OwnedItemViewData> _sellGladiatorViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellEquipmentViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private MarketManager _marketManager;
    private ResourceManager _resourceManager;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private bool _initialized;

    private OwnedGladiatorData _pendingSellGladiator;
    private int _pendingSellPrice;

    private OwnedWeaponData _pendingSellWeapon;
    private int _pendingSellWeaponPrice;

    public void Initialize(
        MainFlowManager flow,
        MarketManager marketManager,
        ResourceManager resourceManager,
        GladiatorManager gladiatorManager,
        InventoryManager inventoryManager
    )
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _marketManager = marketManager;
        _resourceManager = resourceManager;
        _gladiatorManager = gladiatorManager;
        _inventoryManager = inventoryManager;

        if (_flow == null)
        {
            Debug.LogError("[MarketUIManager] flow is null.", this);
            return;
        }

        if (_marketManager == null)
        {
            Debug.LogError("[MarketUIManager] marketManager is null.", this);
            return;
        }

        if (_resourceManager == null)
        {
            Debug.LogError("[MarketUIManager] resourceManager is null.", this);
            return;
        }

        if (_gladiatorManager == null)
        {
            Debug.LogError("[MarketUIManager] gladiatorManager is null.", this);
            return;
        }

        if (_inventoryManager == null)
        {
            Debug.LogError("[MarketUIManager] inventoryManager is null.", this);
            return;
        }

        BindButton(marketBackButton, OnMarketBackClicked);
        BindButton(buyGladiatorButton, OnBuyGladiatorClicked);
        BindButton(buyEquipmentButton, OnBuyEquipmentClicked);
        BindButton(sellGladiatorButton, OnSellGladiatorClicked);
        BindButton(sellEquipmentButton, OnSellEquipmentClicked);
        BindButton(buyGladiatorBackButton, OnBuyGladiatorBackClicked);
        BindButton(buyEquipmentBackButton, OnBuyEquipmentBackClicked);
        BindButton(sellGladiatorBackButton, OnSellGladiatorBackClicked);
        BindButton(sellEquipmentBackButton, OnSellEquipmentBackClicked);
        BindButton(sellGladiatorConfirmYesButton, OnSellGladiatorConfirmYesClicked);
        BindButton(sellGladiatorConfirmNoButton, OnSellGladiatorConfirmNoClicked);
        BindButton(sellEquipmentConfirmYesButton, OnSellEquipmentConfirmYesClicked);
        BindButton(sellEquipmentConfirmNoButton, OnSellEquipmentConfirmNoClicked);
        BindButton(cannotSellConfirmButton, OnCannotSellConfirmClicked);

        int gladiatorBuySlotCount = GetBuyGladiatorVisualSlotCount();
        for (int i = 0; i < gladiatorBuySlotCount; i++)
        {
            int capturedIndex = i;

            if (buyGladiatorSlotButtons[i] != null)
            {
                buyGladiatorSlotButtons[i].onClick.RemoveAllListeners();
                buyGladiatorSlotButtons[i].onClick.AddListener(() => OnBuyGladiatorSlotClicked(capturedIndex));
            }

            if (buyGladiatorSlotIcons != null && i < buyGladiatorSlotIcons.Length && buyGladiatorSlotIcons[i] != null)
            {
                buyGladiatorSlotIcons[i].raycastTarget = false;
            }
        }

        int equipmentBuySlotCount = GetBuyEquipmentVisualSlotCount();
        for (int i = 0; i < equipmentBuySlotCount; i++)
        {
            int capturedIndex = i;

            if (buyEquipmentSlotButtons[i] != null)
            {
                buyEquipmentSlotButtons[i].onClick.RemoveAllListeners();
                buyEquipmentSlotButtons[i].onClick.AddListener(() => OnBuyEquipmentSlotClicked(capturedIndex));
            }

            if (buyEquipmentSlotIcons != null && i < buyEquipmentSlotIcons.Length && buyEquipmentSlotIcons[i] != null)
            {
                buyEquipmentSlotIcons[i].raycastTarget = false;
            }
        }

        _resourceManager.GoldChanged += OnGoldChanged;
        RefreshGoldText(_resourceManager.CurrentGold);

        CloseMarket();
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketUIManager] Initialized. "
                    + $"BuyGladiatorSlots={gladiatorBuySlotCount}, BuyEquipmentSlots={equipmentBuySlotCount}",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (_resourceManager != null)
        {
            _resourceManager.GoldChanged -= OnGoldChanged;
        }
    }

    public void OpenMarketHome()
    {
        SetPanelActive(marketRootPanel, true);
        CloseAllSubPanels();
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseCannotSellPanel();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
    }

    public void CloseMarket()
    {
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseCannotSellPanel();
        CloseAllSubPanels();
        SetPanelActive(marketRootPanel, false);
    }

    private void OnMarketBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMarketBackRequested();
        }
    }

    private void OnBuyGladiatorClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, true);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyGladiatorPanel();
    }

    private void OnBuyEquipmentClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, true);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyEquipmentPanel();
    }

    private void OnSellGladiatorClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, true);
        SetPanelActive(sellEquipmentPanel, false);

        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseCannotSellPanel();
        RefreshSellGladiatorPanel();
    }

    private void OnSellEquipmentClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, true);

        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseCannotSellPanel();
        RefreshSellEquipmentPanel();
    }

    private void OnBuyGladiatorBackClicked()
    {
        SetPanelActive(buyGladiatorPanel, false);
    }

    private void OnBuyEquipmentBackClicked()
    {
        SetPanelActive(buyEquipmentPanel, false);
    }

    private void OnSellGladiatorBackClicked()
    {
        CloseSellGladiatorConfirm();
        CloseCannotSellPanel();
        SetPanelActive(sellGladiatorPanel, false);
    }

    private void OnSellEquipmentBackClicked()
    {
        CloseSellEquipmentConfirm();
        CloseCannotSellPanel();
        SetPanelActive(sellEquipmentPanel, false);
    }

    private void OnBuyGladiatorSlotClicked(int slotIndex)
    {
        if (_marketManager == null)
        {
            Debug.LogError("[MarketUIManager] marketManager is null.", this);
            return;
        }

        string failReason = string.Empty;
        bool purchaseSucceeded = _marketManager.TryBuyGladiator(slotIndex, out failReason);

        if (!purchaseSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyGladiatorPanel();
    }

    private void OnBuyEquipmentSlotClicked(int slotIndex)
    {
        if (_marketManager == null)
        {
            Debug.LogError("[MarketUIManager] marketManager is null.", this);
            return;
        }

        string failReason = string.Empty;
        bool purchaseSucceeded = _marketManager.TryBuyWeapon(slotIndex, out failReason);

        if (!purchaseSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyEquipmentPanel();
    }

    private void RefreshBuyGladiatorPanel()
    {
        int visualSlotCount = GetBuyGladiatorVisualSlotCount();

        for (int i = 0; i < visualSlotCount; i++)
        {
            MarketGladiatorOffer offer = _marketManager != null ? _marketManager.GetGladiatorOffer(i) : null;
            bool shouldShow = offer != null && offer.IsAvailable;

            SetBuyGladiatorSlotVisible(i, shouldShow);

            if (!shouldShow)
            {
                ClearBuyGladiatorSlot(i);
                continue;
            }

            OwnedGladiatorData gladiator = offer.Gladiator;

            if (buyGladiatorSlotIcons != null && i < buyGladiatorSlotIcons.Length && buyGladiatorSlotIcons[i] != null)
            {
                Sprite icon =
                    gladiator != null && gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;

                buyGladiatorSlotIcons[i].sprite = icon;
                buyGladiatorSlotIcons[i].enabled = icon != null;
            }

            if (buyGladiatorSlotTexts != null && i < buyGladiatorSlotTexts.Length && buyGladiatorSlotTexts[i] != null)
            {
                buyGladiatorSlotTexts[i].text = BuildBuyGladiatorText(offer);
            }

            if (
                buyGladiatorSlotButtons != null
                && i < buyGladiatorSlotButtons.Length
                && buyGladiatorSlotButtons[i] != null
            )
            {
                buyGladiatorSlotButtons[i].interactable = true;
            }
        }
    }

    private void RefreshBuyEquipmentPanel()
    {
        int visualSlotCount = GetBuyEquipmentVisualSlotCount();

        for (int i = 0; i < visualSlotCount; i++)
        {
            MarketWeaponOffer offer = _marketManager != null ? _marketManager.GetWeaponOffer(i) : null;
            bool shouldShow = offer != null && offer.IsAvailable;

            SetBuyEquipmentSlotVisible(i, shouldShow);

            if (!shouldShow)
            {
                ClearBuyEquipmentSlot(i);
                continue;
            }

            OwnedWeaponData weapon = offer.Weapon;

            if (buyEquipmentSlotIcons != null && i < buyEquipmentSlotIcons.Length && buyEquipmentSlotIcons[i] != null)
            {
                Sprite icon = weapon != null && weapon.Weapon != null ? weapon.Weapon.icon : null;

                buyEquipmentSlotIcons[i].sprite = icon;
                buyEquipmentSlotIcons[i].enabled = icon != null;
            }

            if (buyEquipmentSlotTexts != null && i < buyEquipmentSlotTexts.Length && buyEquipmentSlotTexts[i] != null)
            {
                buyEquipmentSlotTexts[i].text = BuildBuyEquipmentText(offer);
            }

            if (
                buyEquipmentSlotButtons != null
                && i < buyEquipmentSlotButtons.Length
                && buyEquipmentSlotButtons[i] != null
            )
            {
                buyEquipmentSlotButtons[i].interactable = true;
            }
        }
    }

    private void RefreshSellGladiatorPanel()
    {
        if (sellGladiatorViewer == null)
        {
            Debug.LogWarning("[MarketUIManager] sellGladiatorViewer is not assigned.", this);
            return;
        }

        _sellGladiatorViewBuffer.Clear();

        if (_gladiatorManager != null)
        {
            IReadOnlyList<OwnedGladiatorData> ownedGladiators = _gladiatorManager.OwnedGladiators;
            for (int i = 0; i < ownedGladiators.Count; i++)
            {
                OwnedGladiatorData gladiator = ownedGladiators[i];
                if (gladiator == null)
                {
                    continue;
                }

                Sprite icon = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
                _sellGladiatorViewBuffer.Add(
                    new OwnedItemViewData(icon, gladiator.DisplayName, $"Lv.{gladiator.Level}", string.Empty, gladiator)
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        sellGladiatorViewer.SetItems(_sellGladiatorViewBuffer, OnSellGladiatorCellClicked);
    }

    private void RefreshSellEquipmentPanel()
    {
        if (sellEquipmentViewer == null)
        {
            Debug.LogWarning("[MarketUIManager] sellEquipmentViewer is not assigned.", this);
            return;
        }

        _sellEquipmentViewBuffer.Clear();

        if (_inventoryManager != null)
        {
            IReadOnlyList<OwnedWeaponData> ownedWeapons = _inventoryManager.OwnedWeapons;
            for (int i = 0; i < ownedWeapons.Count; i++)
            {
                OwnedWeaponData weapon = ownedWeapons[i];
                if (weapon == null)
                {
                    continue;
                }

                Sprite icon = weapon.Weapon != null ? weapon.Weapon.icon : null;
                OwnedGladiatorData owner =
                    _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null;

                _sellEquipmentViewBuffer.Add(
                    new OwnedItemViewData(
                        icon,
                        weapon.DisplayName,
                        $"Lv.{weapon.Level}",
                        owner != null ? "E" : string.Empty,
                        weapon
                    )
                );
            }
        }

        Canvas.ForceUpdateCanvases();
        sellEquipmentViewer.SetItems(_sellEquipmentViewBuffer, OnSellEquipmentCellClicked);
    }

    private void OnSellGladiatorCellClicked(OwnedItemViewData data)
    {
        OwnedGladiatorData gladiator = data.Source as OwnedGladiatorData;
        if (gladiator == null)
        {
            return;
        }

        _pendingSellGladiator = gladiator;
        _pendingSellPrice = _marketManager != null ? _marketManager.GetGladiatorSellPrice(gladiator) : 0;

        if (sellGladiatorConfirmNameText != null)
        {
            sellGladiatorConfirmNameText.text =
                $"Name: {gladiator.DisplayName}\n"
                + $"Level: {gladiator.Level}\n"
                + $"Health: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
                + $"Attack: {Mathf.FloorToInt(gladiator.CachedAttack)}";
        }

        if (sellGladiatorConfirmPriceText != null)
        {
            sellGladiatorConfirmPriceText.text = $"Price: {_pendingSellPrice}";
        }

        SetPanelActive(sellGladiatorConfirmMask, true);
        SetPanelActive(sellGladiatorConfirmPanel, true);
    }

    //장착된 무기는 클릭 시 판매 금지 패널 팝업
    private void OnSellEquipmentCellClicked(OwnedItemViewData data)
    {
        OwnedWeaponData weapon = data.Source as OwnedWeaponData;
        if (weapon == null)
        {
            return;
        }

        OwnedGladiatorData owner =
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null;

        if (owner != null)
        {
            OpenCannotSellPanel();
            return;
        }

        _pendingSellWeapon = weapon;
        _pendingSellWeaponPrice = _marketManager != null ? _marketManager.GetWeaponSellPrice(weapon) : 0;

        if (sellEquipmentConfirmNameText != null)
        {
            string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
            string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";

            sellEquipmentConfirmNameText.text =
                $"Name: {weapon.DisplayName}\n"
                + $"Type: {weaponTypeText}\n"
                + $"Skill: {skillName}\n"
                + $"Level: {weapon.Level}\n"
                + $"Attack Bonus: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
                + $"Health Bonus: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
                + $"Attack Speed Bonus: {weapon.CachedAttackSpeedBonus:0.##}\n"
                + $"Move Speed Bonus: {weapon.CachedMoveSpeedBonus:0.##}\n"
                + $"Range Bonus: {weapon.CachedAttackRangeBonus:0.##}";
        }

        if (sellEquipmentConfirmPriceText != null)
        {
            sellEquipmentConfirmPriceText.text = $"Price: {_pendingSellWeaponPrice}";
        }

        SetPanelActive(sellEquipmentConfirmMask, true);
        SetPanelActive(sellEquipmentConfirmPanel, true);
    }

    private void OnSellGladiatorConfirmYesClicked()
    {
        if (_pendingSellGladiator == null)
        {
            CloseSellGladiatorConfirm();
            return;
        }

        string failReason = string.Empty;
        int soldPrice = 0;
        bool sellSucceeded = false;

        if (_marketManager != null)
        {
            sellSucceeded = _marketManager.TrySellGladiator(_pendingSellGladiator, out soldPrice, out failReason);
        }

        if (!sellSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        CloseSellGladiatorConfirm();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshSellGladiatorPanel();
    }

    //이중 방어
    private void OnSellEquipmentConfirmYesClicked()
    {
        if (_pendingSellWeapon == null)
        {
            CloseSellEquipmentConfirm();
            return;
        }

        string failReason = string.Empty;
        int soldPrice = 0;
        bool sellSucceeded = false;

        if (_marketManager != null)
        {
            sellSucceeded = _marketManager.TrySellWeapon(_pendingSellWeapon, out soldPrice, out failReason);
        }

        if (!sellSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);

            if (failReason == "You can't sell equipped items.")
            {
                CloseSellEquipmentConfirm();
                OpenCannotSellPanel();
                RefreshSellEquipmentPanel();
                return;
            }
        }

        CloseSellEquipmentConfirm();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshSellEquipmentPanel();
    }

    private void OnSellGladiatorConfirmNoClicked()
    {
        CloseSellGladiatorConfirm();
    }

    private void OnSellEquipmentConfirmNoClicked()
    {
        CloseSellEquipmentConfirm();
    }

    private void CloseSellGladiatorConfirm()
    {
        _pendingSellGladiator = null;
        _pendingSellPrice = 0;

        SetPanelActive(sellGladiatorConfirmMask, false);
        SetPanelActive(sellGladiatorConfirmPanel, false);
    }

    private void CloseSellEquipmentConfirm()
    {
        _pendingSellWeapon = null;
        _pendingSellWeaponPrice = 0;

        SetPanelActive(sellEquipmentConfirmMask, false);
        SetPanelActive(sellEquipmentConfirmPanel, false);
    }

    private void OpenCannotSellPanel()
    {
        if (cannotSellText != null)
        {
            cannotSellText.text = "you can't sell equipped items";
        }

        SetPanelActive(cannotSellMask, true);
        SetPanelActive(cannotSellPanel, true);
    }

    private void CloseCannotSellPanel()
    {
        SetPanelActive(cannotSellMask, false);
        SetPanelActive(cannotSellPanel, false);
    }

    private void OnCannotSellConfirmClicked()
    {
        CloseCannotSellPanel();
    }

    private string BuildBuyGladiatorText(MarketGladiatorOffer offer)
    {
        if (offer == null || offer.Gladiator == null)
        {
            return string.Empty;
        }

        OwnedGladiatorData gladiator = offer.Gladiator;

        return $"Price: {offer.Price}\n"
            + $"Name: {gladiator.DisplayName}\n"
            + $"Level: {gladiator.Level}\n"
            + $"Loyalty: {gladiator.Loyalty}\n"
            + $"Upkeep: {gladiator.Upkeep}\n"
            + $"Health: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
            + $"Attack: {Mathf.FloorToInt(gladiator.CachedAttack)}\n"
            + $"Attack Speed: {gladiator.CachedAttackSpeed:0.##}\n"
            + $"Move Speed: {gladiator.CachedMoveSpeed:0.##}\n"
            + $"Range: {gladiator.CachedAttackRange:0.##}";
    }

    private string BuildBuyEquipmentText(MarketWeaponOffer offer)
    {
        if (offer == null || offer.Weapon == null)
        {
            return string.Empty;
        }

        OwnedWeaponData weapon = offer.Weapon;
        string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
        string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";

        return $"Price: {offer.Price}\n"
            + $"Name: {weapon.DisplayName}\n"
            + $"Type: {weaponTypeText}\n"
            + $"Skill: {skillName}\n"
            + $"Level: {weapon.Level}\n"
            + $"Attack Bonus: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
            + $"Health Bonus: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
            + $"Attack Speed Bonus: {weapon.CachedAttackSpeedBonus:0.##}\n"
            + $"Move Speed Bonus: {weapon.CachedMoveSpeedBonus:0.##}\n"
            + $"Range Bonus: {weapon.CachedAttackRangeBonus:0.##}";
    }

    private void ClearBuyGladiatorSlot(int slotIndex)
    {
        if (
            buyGladiatorSlotIcons != null
            && slotIndex < buyGladiatorSlotIcons.Length
            && buyGladiatorSlotIcons[slotIndex] != null
        )
        {
            buyGladiatorSlotIcons[slotIndex].sprite = null;
            buyGladiatorSlotIcons[slotIndex].enabled = false;
        }

        if (
            buyGladiatorSlotTexts != null
            && slotIndex < buyGladiatorSlotTexts.Length
            && buyGladiatorSlotTexts[slotIndex] != null
        )
        {
            buyGladiatorSlotTexts[slotIndex].text = string.Empty;
        }

        if (
            buyGladiatorSlotButtons != null
            && slotIndex < buyGladiatorSlotButtons.Length
            && buyGladiatorSlotButtons[slotIndex] != null
        )
        {
            buyGladiatorSlotButtons[slotIndex].interactable = false;
        }
    }

    private void ClearBuyEquipmentSlot(int slotIndex)
    {
        if (
            buyEquipmentSlotIcons != null
            && slotIndex < buyEquipmentSlotIcons.Length
            && buyEquipmentSlotIcons[slotIndex] != null
        )
        {
            buyEquipmentSlotIcons[slotIndex].sprite = null;
            buyEquipmentSlotIcons[slotIndex].enabled = false;
        }

        if (
            buyEquipmentSlotTexts != null
            && slotIndex < buyEquipmentSlotTexts.Length
            && buyEquipmentSlotTexts[slotIndex] != null
        )
        {
            buyEquipmentSlotTexts[slotIndex].text = string.Empty;
        }

        if (
            buyEquipmentSlotButtons != null
            && slotIndex < buyEquipmentSlotButtons.Length
            && buyEquipmentSlotButtons[slotIndex] != null
        )
        {
            buyEquipmentSlotButtons[slotIndex].interactable = false;
        }
    }

    private void SetBuyGladiatorSlotVisible(int slotIndex, bool value)
    {
        if (
            buyGladiatorSlotRoots != null
            && buyGladiatorSlotRoots.Length > 0
            && slotIndex < buyGladiatorSlotRoots.Length
            && buyGladiatorSlotRoots[slotIndex] != null
        )
        {
            buyGladiatorSlotRoots[slotIndex].SetActive(value);
            return;
        }

        if (
            buyGladiatorSlotButtons != null
            && slotIndex < buyGladiatorSlotButtons.Length
            && buyGladiatorSlotButtons[slotIndex] != null
        )
        {
            buyGladiatorSlotButtons[slotIndex].gameObject.SetActive(value);
        }
    }

    private void SetBuyEquipmentSlotVisible(int slotIndex, bool value)
    {
        if (
            buyEquipmentSlotRoots != null
            && buyEquipmentSlotRoots.Length > 0
            && slotIndex < buyEquipmentSlotRoots.Length
            && buyEquipmentSlotRoots[slotIndex] != null
        )
        {
            buyEquipmentSlotRoots[slotIndex].SetActive(value);
            return;
        }

        if (
            buyEquipmentSlotButtons != null
            && slotIndex < buyEquipmentSlotButtons.Length
            && buyEquipmentSlotButtons[slotIndex] != null
        )
        {
            buyEquipmentSlotButtons[slotIndex].gameObject.SetActive(value);
        }
    }

    private int GetBuyGladiatorVisualSlotCount()
    {
        if (buyGladiatorSlotButtons == null)
        {
            return 0;
        }

        int count = buyGladiatorSlotButtons.Length;

        if (buyGladiatorSlotIcons != null && buyGladiatorSlotIcons.Length > 0)
        {
            count = Mathf.Min(count, buyGladiatorSlotIcons.Length);
        }

        if (buyGladiatorSlotTexts != null && buyGladiatorSlotTexts.Length > 0)
        {
            count = Mathf.Min(count, buyGladiatorSlotTexts.Length);
        }

        if (buyGladiatorSlotRoots != null && buyGladiatorSlotRoots.Length > 0)
        {
            count = Mathf.Min(count, buyGladiatorSlotRoots.Length);
        }

        return count;
    }

    private int GetBuyEquipmentVisualSlotCount()
    {
        if (buyEquipmentSlotButtons == null)
        {
            return 0;
        }

        int count = buyEquipmentSlotButtons.Length;

        if (buyEquipmentSlotIcons != null && buyEquipmentSlotIcons.Length > 0)
        {
            count = Mathf.Min(count, buyEquipmentSlotIcons.Length);
        }

        if (buyEquipmentSlotTexts != null && buyEquipmentSlotTexts.Length > 0)
        {
            count = Mathf.Min(count, buyEquipmentSlotTexts.Length);
        }

        if (buyEquipmentSlotRoots != null && buyEquipmentSlotRoots.Length > 0)
        {
            count = Mathf.Min(count, buyEquipmentSlotRoots.Length);
        }

        return count;
    }

    private void OnGoldChanged(int currentGold)
    {
        RefreshGoldText(currentGold);
    }

    private void RefreshGoldText(int currentGold)
    {
        if (currentGoldText != null)
        {
            currentGoldText.text = $"Gold: {currentGold}";
        }

        if (buyEquipmentGoldText != null)
        {
            buyEquipmentGoldText.text = $"Gold: {currentGold}";
        }
    }

    private void CloseAllSubPanels()
    {
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
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

    private static void SetPanelActive(GameObject panel, bool value)
    {
        if (panel != null)
        {
            panel.SetActive(value);
        }
    }
}
