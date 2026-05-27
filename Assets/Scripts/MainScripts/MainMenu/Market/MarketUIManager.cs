using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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

    [SerializeField]
    private RectTransform buyBackground;

    [SerializeField]
    private RectTransform sellBackground;

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
    [FormerlySerializedAs("buyEquipmentButton")]
    private Button buyWeaponTabButton;

    [SerializeField]
    [FormerlySerializedAs("buyArtifactButton")]
    private Button buyArtifactTabButton;

    [SerializeField]
    [FormerlySerializedAs("buyGladiatorButton")]
    private Button buyGladiatorTabButton;

    [Header("Sell Category Buttons")]
    [SerializeField]
    [FormerlySerializedAs("sellEquipmentButton")]
    private Button sellWeaponTabButton;

    [SerializeField]
    [FormerlySerializedAs("sellArtifactButton")]
    private Button sellArtifactTabButton;

    [SerializeField]
    [FormerlySerializedAs("sellGladiatorButton")]
    private Button sellGladiatorTabButton;

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
    private TMP_Text buySelectedTabText;

    [SerializeField]
    private TMP_Text buyEquipmentNameText;

    [SerializeField]
    private TMP_Text buyEquipmentKindText;

    [SerializeField]
    private TMP_Text buyEquipmentSkillText;

    [SerializeField]
    private TMP_Text buyEquipmentDetailText;

    [SerializeField]
    private Image buyEquipmentImage;

    [SerializeField]
    private GladiatorModelPreviewView buyEquipmentModelPreviewView;

    [SerializeField]
    private WeaponModelPreviewView buyEquipmentWeaponPreviewView;

    [SerializeField]
    private TMP_Text buyPriceText;

    [SerializeField]
    private TMP_Text buyBalanceAfterTradeText;

    [Header("Sell Detail")]
    [SerializeField]
    private TMP_Text sellGoldText;

    [SerializeField]
    private TMP_Text sellSelectedTabText;

    [SerializeField]
    private TMP_Text sellEquipmentNameText;

    [SerializeField]
    private TMP_Text sellEquipmentKindText;

    [SerializeField]
    private TMP_Text sellEquipmentSkillText;

    [SerializeField]
    private TMP_Text sellEquipmentDetailText;

    [SerializeField]
    private Image sellEquipmentImage;

    [SerializeField]
    private GladiatorModelPreviewView sellEquipmentModelPreviewView;

    [SerializeField]
    private WeaponModelPreviewView sellEquipmentWeaponPreviewView;

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

    [Header("Sell Equipped Confirm Popup")]
    [SerializeField]
    private GameObject sellEquippedConfirmPanel;

    [SerializeField]
    private TMP_Text sellEquippedConfirmText;

    [SerializeField]
    private Button sellEquippedConfirmButton;

    [SerializeField]
    private Button sellEquippedCancelButton;

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
        ResolveMissingReferences();
        BindButton(buyWeaponTabButton, OnBuyEquipmentClicked);
        BindButton(buyArtifactTabButton, OnBuyArtifactClicked);
        BindButton(buyGladiatorTabButton, OnBuyGladiatorClicked);
        BindButton(sellWeaponTabButton, OnSellEquipmentClicked);
        BindButton(sellArtifactTabButton, OnSellArtifactClicked);
        BindButton(sellGladiatorTabButton, OnSellGladiatorClicked);
        BindButton(buySelectedButton, OnBuySelectedClicked);
        BindButton(sellSelectedButton, OnSellSelectedClicked);
        BindButton(cannotSellConfirmButton, OnCannotSellConfirmClicked);
        BindButton(sellEquippedConfirmButton, OnSellEquippedConfirmClicked);
        BindButton(sellEquippedCancelButton, OnSellEquippedCancelClicked);

        _resourceManager.GoldChanged += OnGoldChanged;
        CacheCannotSellRefsIfNull();
        CacheDetailPreviewViews();
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
        CloseSellEquippedConfirmPanel();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
    }

    public void CloseMarket()
    {
        _tradeMode = TradeMode.None;
        ClearPendingSelections();
        CloseCannotSellPanel();
        CloseSellEquippedConfirmPanel();
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
        CloseSellEquippedConfirmPanel();
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyModePanel, true);
        SetPanelActive(sellModePanel, false);
        ApplyViewerVisibility();
        RefreshCategoryTabLayering();
        SetSelectedTabText(buySelectedTabText, category);
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
        CloseSellEquippedConfirmPanel();
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyModePanel, false);
        SetPanelActive(sellModePanel, true);
        ApplyViewerVisibility();
        RefreshCategoryTabLayering();
        SetSelectedTabText(sellSelectedTabText, category);
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

                _buyEquipmentViewBuffer.Add(BuildWeaponViewData(offer.Weapon, string.Empty, offer));
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

                _buyGladiatorViewBuffer.Add(BuildGladiatorViewData(offer.Gladiator, offer));
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
                _sellEquipmentViewBuffer.Add(BuildWeaponViewData(weapon, equippedMark, weapon));
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

                _sellGladiatorViewBuffer.Add(BuildGladiatorViewData(gladiator, gladiator));
            }
        }

        if (sellGladiatorViewer != null)
        {
            sellGladiatorViewer.SetItems(_sellGladiatorViewBuffer, OnSellGladiatorItemClicked);
        }
    }

    private static OwnedItemViewData BuildGladiatorViewData(OwnedGladiatorData gladiator, object source)
    {
        GameObject modelPrefab =
            gladiator != null && gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null;
        Sprite fallbackIcon =
            gladiator != null && gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;

        return new OwnedItemViewData(
            modelPrefab,
            gladiator?.CustomizeIndicates,
            gladiator?.EquippedWeapon?.Weapon?.leftWeaponPrefab,
            gladiator?.EquippedWeapon?.Weapon?.rightWeaponPrefab,
            fallbackIcon,
            gladiator?.DisplayName,
            gladiator != null ? $"Lv.{gladiator.Level}" : string.Empty,
            string.Empty,
            source
        );
    }

    private static OwnedItemViewData BuildWeaponViewData(OwnedWeaponData weapon, string equippedMark, object source)
    {
        WeaponSO weaponSo = weapon?.Weapon;
        return new OwnedItemViewData(
            weaponSo?.leftWeaponPrefab,
            weaponSo?.rightWeaponPrefab,
            weaponSo?.icon,
            weapon?.DisplayName,
            string.Empty,
            equippedMark,
            source
        );
    }

    private void OnBuyEquipmentItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketWeaponOffer offer || offer.Weapon == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyWeaponOffer = offer;
        SetBuyDetail(
            offer.Weapon.DisplayName,
            GetWeaponKindText(offer.Weapon),
            GetWeaponSkillText(offer.Weapon),
            BuildBuyEquipmentDetailText(offer),
            offer.Weapon.Weapon != null ? offer.Weapon.Weapon.icon : null,
            offer.Price,
            GetBalanceAfterBuy(offer.Price),
            null,
            null,
            offer.Weapon.Weapon?.leftWeaponPrefab,
            offer.Weapon.Weapon?.rightWeaponPrefab
        );
    }

    private void OnBuyArtifactItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketArtifactOffer offer || offer.Artifact == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyArtifactOffer = offer;
        SetBuyDetail(
            offer.Artifact.artifactName,
            string.Empty,
            string.Empty,
            BuildBuyArtifactDetailText(offer),
            offer.Artifact.icon,
            offer.Price,
            GetBalanceAfterBuy(offer.Price)
        );
    }

    private void OnBuyGladiatorItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not MarketGladiatorOffer offer || offer.Gladiator == null)
        {
            return;
        }

        ClearPendingSelections();
        _pendingBuyGladiatorOffer = offer;
        SetBuyDetail(
            offer.Gladiator.DisplayName,
            GetGladiatorKindText(offer.Gladiator),
            string.Empty,
            BuildBuyGladiatorDetailText(offer),
            offer.Gladiator.GladiatorClass != null ? offer.Gladiator.GladiatorClass.icon : null,
            offer.Price,
            GetBalanceAfterBuy(offer.Price),
            offer.Gladiator.GladiatorClass != null ? offer.Gladiator.GladiatorClass.previewModelPrefab : null,
            offer.Gladiator.CustomizeIndicates,
            offer.Gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
            offer.Gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab
        );
    }

    private void OnSellEquipmentItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedWeaponData weapon)
        {
            return;
        }

        ClearPendingSelections();
        CloseSellEquippedConfirmPanel();
        _pendingSellWeapon = weapon;
        int price = _marketManager != null ? _marketManager.GetWeaponSellPrice(weapon) : 0;
        SetSellDetail(
            weapon.DisplayName,
            GetWeaponKindText(weapon),
            GetWeaponSkillText(weapon),
            BuildSellEquipmentDetailText(weapon),
            weapon.Weapon != null ? weapon.Weapon.icon : null,
            price,
            GetBalanceAfterSell(price),
            null,
            null,
            weapon.Weapon?.leftWeaponPrefab,
            weapon.Weapon?.rightWeaponPrefab
        );
    }

    private void OnSellArtifactItemClicked(OwnedItemViewData data)
    {
        if (data.Source is not OwnedArtifactData artifact)
        {
            return;
        }

        ClearPendingSelections();
        CloseSellEquippedConfirmPanel();
        _pendingSellArtifact = artifact;
        int price = _marketManager != null ? _marketManager.GetArtifactSellPrice() : 0;
        SetSellDetail(
            artifact.DisplayName,
            string.Empty,
            string.Empty,
            BuildSellArtifactDetailText(artifact),
            artifact.Artifact != null ? artifact.Artifact.icon : null,
            price,
            GetBalanceAfterSell(price)
        );
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
        SetSellDetail(
            gladiator.DisplayName,
            GetGladiatorKindText(gladiator),
            string.Empty,
            BuildSellGladiatorDetailText(gladiator),
            gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null,
            price,
            GetBalanceAfterSell(price),
            gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null,
            gladiator.CustomizeIndicates,
            gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
            gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab
        );
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
            ClearPendingSelections();
            ClearBuyDetail();
            RefreshCurrentCategoryViewer();
        }
    }

    private void OnSellSelectedClicked()
    {
        if (_marketManager == null || _tradeMode != TradeMode.Sell)
        {
            return;
        }

        if (TryGetPendingSellEquippedOwner(out OwnedGladiatorData owner))
        {
            OpenSellEquippedConfirmPanel(owner);
            return;
        }

        SellSelectedItem();
    }

    private void SellSelectedItem()
    {
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
            ClearPendingSelections();
            ClearSellDetail();
            CloseSellEquippedConfirmPanel();
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

    private void SetBuyDetail(
        string name,
        string kind,
        string skill,
        string detail,
        Sprite icon,
        int price,
        int balanceAfterTrade,
        GameObject modelPrefab = null,
        int[] modelCustomizeIndicates = null,
        GameObject leftWeaponPrefab = null,
        GameObject rightWeaponPrefab = null
    )
    {
        SetTradeDetailTexts(
            buyEquipmentNameText,
            buyEquipmentKindText,
            buyEquipmentSkillText,
            buyEquipmentDetailText,
            buyEquipmentImage,
            buyEquipmentModelPreviewView,
            buyEquipmentWeaponPreviewView,
            name,
            kind,
            skill,
            detail,
            icon,
            modelPrefab,
            modelCustomizeIndicates,
            leftWeaponPrefab,
            rightWeaponPrefab
        );
        if (buyPriceText != null)
        {
            buyPriceText.text = $"가격: {price}";
        }

        SetBuyBalanceAfterTrade(balanceAfterTrade);
    }

    private void SetSellDetail(
        string name,
        string kind,
        string skill,
        string detail,
        Sprite icon,
        int price,
        int balanceAfterTrade,
        GameObject modelPrefab = null,
        int[] modelCustomizeIndicates = null,
        GameObject leftWeaponPrefab = null,
        GameObject rightWeaponPrefab = null
    )
    {
        SetTradeDetailTexts(
            sellEquipmentNameText,
            sellEquipmentKindText,
            sellEquipmentSkillText,
            sellEquipmentDetailText,
            sellEquipmentImage,
            sellEquipmentModelPreviewView,
            sellEquipmentWeaponPreviewView,
            name,
            kind,
            skill,
            detail,
            icon,
            modelPrefab,
            modelCustomizeIndicates,
            leftWeaponPrefab,
            rightWeaponPrefab
        );
        if (sellPriceText != null)
        {
            sellPriceText.text = $"가격: {price}";
        }

        SetSellBalanceAfterTrade(balanceAfterTrade);
    }

    private void ClearBuyDetail()
    {
        SetTradeDetailTexts(
            buyEquipmentNameText,
            buyEquipmentKindText,
            buyEquipmentSkillText,
            buyEquipmentDetailText,
            buyEquipmentImage,
            buyEquipmentModelPreviewView,
            buyEquipmentWeaponPreviewView,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null
        );
        if (buyPriceText != null)
        {
            buyPriceText.text = "가격: -";
        }

        SetBuyBalanceAfterTrade(null);
    }

    private void ClearSellDetail()
    {
        SetTradeDetailTexts(
            sellEquipmentNameText,
            sellEquipmentKindText,
            sellEquipmentSkillText,
            sellEquipmentDetailText,
            sellEquipmentImage,
            sellEquipmentModelPreviewView,
            sellEquipmentWeaponPreviewView,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null
        );
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

    private void CacheDetailPreviewViews()
    {
        if (buyEquipmentModelPreviewView == null && buyEquipmentImage != null)
        {
            buyEquipmentModelPreviewView = buyEquipmentImage.GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        if (buyEquipmentWeaponPreviewView == null && buyEquipmentImage != null)
        {
            buyEquipmentWeaponPreviewView = buyEquipmentImage.GetComponentInChildren<WeaponModelPreviewView>(true);
        }

        if (sellEquipmentModelPreviewView == null && sellEquipmentImage != null)
        {
            sellEquipmentModelPreviewView = sellEquipmentImage.GetComponentInChildren<GladiatorModelPreviewView>(true);
        }

        if (sellEquipmentWeaponPreviewView == null && sellEquipmentImage != null)
        {
            sellEquipmentWeaponPreviewView = sellEquipmentImage.GetComponentInChildren<WeaponModelPreviewView>(true);
        }
    }

    private static void SetTradeDetailTexts(
        TMP_Text nameText,
        TMP_Text kindText,
        TMP_Text skillText,
        TMP_Text detailText,
        Image image,
        GladiatorModelPreviewView modelPreviewView,
        WeaponModelPreviewView weaponPreviewView,
        string name,
        string kind,
        string skill,
        string detail,
        Sprite icon,
        GameObject modelPrefab,
        int[] modelCustomizeIndicates,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab
    )
    {
        SetText(nameText, name);
        SetText(kindText, kind);
        SetText(skillText, skill);
        SetText(detailText, detail);

        bool hasWeaponPreview = leftWeaponPrefab != null || rightWeaponPrefab != null;
        bool useModelPreview = modelPreviewView != null && modelPrefab != null;
        bool useWeaponPreview = weaponPreviewView != null && !useModelPreview && hasWeaponPreview;
        if (modelPreviewView != null)
        {
            if (useModelPreview)
            {
                modelPreviewView.Show(modelPrefab, modelCustomizeIndicates, leftWeaponPrefab, rightWeaponPrefab);
            }
            else
            {
                modelPreviewView.Clear();
            }
        }

        if (weaponPreviewView != null)
        {
            if (useWeaponPreview)
            {
                weaponPreviewView.Show(leftWeaponPrefab, rightWeaponPrefab);
            }
            else
            {
                weaponPreviewView.Clear();
            }
        }

        if (image == null)
        {
            return;
        }

        image.sprite = icon;
        image.enabled = !useModelPreview && !useWeaponPreview && icon != null;
        image.preserveAspect = true;
    }

    private void RefreshCategoryTabLayering()
    {
        if (_tradeMode == TradeMode.Buy)
        {
            MoveCategoryTabsAroundBackground(
                buyBackground,
                buyWeaponTabButton,
                buyArtifactTabButton,
                buyGladiatorTabButton,
                _currentCategory
            );
            return;
        }

        if (_tradeMode == TradeMode.Sell)
        {
            MoveCategoryTabsAroundBackground(
                sellBackground,
                sellWeaponTabButton,
                sellArtifactTabButton,
                sellGladiatorTabButton,
                _currentCategory
            );
        }
    }

    private static void MoveCategoryTabsAroundBackground(
        RectTransform background,
        Button weaponButton,
        Button artifactButton,
        Button gladiatorButton,
        MarketCategory activeCategory
    )
    {
        if (background == null)
        {
            return;
        }

        Button activeButton = GetCategoryButton(weaponButton, artifactButton, gladiatorButton, activeCategory);
        Button[] inactiveButtons = GetInactiveCategoryButtons(
            weaponButton,
            artifactButton,
            gladiatorButton,
            activeCategory
        );

        DisableSortingCanvas(background.gameObject);
        DisableButtonSortingCanvas(weaponButton);
        DisableButtonSortingCanvas(artifactButton);
        DisableButtonSortingCanvas(gladiatorButton);

        Transform backgroundParent = background.parent;
        if (backgroundParent == null)
        {
            return;
        }

        if (
            activeButton == null
            || inactiveButtons[0] == null
            || inactiveButtons[1] == null
            || activeButton.transform.parent != backgroundParent
            || inactiveButtons[0].transform.parent != backgroundParent
            || inactiveButtons[1].transform.parent != backgroundParent
        )
        {
            return;
        }

        // 4개 요소의 기존 묶음 위치를 기준으로 최종 순서를 한 번에 고정한다.
        // 최종 렌더 순서: 비선택 탭 2개 -> 배경 -> 선택 탭 1개.
        int startIndex = Mathf.Min(
            background.GetSiblingIndex(),
            activeButton.transform.GetSiblingIndex(),
            inactiveButtons[0].transform.GetSiblingIndex(),
            inactiveButtons[1].transform.GetSiblingIndex()
        );

        Transform[] orderedTransforms =
        {
            inactiveButtons[0].transform,
            inactiveButtons[1].transform,
            background,
            activeButton.transform,
        };

        for (int i = 0; i < orderedTransforms.Length; i++)
        {
            orderedTransforms[i].SetSiblingIndex(Mathf.Clamp(startIndex + i, 0, backgroundParent.childCount - 1));
        }
    }

    private static Button GetCategoryButton(
        Button weaponButton,
        Button artifactButton,
        Button gladiatorButton,
        MarketCategory category
    )
    {
        return category switch
        {
            MarketCategory.Equipment => weaponButton,
            MarketCategory.Artifact => artifactButton,
            MarketCategory.Gladiator => gladiatorButton,
            _ => null,
        };
    }

    private static void DisableButtonSortingCanvas(Button button)
    {
        if (button != null)
        {
            DisableSortingCanvas(button.gameObject);
        }
    }

    private static void DisableSortingCanvas(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        canvas.overrideSorting = false;
    }

    private static Button[] GetInactiveCategoryButtons(
        Button weaponButton,
        Button artifactButton,
        Button gladiatorButton,
        MarketCategory activeCategory
    )
    {
        return activeCategory switch
        {
            MarketCategory.Equipment => new[] { artifactButton, gladiatorButton },
            MarketCategory.Artifact => new[] { weaponButton, gladiatorButton },
            MarketCategory.Gladiator => new[] { weaponButton, artifactButton },
            _ => new[] { artifactButton, gladiatorButton },
        };
    }

    private static void SetSelectedTabText(TMP_Text text, MarketCategory category)
    {
        SetText(text, GetCategoryDisplayName(category));
    }

    private static string GetCategoryDisplayName(MarketCategory category)
    {
        return category switch
        {
            MarketCategory.Equipment => "무기",
            MarketCategory.Artifact => "장신구",
            MarketCategory.Gladiator => "검투사",
            _ => string.Empty,
        };
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

    private string BuildBuyGladiatorDetailText(MarketGladiatorOffer offer)
    {
        if (offer == null || offer.Gladiator == null)
        {
            return string.Empty;
        }

        OwnedGladiatorData gladiator = offer.Gladiator;

        return $"레벨: {gladiator.Level}\n"
            + $"충성도: {gladiator.Loyalty}\n"
            + $"유지비: {gladiator.Upkeep}\n"
            + $"최대체력: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
            + $"공격력: {Mathf.FloorToInt(gladiator.CachedAttack)}\n"
            + $"공격속도: {gladiator.CachedAttackSpeed:0.##}\n"
            + $"이동속도: {gladiator.CachedMoveSpeed:0.##}\n"
            + $"사거리: {gladiator.CachedAttackRange:0.##}";
    }

    private string BuildBuyEquipmentDetailText(MarketWeaponOffer offer)
    {
        if (offer == null || offer.Weapon == null)
        {
            return string.Empty;
        }

        return BuildSellEquipmentDetailText(offer.Weapon);
    }

    private static string BuildBuyArtifactDetailText(MarketArtifactOffer offer)
    {
        if (offer == null || offer.Artifact == null)
        {
            return string.Empty;
        }

        return BuildArtifactText(offer.Artifact);
    }

    private string BuildSellGladiatorDetailText(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return string.Empty;
        }

        return $"레벨: {gladiator.Level}\n"
            + $"충성도: {gladiator.Loyalty}\n"
            + $"유지비: {gladiator.Upkeep}\n"
            + $"최대체력: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
            + $"공격력: {Mathf.FloorToInt(gladiator.CachedAttack)}\n"
            + $"공격속도: {gladiator.CachedAttackSpeed:0.##}\n"
            + $"이동속도: {gladiator.CachedMoveSpeed:0.##}\n"
            + $"사거리: {gladiator.CachedAttackRange:0.##}";
    }

    private static string BuildSellEquipmentDetailText(OwnedWeaponData weapon)
    {
        if (weapon == null)
        {
            return string.Empty;
        }

        return $"레벨: {weapon.Level}\n"
            + $"추가공격력: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
            + $"추가체력: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
            + $"추가공격속도: {weapon.CachedAttackSpeedBonus:0.##}\n"
            + $"추가이동속도: {weapon.CachedMoveSpeedBonus:0.##}\n"
            + $"추가사거리: {weapon.CachedAttackRangeBonus:0.##}";
    }

    private static string BuildSellArtifactDetailText(OwnedArtifactData artifact)
    {
        if (artifact == null || artifact.Artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.Artifact.artifactLore) ? "-" : artifact.Artifact.artifactLore;
        return $"퍼크: {artifact.Artifact.ArtifactPerkId}\n" + lore;
    }

    private static string BuildArtifactText(ArtifactSO artifact)
    {
        if (artifact == null)
        {
            return string.Empty;
        }

        string lore = string.IsNullOrWhiteSpace(artifact.artifactLore) ? "-" : artifact.artifactLore;
        return $"퍼크: {artifact.ArtifactPerkId}\n" + lore;
    }

    private static string GetWeaponKindText(OwnedWeaponData weapon)
    {
        return weapon != null && weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : string.Empty;
    }

    private static string GetWeaponSkillText(OwnedWeaponData weapon)
    {
        return weapon != null && weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : string.Empty;
    }

    private static string GetGladiatorKindText(OwnedGladiatorData gladiator)
    {
        if (gladiator == null || gladiator.GladiatorClass == null)
        {
            return string.Empty;
        }

        return !string.IsNullOrWhiteSpace(gladiator.GladiatorClass.className)
            ? gladiator.GladiatorClass.className
            : gladiator.GladiatorClass.name;
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

    private void OpenSellEquippedConfirmPanel(OwnedGladiatorData owner)
    {
        if (sellEquippedConfirmText != null)
        {
            string ownerName = owner != null ? owner.DisplayName : "검투사";
            string itemName = GetPendingSellItemName();
            string itemParticle = HasFinalConsonant(itemName) ? "은" : "는";
            string ownerParticle = HasFinalConsonant(ownerName) ? "이" : "가";

            sellEquippedConfirmText.text =
                $"{itemName}{itemParticle} 현재 {ownerName}{ownerParticle} 장착중입니다.\n"
                + $"판매 시 {ownerName}의 {itemName}{itemParticle} 해제됩니다.\n"
                + "정말 판매하시겠습니까?";
        }

        SetPanelActive(sellEquippedConfirmPanel, true);
    }

    private void CloseSellEquippedConfirmPanel()
    {
        SetPanelActive(sellEquippedConfirmPanel, false);
    }

    private void OnSellEquippedConfirmClicked()
    {
        if (!TryUnequipPendingSellItem())
        {
            return;
        }

        CloseSellEquippedConfirmPanel();
        SellSelectedItem();
    }

    private void OnSellEquippedCancelClicked()
    {
        CloseSellEquippedConfirmPanel();
    }

    private bool TryGetPendingSellEquippedOwner(out OwnedGladiatorData owner)
    {
        owner = null;

        if (_gladiatorManager == null)
        {
            return false;
        }

        if (_pendingSellWeapon != null)
        {
            owner = _gladiatorManager.FindOwnerOfEquippedWeapon(_pendingSellWeapon);
            return owner != null;
        }

        if (_pendingSellArtifact != null)
        {
            owner = _gladiatorManager.FindOwnerOfEquippedArtifact(_pendingSellArtifact);
            return owner != null;
        }

        return false;
    }

    private bool TryUnequipPendingSellItem()
    {
        if (_gladiatorManager == null)
        {
            return false;
        }

        string failReason;

        if (_pendingSellWeapon != null)
        {
            OwnedGladiatorData owner = _gladiatorManager.FindOwnerOfEquippedWeapon(_pendingSellWeapon);
            return owner == null || _gladiatorManager.TryUnequipWeapon(owner, out failReason);
        }

        if (_pendingSellArtifact != null)
        {
            OwnedGladiatorData owner = _gladiatorManager.FindOwnerOfEquippedArtifact(_pendingSellArtifact);
            return owner == null || _gladiatorManager.TryUnequipArtifact(owner, out failReason);
        }

        return false;
    }

    private string GetPendingSellItemName()
    {
        if (_pendingSellWeapon != null)
        {
            return _pendingSellWeapon.DisplayName;
        }

        if (_pendingSellArtifact != null)
        {
            return _pendingSellArtifact.DisplayName;
        }

        return "선택한 장비";
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

    private void ResolveMissingReferences()
    {
        if (buyBackground == null && buyModePanel != null)
        {
            buyBackground = FindChildTransformByName(buyModePanel.transform, "BuyBackground") as RectTransform;
        }

        if (sellBackground == null && sellModePanel != null)
        {
            sellBackground =
                (
                    FindChildTransformByName(sellModePanel.transform, "SellBackground")
                    ?? FindChildTransformByName(sellModePanel.transform, "BuyBackground")
                ) as RectTransform;
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

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
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
