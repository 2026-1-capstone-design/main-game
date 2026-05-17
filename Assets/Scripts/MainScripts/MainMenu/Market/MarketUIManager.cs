using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MarketUIManager : MonoBehaviour
{
    private enum TradeMode
    {
        None = 0,
        Buy = 1,
        Sell = 2,
    }

    private enum MarketCategory
    {
        Equipment = 0,
        Artifact = 1,
        Gladiator = 2,
    }

    [Header("Panels")]
    [SerializeField]
    private GameObject marketRootPanel;

    [SerializeField]
    private GameObject buyModePanel;

    [SerializeField]
    private GameObject sellModePanel;

    [Header("Root Buttons")]
    [SerializeField]
    private Button marketBackButton;

    [SerializeField]
    private Button buyModeButton;

    [SerializeField]
    private Button sellModeButton;

    [Header("Mode Back Buttons")]
    [SerializeField]
    private Button buyModeBackButton;

    [SerializeField]
    private Button sellModeBackButton;

    [Header("Buy Category Buttons")]
    [SerializeField]
    private Button buyEquipmentButton;

    [SerializeField]
    private Button buyArtifactButton;

    [SerializeField]
    private Button buyGladiatorButton;

    [Header("Sell Category Buttons")]
    [SerializeField]
    private Button sellEquipmentButton;

    [SerializeField]
    private Button sellArtifactButton;

    [SerializeField]
    private Button sellGladiatorButton;

    [Header("Trade Buttons")]
    [SerializeField]
    private Button buySelectedButton;

    [SerializeField]
    private Button sellSelectedButton;

    [Header("Buy Viewers")]
    [SerializeField]
    private OwnedItemGridViewer buyEquipmentViewer;

    [SerializeField]
    private OwnedItemGridViewer buyArtifactViewer;

    [SerializeField]
    private OwnedItemGridViewer buyGladiatorViewer;

    [Header("Sell Viewers")]
    [SerializeField]
    private OwnedItemGridViewer sellEquipmentViewer;

    [SerializeField]
    private OwnedItemGridViewer sellArtifactViewer;

    [SerializeField]
    private OwnedItemGridViewer sellGladiatorViewer;

    [Header("Buy Detail")]
    [SerializeField]
    private TMP_Text buyGoldText;

    [SerializeField]
    private TMP_Text buyDescriptionText;

    [SerializeField]
    private TMP_Text buyPriceText;

    [SerializeField]
    private TMP_Text buyBalanceAfterTradeText;

    [Header("Sell Detail")]
    [SerializeField]
    private TMP_Text sellGoldText;

    [SerializeField]
    private TMP_Text sellDescriptionText;

    [SerializeField]
    private TMP_Text sellPriceText;

    [SerializeField]
    private TMP_Text sellBalanceAfterTradeText;

    [Header("Cannot Sell Equipped Item")]
    [SerializeField]
    private GameObject cannotSellPanel;

    [SerializeField]
    private TMP_Text cannotSellText;

    [SerializeField]
    private Button cannotSellConfirmButton;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<OwnedItemViewData> _buyEquipmentViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _buyArtifactViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _buyGladiatorViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellEquipmentViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellArtifactViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellGladiatorViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private MarketManager _marketManager;
    private ResourceManager _resourceManager;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private bool _initialized;

    private TradeMode _tradeMode = TradeMode.None;
    private MarketCategory _currentCategory = MarketCategory.Equipment;

    private MarketGladiatorOffer _pendingBuyGladiatorOffer;
    private MarketWeaponOffer _pendingBuyWeaponOffer;
    private MarketArtifactOffer _pendingBuyArtifactOffer;
    private OwnedGladiatorData _pendingSellGladiator;
    private OwnedWeaponData _pendingSellWeapon;
    private OwnedArtifactData _pendingSellArtifact;

    public void Initialize(
        MainFlowManager flow,
        MarketManager marketManager,
        ResourceManager resourceManager,
        GladiatorManager gladiatorManager,
        InventoryManager inventoryManager,
        ResearchManager researchManager
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

        if (!ValidateDependencies())
        {
            return;
        }

        BindButton(marketBackButton, OnMarketBackClicked);
        BindButton(buyModeButton, OnBuyModeClicked);
        BindButton(sellModeButton, OnSellModeClicked);
        BindButton(buyModeBackButton, OnModeBackClicked);
        BindButton(sellModeBackButton, OnModeBackClicked);
        BindButton(buyEquipmentButton, OnBuyEquipmentClicked);
        BindButton(buyArtifactButton, OnBuyArtifactClicked);
        BindButton(buyGladiatorButton, OnBuyGladiatorClicked);
        BindButton(sellEquipmentButton, OnSellEquipmentClicked);
        BindButton(sellArtifactButton, OnSellArtifactClicked);
        BindButton(sellGladiatorButton, OnSellGladiatorClicked);
        BindButton(buySelectedButton, OnBuySelectedClicked);
        BindButton(sellSelectedButton, OnSellSelectedClicked);
        BindButton(cannotSellConfirmButton, OnCannotSellConfirmClicked);

        _resourceManager.GoldChanged += OnGoldChanged;
        CacheCannotSellRefsIfNull();
        CloseMarket();
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[MarketUIManager] Initialized with grid-based market panels.", this);
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
        _tradeMode = TradeMode.None;
        ClearPendingSelections();
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyModePanel, false);
        SetPanelActive(sellModePanel, false);
        CloseCannotSellPanel();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
    }

    public void CloseMarket()
    {
        _tradeMode = TradeMode.None;
        ClearPendingSelections();
        CloseCannotSellPanel();
        SetPanelActive(buyModePanel, false);
        SetPanelActive(sellModePanel, false);
        SetPanelActive(marketRootPanel, false);
    }

    private bool ValidateDependencies()
    {
        if (_flow == null)
        {
            Debug.LogError("[MarketUIManager] flow is null.", this);
            return false;
        }

        if (_marketManager == null)
        {
            Debug.LogError("[MarketUIManager] marketManager is null.", this);
            return false;
        }

        if (_resourceManager == null)
        {
            Debug.LogError("[MarketUIManager] resourceManager is null.", this);
            return false;
        }

        if (_gladiatorManager == null)
        {
            Debug.LogError("[MarketUIManager] gladiatorManager is null.", this);
            return false;
        }

        if (_inventoryManager == null)
        {
            Debug.LogError("[MarketUIManager] inventoryManager is null.", this);
            return false;
        }

        return true;
    }

    private void OnMarketBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleMarketBackRequested();
        }
    }

    private void OnBuyModeClicked()
    {
        OpenBuyModePanel(MarketCategory.Equipment);
    }

    private void OnSellModeClicked()
    {
        OpenSellModePanel(MarketCategory.Equipment);
    }

    private void OnModeBackClicked()
    {
        OpenMarketHome();
    }

    private void OnBuyEquipmentClicked()
    {
        OpenBuyModePanel(MarketCategory.Equipment);
    }

    private void OnBuyArtifactClicked()
    {
        OpenBuyModePanel(MarketCategory.Artifact);
    }

    private void OnBuyGladiatorClicked()
    {
        OpenBuyModePanel(MarketCategory.Gladiator);
    }

    private void OnSellEquipmentClicked()
    {
        OpenSellModePanel(MarketCategory.Equipment);
    }

    private void OnSellArtifactClicked()
    {
        OpenSellModePanel(MarketCategory.Artifact);
    }

    private void OnSellGladiatorClicked()
    {
        OpenSellModePanel(MarketCategory.Gladiator);
    }

    private void OpenBuyModePanel(MarketCategory category)
    {
        _tradeMode = TradeMode.Buy;
        _currentCategory = category;
        ClearPendingSelections();
        CloseCannotSellPanel();
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyModePanel, true);
        SetPanelActive(sellModePanel, false);
        ApplyViewerVisibility();
        RefreshGoldText(_resourceManager.CurrentGold);
        RefreshCurrentCategoryViewer();
        ClearBuyDetail();
    }

    private void OpenSellModePanel(MarketCategory category)
    {
        _tradeMode = TradeMode.Sell;
        _currentCategory = category;
        ClearPendingSelections();
        CloseCannotSellPanel();
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyModePanel, false);
        SetPanelActive(sellModePanel, true);
        ApplyViewerVisibility();
        RefreshGoldText(_resourceManager.CurrentGold);
        RefreshCurrentCategoryViewer();
        ClearSellDetail();
    }

    private void ApplyViewerVisibility()
    {
        SetViewerActive(
            buyEquipmentViewer,
            _tradeMode == TradeMode.Buy && _currentCategory == MarketCategory.Equipment
        );
        SetViewerActive(buyArtifactViewer, _tradeMode == TradeMode.Buy && _currentCategory == MarketCategory.Artifact);
        SetViewerActive(
            buyGladiatorViewer,
            _tradeMode == TradeMode.Buy && _currentCategory == MarketCategory.Gladiator
        );
        SetViewerActive(
            sellEquipmentViewer,
            _tradeMode == TradeMode.Sell && _currentCategory == MarketCategory.Equipment
        );
        SetViewerActive(
            sellArtifactViewer,
            _tradeMode == TradeMode.Sell && _currentCategory == MarketCategory.Artifact
        );
        SetViewerActive(
            sellGladiatorViewer,
            _tradeMode == TradeMode.Sell && _currentCategory == MarketCategory.Gladiator
        );
    }

    private void RefreshCurrentCategoryViewer()
    {
        if (_tradeMode == TradeMode.Buy)
        {
            if (_currentCategory == MarketCategory.Equipment)
            {
                RefreshBuyEquipmentViewer();
            }
            else if (_currentCategory == MarketCategory.Artifact)
            {
                RefreshBuyArtifactViewer();
            }
            else
            {
                RefreshBuyGladiatorViewer();
            }
        }
        else if (_tradeMode == TradeMode.Sell)
        {
            if (_currentCategory == MarketCategory.Equipment)
            {
                RefreshSellEquipmentViewer();
            }
            else if (_currentCategory == MarketCategory.Artifact)
            {
                RefreshSellArtifactViewer();
            }
            else
            {
                RefreshSellGladiatorViewer();
            }
        }
    }

    private void RefreshBuyEquipmentViewer()
    {
        _buyEquipmentViewBuffer.Clear();

        if (_marketManager != null)
        {
            IReadOnlyList<MarketWeaponOffer> offers = _marketManager.WeaponOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                MarketWeaponOffer offer = offers[i];
                if (offer == null || !offer.IsAvailable || offer.Weapon == null)
                {
                    continue;
                }

                Sprite icon = offer.Weapon.Weapon != null ? offer.Weapon.Weapon.icon : null;
                _buyEquipmentViewBuffer.Add(
                    new OwnedItemViewData(icon, offer.Weapon.DisplayName, string.Empty, string.Empty, offer)
                );
            }
        }

        if (buyEquipmentViewer != null)
        {
            buyEquipmentViewer.SetItems(_buyEquipmentViewBuffer, OnBuyEquipmentItemClicked);
        }
    }

    private void RefreshBuyArtifactViewer()
    {
        _buyArtifactViewBuffer.Clear();

        if (_marketManager != null)
        {
            IReadOnlyList<MarketArtifactOffer> offers = _marketManager.ArtifactOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                MarketArtifactOffer offer = offers[i];
                if (offer == null || !offer.IsAvailable || offer.Artifact == null)
                {
                    continue;
                }

                _buyArtifactViewBuffer.Add(
                    new OwnedItemViewData(
                        offer.Artifact.icon,
                        offer.Artifact.artifactName,
                        string.Empty,
                        string.Empty,
                        offer
                    )
                );
            }
        }

        if (buyArtifactViewer != null)
        {
            buyArtifactViewer.SetItems(_buyArtifactViewBuffer, OnBuyArtifactItemClicked);
        }
    }

    private void RefreshBuyGladiatorViewer()
    {
        _buyGladiatorViewBuffer.Clear();

        if (_marketManager != null)
        {
            IReadOnlyList<MarketGladiatorOffer> offers = _marketManager.GladiatorOffers;
            for (int i = 0; i < offers.Count; i++)
            {
                MarketGladiatorOffer offer = offers[i];
                if (offer == null || !offer.IsAvailable || offer.Gladiator == null)
                {
                    continue;
                }

                Sprite icon = offer.Gladiator.GladiatorClass != null ? offer.Gladiator.GladiatorClass.icon : null;
                _buyGladiatorViewBuffer.Add(
                    new OwnedItemViewData(icon, offer.Gladiator.DisplayName, string.Empty, string.Empty, offer)
                );
            }
        }

        if (buyGladiatorViewer != null)
        {
            buyGladiatorViewer.SetItems(_buyGladiatorViewBuffer, OnBuyGladiatorItemClicked);
        }
    }

    private void RefreshSellEquipmentViewer()
    {
        _sellEquipmentViewBuffer.Clear();

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

                int price = _marketManager != null ? _marketManager.GetWeaponSellPrice(weapon) : 0;
                string equippedMark =
                    _gladiatorManager != null && _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) != null
                        ? "E"
                        : string.Empty;
                Sprite icon = weapon.Weapon != null ? weapon.Weapon.icon : null;
                _sellEquipmentViewBuffer.Add(
                    new OwnedItemViewData(icon, weapon.DisplayName, string.Empty, equippedMark, weapon)
                );
            }
        }

        if (sellEquipmentViewer != null)
        {
            sellEquipmentViewer.SetItems(_sellEquipmentViewBuffer, OnSellEquipmentItemClicked);
        }
    }

    private void RefreshSellArtifactViewer()
    {
        _sellArtifactViewBuffer.Clear();

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

                int price = _marketManager != null ? _marketManager.GetArtifactSellPrice() : 0;
                string equippedMark =
                    _gladiatorManager != null && _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) != null
                        ? "E"
                        : string.Empty;
                _sellArtifactViewBuffer.Add(
                    new OwnedItemViewData(
                        artifact.Artifact.icon,
                        artifact.DisplayName,
                        string.Empty,
                        equippedMark,
                        artifact
                    )
                );
            }
        }

        if (sellArtifactViewer != null)
        {
            sellArtifactViewer.SetItems(_sellArtifactViewBuffer, OnSellArtifactItemClicked);
        }
    }

    private void RefreshSellGladiatorViewer()
    {
        _sellGladiatorViewBuffer.Clear();

        if (_gladiatorManager != null)
        {
            IReadOnlyList<OwnedGladiatorData> gladiators = _gladiatorManager.OwnedGladiators;
            for (int i = 0; i < gladiators.Count; i++)
            {
                OwnedGladiatorData gladiator = gladiators[i];
                if (gladiator == null)
                {
                    continue;
                }

                int price = _marketManager != null ? _marketManager.GetGladiatorSellPrice(gladiator) : 0;
                Sprite icon = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
                _sellGladiatorViewBuffer.Add(
                    new OwnedItemViewData(icon, gladiator.DisplayName, string.Empty, string.Empty, gladiator)
                );
            }
        }

        if (sellGladiatorViewer != null)
        {
            sellGladiatorViewer.SetItems(_sellGladiatorViewBuffer, OnSellGladiatorItemClicked);
        }
    }

    private void OnBuyEquipmentItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketWeaponOffer offer || offer.Weapon == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyWeaponOffer = offer;
        SetBuyDetail(BuildBuyEquipmentText(offer), offer.Price, GetBalanceAfterBuy(offer.Price));
    }

    private void OnBuyArtifactItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketArtifactOffer offer || offer.Artifact == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyArtifactOffer = offer;
        SetBuyDetail(BuildBuyArtifactText(offer), offer.Price, GetBalanceAfterBuy(offer.Price));
    }

    private void OnBuyGladiatorItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketGladiatorOffer offer || offer.Gladiator == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyGladiatorOffer = offer;
        SetBuyDetail(BuildBuyGladiatorText(offer), offer.Price, GetBalanceAfterBuy(offer.Price));
    }

    private void OnSellEquipmentItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedWeaponData weapon)
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

        ClearPendingSelections();
        _pendingSellWeapon = weapon;
        int price = _marketManager != null ? _marketManager.GetWeaponSellPrice(weapon) : 0;
        SetSellDetail(BuildSellEquipmentText(weapon), price, GetBalanceAfterSell(price));
    }

    private void OnSellArtifactItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedArtifactData artifact)
        {
            return;
        }

        OwnedGladiatorData owner =
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedArtifact(artifact) : null;
        if (owner != null)
        {
            OpenCannotSellPanel();
            return;
        }

        ClearPendingSelections();
        _pendingSellArtifact = artifact;
        int price = _marketManager != null ? _marketManager.GetArtifactSellPrice() : 0;
        SetSellDetail(BuildSellArtifactText(artifact), price, GetBalanceAfterSell(price));
    }

    private void OnSellGladiatorItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedGladiatorData gladiator)
        {
            return;
        }

        ClearPendingSelections();
        _pendingSellGladiator = gladiator;
        int price = _marketManager != null ? _marketManager.GetGladiatorSellPrice(gladiator) : 0;
        SetSellDetail(BuildSellGladiatorText(gladiator), price, GetBalanceAfterSell(price));
    }

    private void OnBuySelectedClicked()
    {
        if (_marketManager == null || _tradeMode != TradeMode.Buy)
        {
            return;
        }

        bool succeeded = false;
        string failReason = string.Empty;

        if (_pendingBuyWeaponOffer != null)
        {
            succeeded = _marketManager.TryBuyWeapon(_pendingBuyWeaponOffer.SlotIndex, out failReason);
        }
        else if (_pendingBuyArtifactOffer != null)
        {
            succeeded = _marketManager.TryBuyArtifact(_pendingBuyArtifactOffer.SlotIndex, out failReason);
        }
        else if (_pendingBuyGladiatorOffer != null)
        {
            succeeded = _marketManager.TryBuyGladiator(_pendingBuyGladiatorOffer.SlotIndex, out failReason);
        }
        else
        {
            return;
        }

        if (!succeeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        if (succeeded)
        {
            SetBuyBalanceAfterTrade(_resourceManager.CurrentGold);
            ClearPendingSelections();
            RefreshCurrentCategoryViewer();
        }
    }

    private void OnSellSelectedClicked()
    {
        if (_marketManager == null || _tradeMode != TradeMode.Sell)
        {
            return;
        }

        bool succeeded = false;
        string failReason = string.Empty;
        int soldPrice = 0;

        if (_pendingSellWeapon != null)
        {
            succeeded = _marketManager.TrySellWeapon(_pendingSellWeapon, out soldPrice, out failReason);
        }
        else if (_pendingSellArtifact != null)
        {
            succeeded = _marketManager.TrySellArtifact(_pendingSellArtifact, out soldPrice, out failReason);
        }
        else if (_pendingSellGladiator != null)
        {
            succeeded = _marketManager.TrySellGladiator(_pendingSellGladiator, out soldPrice, out failReason);
        }
        else
        {
            return;
        }

        if (!succeeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
            if (failReason == "You can't sell equipped items.")
            {
                OpenCannotSellPanel();
            }
        }

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        if (succeeded)
        {
            SetSellBalanceAfterTrade(_resourceManager.CurrentGold);
            ClearPendingSelections();
            RefreshCurrentCategoryViewer();
        }
    }

    private int GetBalanceAfterBuy(int price)
    {
        return (_resourceManager != null ? _resourceManager.CurrentGold : 0) - Mathf.Max(0, price);
    }

    private int GetBalanceAfterSell(int price)
    {
        return (_resourceManager != null ? _resourceManager.CurrentGold : 0) + Mathf.Max(0, price);
    }

    private void SetBuyDetail(string description, int price, int balanceAfterTrade)
    {
        if (buyDescriptionText != null)
        {
            buyDescriptionText.text = description;
        }

        if (buyPriceText != null)
        {
            buyPriceText.text = $"가격: {price}";
        }

        SetBuyBalanceAfterTrade(balanceAfterTrade);
    }

    private void SetSellDetail(string description, int price, int balanceAfterTrade)
    {
        if (sellDescriptionText != null)
        {
            sellDescriptionText.text = description;
        }

        if (sellPriceText != null)
        {
            sellPriceText.text = $"가격: {price}";
        }

        SetSellBalanceAfterTrade(balanceAfterTrade);
    }

    private void ClearBuyDetail()
    {
        if (buyDescriptionText != null)
        {
            buyDescriptionText.text = string.Empty;
        }

        if (buyPriceText != null)
        {
            buyPriceText.text = "가격: -";
        }

        SetBuyBalanceAfterTrade(null);
    }

    private void ClearSellDetail()
    {
        if (sellDescriptionText != null)
        {
            sellDescriptionText.text = string.Empty;
        }

        if (sellPriceText != null)
        {
            sellPriceText.text = "가격: -";
        }

        SetSellBalanceAfterTrade(null);
    }

    private void SetBuyBalanceAfterTrade(int? balanceAfterTrade)
    {
        if (buyBalanceAfterTradeText != null)
        {
            buyBalanceAfterTradeText.text = balanceAfterTrade.HasValue
                ? $"거래 후 잔금: {balanceAfterTrade.Value}"
                : "거래 후 잔금: -";
        }
    }

    private void SetSellBalanceAfterTrade(int? balanceAfterTrade)
    {
        if (sellBalanceAfterTradeText != null)
        {
            sellBalanceAfterTradeText.text = balanceAfterTrade.HasValue
                ? $"거래 후 잔금: {balanceAfterTrade.Value}"
                : "거래 후 잔금: -";
        }
    }

    private void ClearPendingSelections()
    {
        _pendingBuyGladiatorOffer = null;
        _pendingBuyWeaponOffer = null;
        _pendingBuyArtifactOffer = null;
        _pendingSellGladiator = null;
        _pendingSellWeapon = null;
        _pendingSellArtifact = null;
    }

    private void OnGoldChanged(int currentGold)
    {
        RefreshGoldText(currentGold);
    }

    private void RefreshGoldText(int currentGold)
    {
        string text = $"골드: {currentGold}";

        if (buyGoldText != null)
        {
            buyGoldText.text = text;
        }

        if (sellGoldText != null)
        {
            sellGoldText.text = text;
        }
    }

    private string BuildBuyGladiatorText(MarketGladiatorOffer offer)
    {
        if (offer == null || offer.Gladiator == null)
        {
            return string.Empty;
        }

        OwnedGladiatorData gladiator = offer.Gladiator;

        return $"이름: {gladiator.DisplayName}\n"
            + $"레벨: {gladiator.Level}\n"
            + $"충성도: {gladiator.Loyalty}\n"
            + $"유지비: {gladiator.Upkeep}\n"
            + $"최대체력: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
            + $"공격력: {Mathf.FloorToInt(gladiator.CachedAttack)}\n"
            + $"공격속도: {gladiator.CachedAttackSpeed:0.##}\n"
            + $"이동속도: {gladiator.CachedMoveSpeed:0.##}\n"
            + $"사거리: {gladiator.CachedAttackRange:0.##}";
    }

    private string BuildBuyEquipmentText(MarketWeaponOffer offer)
    {
        if (offer == null || offer.Weapon == null)
        {
            return string.Empty;
        }

        return BuildSellEquipmentText(offer.Weapon);
    }

    private static string BuildBuyArtifactText(MarketArtifactOffer offer)
    {
        if (offer == null || offer.Artifact == null)
        {
            return string.Empty;
        }

        return BuildArtifactText(offer.Artifact);
    }

    private string BuildSellGladiatorText(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return string.Empty;
        }

        return $"이름: {gladiator.DisplayName}\n"
            + $"레벨: {gladiator.Level}\n"
            + $"충성도: {gladiator.Loyalty}\n"
            + $"유지비: {gladiator.Upkeep}\n"
            + $"최대체력: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
            + $"공격력: {Mathf.FloorToInt(gladiator.CachedAttack)}\n"
            + $"공격속도: {gladiator.CachedAttackSpeed:0.##}\n"
            + $"이동속도: {gladiator.CachedMoveSpeed:0.##}\n"
            + $"사거리: {gladiator.CachedAttackRange:0.##}";
    }

    private static string BuildSellEquipmentText(OwnedWeaponData weapon)
    {
        if (weapon == null)
        {
            return string.Empty;
        }

        string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
        string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";

        return $"이름: {weapon.DisplayName}\n"
            + $"무기군: {weaponTypeText}\n"
            + $"스킬: {skillName}\n"
            + $"레벨: {weapon.Level}\n"
            + $"추가공격력: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
            + $"추가체력: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
            + $"추가공격속도: {weapon.CachedAttackSpeedBonus:0.##}\n"
            + $"추가이동속도: {weapon.CachedMoveSpeedBonus:0.##}\n"
            + $"추가사거리: {weapon.CachedAttackRangeBonus:0.##}";
    }

    private static string BuildSellArtifactText(OwnedArtifactData artifact)
    {
        if (artifact == null || artifact.Artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.Artifact.artifactLore) ? "-" : artifact.Artifact.artifactLore;
        return $"이름: {artifact.DisplayName}\n" + $"퍼크: {artifact.Artifact.ArtifactPerkId}\n" + lore;
    }

    private static string BuildArtifactText(ArtifactSO artifact)
    {
        if (artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.artifactLore) ? "-" : artifact.artifactLore;
        return $"이름: {artifact.artifactName}\n" + $"퍼크: {artifact.ArtifactPerkId}\n" + lore;
    }

    private void OpenCannotSellPanel()
    {
        if (cannotSellText != null)
        {
            cannotSellText.text = "you can't sell equipped items";
        }

        SetPanelActive(cannotSellPanel, true);
    }

    private void CloseCannotSellPanel()
    {
        SetPanelActive(cannotSellPanel, false);
    }

    private void OnCannotSellConfirmClicked()
    {
        CloseCannotSellPanel();
    }

    private void CacheCannotSellRefsIfNull()
    {
        if (cannotSellPanel != null)
        {
            return;
        }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
            {
                continue;
            }

            Transform found = FindChildTransformByName(roots[i].transform, "CannotSellPanel");
            if (found != null)
            {
                cannotSellPanel = found.gameObject;
                break;
            }
        }

        if (cannotSellPanel == null)
        {
            Debug.LogWarning(
                "[MarketUIManager] cannotSellPanel not found. Assign it in Inspector or name the GameObject 'CannotSellPanel'.",
                this
            );
        }
    }

    private static Transform FindChildTransformByName(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildTransformByName(child, childName);
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

    private static void SetViewerActive(OwnedItemGridViewer viewer, bool value)
    {
        if (viewer != null)
        {
            viewer.gameObject.SetActive(value);
        }
    }

    private static void SetPanelActive(GameObject panel, bool value)
    {
        if (panel != null)
        {
            panel.SetActive(value);
        }
    }
}
