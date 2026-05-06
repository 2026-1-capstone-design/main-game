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

    [SerializeField]
    private GameObject buyArtifactPanel;

    [SerializeField]
    private GameObject sellArtifactPanel;

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

    [SerializeField]
    private Button buyArtifactButton;

    [SerializeField]
    private Button sellArtifactButton;

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
    private TMP_Text sellGladiatorGoldText;

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
    private TMP_Text sellEquipmentGoldText;

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

    [Header("Buy Artifact Panel")]
    [SerializeField]
    private Button buyArtifactBackButton;

    [SerializeField]
    private TMP_Text buyArtifactGoldText;

    [SerializeField]
    private Button[] buyArtifactSlotButtons;

    [SerializeField]
    private GameObject[] buyArtifactSlotRoots;

    [SerializeField]
    private Image[] buyArtifactSlotIcons;

    [SerializeField]
    private TMP_Text[] buyArtifactSlotTexts;

    [Header("Sell Artifact Panel")]
    [SerializeField]
    private Button sellArtifactBackButton;

    [SerializeField]
    private TMP_Text sellArtifactGoldText;

    [SerializeField]
    private OwnedItemGridViewer sellArtifactViewer;

    [SerializeField]
    private GameObject sellArtifactConfirmMask;

    [SerializeField]
    private GameObject sellArtifactConfirmPanel;

    [SerializeField]
    private TMP_Text sellArtifactConfirmNameText;

    [SerializeField]
    private TMP_Text sellArtifactConfirmPriceText;

    [SerializeField]
    private Button sellArtifactConfirmYesButton;

    [SerializeField]
    private Button sellArtifactConfirmNoButton;

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

    private readonly List<OwnedItemViewData> _sellGladiatorViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellEquipmentViewBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedItemViewData> _sellArtifactViewBuffer = new List<OwnedItemViewData>();

    private MainFlowManager _flow;
    private MarketManager _marketManager;
    private ResourceManager _resourceManager;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private ResearchManager _researchManager;
    private bool _initialized;

    private OwnedGladiatorData _pendingSellGladiator;
    private int _pendingSellPrice;

    private OwnedWeaponData _pendingSellWeapon;
    private int _pendingSellWeaponPrice;

    private ArtifactSO _pendingSellArtifact;
    private int _pendingSellArtifactPrice;

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
        _researchManager = researchManager;

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

        if (_researchManager == null)
        {
            Debug.LogError("[MarketUIManager] researchManager is null.", this);
            return;
        }

        BindButton(marketBackButton, OnMarketBackClicked);
        BindButton(buyGladiatorButton, OnBuyGladiatorClicked);
        BindButton(buyEquipmentButton, OnBuyEquipmentClicked);
        BindButton(sellGladiatorButton, OnSellGladiatorClicked);
        BindButton(sellEquipmentButton, OnSellEquipmentClicked);
        BindButton(buyArtifactButton, OnBuyArtifactClicked);
        BindButton(sellArtifactButton, OnSellArtifactClicked);
        BindButton(buyGladiatorBackButton, OnBuyGladiatorBackClicked);
        BindButton(buyEquipmentBackButton, OnBuyEquipmentBackClicked);
        BindButton(sellGladiatorBackButton, OnSellGladiatorBackClicked);
        BindButton(sellEquipmentBackButton, OnSellEquipmentBackClicked);
        BindButton(buyArtifactBackButton, OnBuyArtifactBackClicked);
        BindButton(sellArtifactBackButton, OnSellArtifactBackClicked);
        BindButton(sellGladiatorConfirmYesButton, OnSellGladiatorConfirmYesClicked);
        BindButton(sellGladiatorConfirmNoButton, OnSellGladiatorConfirmNoClicked);
        BindButton(sellEquipmentConfirmYesButton, OnSellEquipmentConfirmYesClicked);
        BindButton(sellEquipmentConfirmNoButton, OnSellEquipmentConfirmNoClicked);
        BindButton(sellArtifactConfirmYesButton, OnSellArtifactConfirmYesClicked);
        BindButton(sellArtifactConfirmNoButton, OnSellArtifactConfirmNoClicked);
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

        int artifactBuySlotCount = GetBuyArtifactVisualSlotCount();
        for (int i = 0; i < artifactBuySlotCount; i++)
        {
            int capturedIndex = i;

            if (buyArtifactSlotButtons[i] != null)
            {
                buyArtifactSlotButtons[i].onClick.RemoveAllListeners();
                buyArtifactSlotButtons[i].onClick.AddListener(() => OnBuyArtifactSlotClicked(capturedIndex));
            }

            if (buyArtifactSlotIcons != null && i < buyArtifactSlotIcons.Length && buyArtifactSlotIcons[i] != null)
            {
                buyArtifactSlotIcons[i].raycastTarget = false;
            }
        }

        _resourceManager.GoldChanged += OnGoldChanged;
        RefreshGoldText(_resourceManager.CurrentGold);

        CacheCannotSellRefsIfNull();
        CloseMarket();
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketUIManager] Initialized. "
                    + $"BuyGladiatorSlots={gladiatorBuySlotCount}, BuyEquipmentSlots={equipmentBuySlotCount}, "
                    + $"BuyArtifactSlots={artifactBuySlotCount}",
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
        CloseSellArtifactConfirm();
        CloseCannotSellPanel();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
    }

    public void CloseMarket()
    {
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();
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
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, false);
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();

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
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, false);
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();

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
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, false);

        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();
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
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, false);

        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();
        CloseCannotSellPanel();
        RefreshSellEquipmentPanel();
    }

    private void OnBuyArtifactClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
        SetPanelActive(buyArtifactPanel, true);
        SetPanelActive(sellArtifactPanel, false);
        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyArtifactPanel();
    }

    private void OnSellArtifactClicked()
    {
        SetPanelActive(marketRootPanel, true);
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, true);

        CloseSellGladiatorConfirm();
        CloseSellEquipmentConfirm();
        CloseSellArtifactConfirm();
        CloseCannotSellPanel();
        RefreshSellArtifactPanel();
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

    private void OnBuyArtifactBackClicked()
    {
        SetPanelActive(buyArtifactPanel, false);
    }

    private void OnSellArtifactBackClicked()
    {
        CloseSellArtifactConfirm();
        CloseCannotSellPanel();
        SetPanelActive(sellArtifactPanel, false);
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

    private void OnBuyArtifactSlotClicked(int slotIndex)
    {
        if (_marketManager == null)
        {
            Debug.LogError("[MarketUIManager] marketManager is null.", this);
            return;
        }

        string failReason = string.Empty;
        bool purchaseSucceeded = _marketManager.TryBuyArtifact(slotIndex, out failReason);

        if (!purchaseSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshBuyArtifactPanel();
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

    private void RefreshBuyArtifactPanel()
    {
        int visualSlotCount = GetBuyArtifactVisualSlotCount();

        for (int i = 0; i < visualSlotCount; i++)
        {
            MarketArtifactOffer offer = _marketManager != null ? _marketManager.GetArtifactOffer(i) : null;
            bool shouldShow = offer != null && offer.IsAvailable;

            SetBuyArtifactSlotVisible(i, shouldShow);

            if (!shouldShow)
            {
                ClearBuyArtifactSlot(i);
                continue;
            }

            ArtifactSO artifact = offer.Artifact;

            if (buyArtifactSlotIcons != null && i < buyArtifactSlotIcons.Length && buyArtifactSlotIcons[i] != null)
            {
                Sprite icon = artifact != null ? artifact.icon : null;
                buyArtifactSlotIcons[i].sprite = icon;
                buyArtifactSlotIcons[i].enabled = icon != null;
            }

            if (buyArtifactSlotTexts != null && i < buyArtifactSlotTexts.Length && buyArtifactSlotTexts[i] != null)
            {
                buyArtifactSlotTexts[i].text = BuildBuyArtifactText(offer);
            }

            if (
                buyArtifactSlotButtons != null
                && i < buyArtifactSlotButtons.Length
                && buyArtifactSlotButtons[i] != null
            )
            {
                buyArtifactSlotButtons[i].interactable = true;
            }
        }
    }

    private void RefreshSellArtifactPanel()
    {
        CloseCannotSellPanel();

        if (sellArtifactViewer == null)
        {
            Debug.LogWarning("[MarketUIManager] sellArtifactViewer is not assigned.", this);
            return;
        }

        _sellArtifactViewBuffer.Clear();

        if (_researchManager != null)
        {
            IReadOnlyList<ArtifactSO> ownedArtifacts = _researchManager.UnlockedArtifacts;
            for (int i = 0; i < ownedArtifacts.Count; i++)
            {
                ArtifactSO artifact = ownedArtifacts[i];
                if (artifact == null)
                {
                    continue;
                }

                _sellArtifactViewBuffer.Add(new OwnedItemViewData(artifact.icon, artifact.artifactName, artifact));
            }
        }

        Canvas.ForceUpdateCanvases();
        sellArtifactViewer.SetItems(_sellArtifactViewBuffer, OnSellArtifactCellClicked);
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
                $"이름: {gladiator.DisplayName}\n"
                + $"레벨: {gladiator.Level}\n"
                + $"체력: {Mathf.FloorToInt(gladiator.CachedMaxHealth)}\n"
                + $"공격력: {Mathf.FloorToInt(gladiator.CachedAttack)}";
        }

        if (sellGladiatorConfirmPriceText != null)
        {
            sellGladiatorConfirmPriceText.text = $"가격: {_pendingSellPrice}";
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
                $"이름: {weapon.DisplayName}\n"
                + $"무기군: {weaponTypeText}\n"
                + $"스킬: {skillName}\n"
                + $"레벨: {weapon.Level}\n"
                + $"추가공격력: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
                + $"추가체력: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
                + $"추가공격속도: {weapon.CachedAttackSpeedBonus:0.##}\n"
                + $"추가이동속도: {weapon.CachedMoveSpeedBonus:0.##}\n"
                + $"추가사거리: {weapon.CachedAttackRangeBonus:0.##}";
        }

        if (sellEquipmentConfirmPriceText != null)
        {
            sellEquipmentConfirmPriceText.text = $"가격: {_pendingSellWeaponPrice}";
        }

        SetPanelActive(sellEquipmentConfirmMask, true);
        SetPanelActive(sellEquipmentConfirmPanel, true);
    }

    private void OnSellArtifactCellClicked(OwnedItemViewData data)
    {
        if (data.Source is not ArtifactSO artifact)
        {
            return;
        }

        _pendingSellArtifact = artifact;
        _pendingSellArtifactPrice = _marketManager != null ? _marketManager.GetArtifactSellPrice() : 0;

        if (sellArtifactConfirmNameText != null)
        {
            string desc = string.IsNullOrWhiteSpace(artifact.description) ? "-" : artifact.description;
            sellArtifactConfirmNameText.text = $"이름: {artifact.artifactName}\n{desc}";
        }

        if (sellArtifactConfirmPriceText != null)
        {
            sellArtifactConfirmPriceText.text = $"가격: {_pendingSellArtifactPrice}";
        }

        SetPanelActive(sellArtifactConfirmMask, true);
        SetPanelActive(sellArtifactConfirmPanel, true);
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

    private void OnSellArtifactConfirmYesClicked()
    {
        if (_pendingSellArtifact == null)
        {
            CloseSellArtifactConfirm();
            return;
        }

        string failReason = string.Empty;
        int soldPrice = 0;
        bool sellSucceeded = false;

        if (_marketManager != null)
        {
            sellSucceeded = _marketManager.TrySellArtifact(_pendingSellArtifact, out soldPrice, out failReason);
        }

        if (!sellSucceeded && !string.IsNullOrEmpty(failReason))
        {
            Debug.LogWarning("[MarketUIManager] " + failReason, this);
        }

        CloseSellArtifactConfirm();
        RefreshGoldText(_resourceManager != null ? _resourceManager.CurrentGold : 0);
        RefreshSellArtifactPanel();
    }

    private void OnSellArtifactConfirmNoClicked()
    {
        CloseSellArtifactConfirm();
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

    private void CloseSellArtifactConfirm()
    {
        _pendingSellArtifact = null;
        _pendingSellArtifactPrice = 0;

        SetPanelActive(sellArtifactConfirmMask, false);
        SetPanelActive(sellArtifactConfirmPanel, false);
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

    private string BuildBuyGladiatorText(MarketGladiatorOffer offer)
    {
        if (offer == null || offer.Gladiator == null)
        {
            return string.Empty;
        }

        OwnedGladiatorData gladiator = offer.Gladiator;

        return $"가격: {offer.Price}\n"
            + $"이름: {gladiator.DisplayName}\n"
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

        OwnedWeaponData weapon = offer.Weapon;
        string weaponTypeText = weapon.Weapon != null ? weapon.Weapon.weaponType.ToString() : "(None)";
        string skillName = weapon.WeaponSkill != null ? weapon.WeaponSkill.skillName : "(None)";

        return $"가격: {offer.Price}\n"
            + $"이름: {weapon.DisplayName}\n"
            + $"무기군: {weaponTypeText}\n"
            + $"스킬: {skillName}\n"
            + $"레벨: {weapon.Level}\n"
            + $"추가공격력: {Mathf.FloorToInt(weapon.CachedAttackBonus)}\n"
            + $"추가체력: {Mathf.FloorToInt(weapon.CachedHealthBonus)}\n"
            + $"추가공격속도: {weapon.CachedAttackSpeedBonus:0.##}\n"
            + $"추가이동속도: {weapon.CachedMoveSpeedBonus:0.##}\n"
            + $"추가사거리: {weapon.CachedAttackRangeBonus:0.##}";
    }

    private string BuildBuyArtifactText(MarketArtifactOffer offer)
    {
        if (offer == null || offer.Artifact == null)
        {
            return string.Empty;
        }

        ArtifactSO artifact = offer.Artifact;
        string desc = string.IsNullOrWhiteSpace(artifact.description) ? "-" : artifact.description;

        return $"가격: {offer.Price}\n이름: {artifact.artifactName}\n{desc}";
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

    private void ClearBuyArtifactSlot(int slotIndex)
    {
        if (
            buyArtifactSlotIcons != null
            && slotIndex < buyArtifactSlotIcons.Length
            && buyArtifactSlotIcons[slotIndex] != null
        )
        {
            buyArtifactSlotIcons[slotIndex].sprite = null;
            buyArtifactSlotIcons[slotIndex].enabled = false;
        }

        if (
            buyArtifactSlotTexts != null
            && slotIndex < buyArtifactSlotTexts.Length
            && buyArtifactSlotTexts[slotIndex] != null
        )
        {
            buyArtifactSlotTexts[slotIndex].text = string.Empty;
        }

        if (
            buyArtifactSlotButtons != null
            && slotIndex < buyArtifactSlotButtons.Length
            && buyArtifactSlotButtons[slotIndex] != null
        )
        {
            buyArtifactSlotButtons[slotIndex].interactable = false;
        }
    }

    private void SetBuyArtifactSlotVisible(int slotIndex, bool value)
    {
        if (
            buyArtifactSlotRoots != null
            && buyArtifactSlotRoots.Length > 0
            && slotIndex < buyArtifactSlotRoots.Length
            && buyArtifactSlotRoots[slotIndex] != null
        )
        {
            buyArtifactSlotRoots[slotIndex].SetActive(value);
            return;
        }

        if (
            buyArtifactSlotButtons != null
            && slotIndex < buyArtifactSlotButtons.Length
            && buyArtifactSlotButtons[slotIndex] != null
        )
        {
            buyArtifactSlotButtons[slotIndex].gameObject.SetActive(value);
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

    private int GetBuyArtifactVisualSlotCount()
    {
        if (buyArtifactSlotButtons == null)
        {
            return 0;
        }

        int count = buyArtifactSlotButtons.Length;

        if (buyArtifactSlotIcons != null && buyArtifactSlotIcons.Length > 0)
        {
            count = Mathf.Min(count, buyArtifactSlotIcons.Length);
        }

        if (buyArtifactSlotTexts != null && buyArtifactSlotTexts.Length > 0)
        {
            count = Mathf.Min(count, buyArtifactSlotTexts.Length);
        }

        if (buyArtifactSlotRoots != null && buyArtifactSlotRoots.Length > 0)
        {
            count = Mathf.Min(count, buyArtifactSlotRoots.Length);
        }

        return count;
    }

    private void OnGoldChanged(int currentGold)
    {
        RefreshGoldText(currentGold);
    }

    private void RefreshGoldText(int currentGold)
    {
        string text = $"골드: {currentGold}";

        if (currentGoldText != null)
        {
            currentGoldText.text = text;
        }

        if (buyEquipmentGoldText != null)
        {
            buyEquipmentGoldText.text = text;
        }

        if (buyArtifactGoldText != null)
        {
            buyArtifactGoldText.text = text;
        }

        if (sellGladiatorGoldText != null)
        {
            sellGladiatorGoldText.text = text;
        }

        if (sellEquipmentGoldText != null)
        {
            sellEquipmentGoldText.text = text;
        }

        if (sellArtifactGoldText != null)
        {
            sellArtifactGoldText.text = text;
        }
    }

    private void CloseAllSubPanels()
    {
        SetPanelActive(buyGladiatorPanel, false);
        SetPanelActive(buyEquipmentPanel, false);
        SetPanelActive(sellGladiatorPanel, false);
        SetPanelActive(sellEquipmentPanel, false);
        SetPanelActive(buyArtifactPanel, false);
        SetPanelActive(sellArtifactPanel, false);
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

    // Inspector에 미할당 시 씬 전체 탐색으로 레퍼런스 캐싱
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
}
