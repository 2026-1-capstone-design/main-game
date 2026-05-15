using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MarketManager : SingletonBehaviour<MarketManager>
{
    [SerializeField]
    private bool verboseLog = true;

    // _gladiatorOffers 와 _weaponOffers 모두 날짜가 바뀌기 전까지만 유지되는 하루 단위 캐시
    private readonly List<MarketGladiatorOffer> _gladiatorOffers = new List<MarketGladiatorOffer>();
    private readonly List<MarketWeaponOffer> _weaponOffers = new List<MarketWeaponOffer>();

    private RecruitFactory _recruitFactory;
    private EquipmentFactory _equipmentFactory;
    private GladiatorManager _gladiatorManager;
    private InventoryManager _inventoryManager;
    private ResourceManager _resourceManager;

    private int _initializedDay = -1; // 현재 시장 재고가 어느 날짜 기준으로 생성됐는지 나타냄

    // 같은 날 재진입 시(배틀 종료 후 등_) 재생성을 막는 기준값
    public int InitializedDay => _initializedDay;

    private bool _initialized;

    public IReadOnlyList<MarketGladiatorOffer> GladiatorOffers => _gladiatorOffers;
    public IReadOnlyList<MarketWeaponOffer> WeaponOffers => _weaponOffers;

    public void RestoreOffersForLoad(
        int initializedDay,
        List<MarketGladiatorOffer> gladiatorOffers,
        List<MarketWeaponOffer> weaponOffers
    )
    {
        if (!_initialized)
        {
            Debug.LogError("[MarketManager] RestoreOffersForLoad called before Initialize.", this);
            return;
        }

        _gladiatorOffers.Clear();
        _weaponOffers.Clear();

        if (gladiatorOffers != null)
        {
            _gladiatorOffers.AddRange(gladiatorOffers);
        }

        if (weaponOffers != null)
        {
            _weaponOffers.AddRange(weaponOffers);
        }

        _initializedDay = Mathf.Max(1, initializedDay);

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketManager] Offers restored from save. Day={_initializedDay}, GladiatorOffers={_gladiatorOffers.Count}, WeaponOffers={_weaponOffers.Count}",
                this
            );
        }
    }

    // 시장이 참조할 factory와 실제 보유/골드 매니저를 연결
    // 마켓 매니저가 DDOL 매니저라서 메인씬 재진입 시 scene 의존성을 다시 꽂아주는 역할도 함
    public void Initialize(
        RecruitFactory recruitFactory,
        EquipmentFactory equipmentFactory,
        GladiatorManager gladiatorManager,
        InventoryManager inventoryManager,
        ResourceManager resourceManager
    )
    {
        _recruitFactory = recruitFactory;
        _equipmentFactory = equipmentFactory;
        _gladiatorManager = gladiatorManager;
        _inventoryManager = inventoryManager;
        _resourceManager = resourceManager;

        if (_recruitFactory == null)
        {
            Debug.LogError("[MarketManager] recruitFactory is null.", this);
            return;
        }

        if (_equipmentFactory == null)
        {
            Debug.LogError("[MarketManager] equipmentFactory is null.", this);
            return;
        }

        if (_gladiatorManager == null)
        {
            Debug.LogError("[MarketManager] gladiatorManager is null.", this);
            return;
        }

        if (_inventoryManager == null)
        {
            Debug.LogError("[MarketManager] inventoryManager is null.", this);
            return;
        }

        if (_resourceManager == null)
        {
            Debug.LogError("[MarketManager] resourceManager is null.", this);
            return;
        }

        bool wasInitialized = _initialized;
        _initialized = true;

        if (!wasInitialized)
        {
            _gladiatorOffers.Clear();
            _weaponOffers.Clear();

            if (verboseLog)
            {
                Debug.Log("[MarketManager] Initialized.", this);
            }
        }
        else
        {
            if (verboseLog)
            {
                Debug.Log("[MarketManager] Scene dependencies rebound.", this);
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();

        if (!IsPrimaryInstance)
        {
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    // 해당 날짜의 시장 재고를 생성
    // 이미 같은 날짜에 재고가 있으면 그대로 유지, 날짜가 바뀌었을 때만 새로 만든다.
    public void InitializeDay(int currentDay)
    {
        if (!_initialized)
        {
            Debug.LogError("[MarketManager] InitializeDay called before Initialize.", this);
            return;
        }

        int safeDay = Mathf.Max(1, currentDay);

        if (_initializedDay == safeDay && (_gladiatorOffers.Count > 0 || _weaponOffers.Count > 0))
        {
            if (verboseLog)
            {
                Debug.Log($"[MarketManager] InitializeDay skipped. Market already initialized for Day={safeDay}", this);
            }

            return;
        }

        _gladiatorOffers.Clear();
        _weaponOffers.Clear();

        int gladiatorSlotCount = GetConfiguredGladiatorSlotCount();
        for (int i = 0; i < gladiatorSlotCount; i++)
        {
            MarketGladiatorOffer offer = _recruitFactory.CreateMarketGladiatorOffer(safeDay, i);
            _gladiatorOffers.Add(offer);
        }

        int weaponSlotCount = GetConfiguredWeaponSlotCount();
        for (int i = 0; i < weaponSlotCount; i++)
        {
            MarketWeaponOffer offer = _equipmentFactory.CreateMarketWeaponOffer(safeDay, i);
            _weaponOffers.Add(offer);
        }

        _initializedDay = safeDay;

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketManager] InitializeDay({safeDay}) complete. "
                    + $"GladiatorOfferCount={_gladiatorOffers.Count}, WeaponOfferCount={_weaponOffers.Count}",
                this
            );
        }
    }

    public MarketGladiatorOffer GetGladiatorOffer(int slotIndex)
    {
        for (int i = 0; i < _gladiatorOffers.Count; i++)
        {
            if (_gladiatorOffers[i] != null && _gladiatorOffers[i].SlotIndex == slotIndex)
            {
                return _gladiatorOffers[i];
            }
        }

        return null;
    }

    public MarketWeaponOffer GetWeaponOffer(int slotIndex)
    {
        for (int i = 0; i < _weaponOffers.Count; i++)
        {
            if (_weaponOffers[i] != null && _weaponOffers[i].SlotIndex == slotIndex)
            {
                return _weaponOffers[i];
            }
        }

        return null;
    }

    public int GetGladiatorSellPrice(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return 0;
        }

        BalanceSO balance = _recruitFactory != null ? _recruitFactory.Balance : null;
        if (balance == null)
        {
            return 0;
        }

        int basePrice = CalculateApproximateGladiatorPrice(gladiator, balance);
        if (basePrice <= 0 || balance.gladiatorBuyPricePerLevel <= 0)
        {
            return 0;
        }

        float sellRatio = balance.gladiatorSellPricePerLevel / (float)balance.gladiatorBuyPricePerLevel;
        return Mathf.Max(0, Mathf.RoundToInt(basePrice * sellRatio));
    }

    public int GetWeaponSellPrice(OwnedWeaponData weapon)
    {
        if (weapon == null)
        {
            return 0;
        }

        BalanceSO balance = _equipmentFactory != null ? _equipmentFactory.Balance : null;
        if (balance == null)
        {
            return 0;
        }

        int basePrice = _equipmentFactory != null ? _equipmentFactory.CalculateWeaponPrice(weapon) : 0;
        if (basePrice <= 0 || balance.weaponBuyPricePerLevel <= 0)
        {
            return 0;
        }

        float sellRatio = balance.weaponSellPricePerLevel / (float)balance.weaponBuyPricePerLevel;
        return Mathf.Max(0, Mathf.RoundToInt(basePrice * sellRatio));
    }

    // 검투사 구매를 책임지는 함수.
    // 골드 차감 -> preview를 실제 보유 검투사로 복사 추가 -> 슬롯 sold 처리 순서로 진행된다.
    // 중간 실패 시 차감한 골드는 즉시 롤백
    public bool TryBuyGladiator(int slotIndex, out string failReason)
    {
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "MarketManager is not initialized.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        if (slotIndex < 0 || slotIndex >= _gladiatorOffers.Count)
        {
            failReason = "Invalid gladiator slot index.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        MarketGladiatorOffer offer = _gladiatorOffers[slotIndex];
        if (offer == null || offer.Gladiator == null)
        {
            failReason = "This market slot is empty.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (offer.IsSold)
        {
            failReason = "This gladiator is already sold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (!_resourceManager.CanAfford(offer.Price))
        {
            failReason = "Not enough gold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (!_resourceManager.TrySpendGold(offer.Price))
        {
            failReason = "Failed to spend gold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        bool addSucceeded = _gladiatorManager.AddPurchasedGladiatorFromMarketPreview(offer.Gladiator);
        if (!addSucceeded)
        {
            _resourceManager.AddGold(offer.Price);
            failReason = "Failed to add purchased gladiator.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        offer.MarkSold();

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketManager] Gladiator purchased. "
                    + $"Slot={slotIndex}, Name={offer.Gladiator.DisplayName}, Price={offer.Price}",
                this
            );
        }

        return true;
    }

    // TryBuyGladiato 플로우 참고.
    public bool TryBuyWeapon(int slotIndex, out string failReason)
    {
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "MarketManager is not initialized.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        if (slotIndex < 0 || slotIndex >= _weaponOffers.Count)
        {
            failReason = "Invalid weapon slot index.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        MarketWeaponOffer offer = _weaponOffers[slotIndex];
        if (offer == null || offer.Weapon == null)
        {
            failReason = "This market slot is empty.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (offer.IsSold)
        {
            failReason = "This weapon is already sold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (!_resourceManager.CanAfford(offer.Price))
        {
            failReason = "Not enough gold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        if (!_resourceManager.TrySpendGold(offer.Price))
        {
            failReason = "Failed to spend gold.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        bool addSucceeded = _inventoryManager.AddPurchasedWeaponFromMarketPreview(offer.Weapon);
        if (!addSucceeded)
        {
            _resourceManager.AddGold(offer.Price);
            failReason = "Failed to add purchased weapon.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        offer.MarkSold();

        if (verboseLog)
        {
            Debug.Log(
                $"[MarketManager] Weapon purchased. "
                    + $"Slot={slotIndex}, Name={offer.Weapon.DisplayName}, Price={offer.Price}",
                this
            );
        }

        return true;
    }

    // 보유 검투사를 판매하고 골드를 지급.
    // 판매 과정에서 GladiatorManager가 장착 무기를 먼저 자동 해제함.
    public bool TrySellGladiator(OwnedGladiatorData gladiator, out int sellPrice, out string failReason)
    {
        sellPrice = 0;
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "MarketManager is not initialized.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        if (gladiator == null)
        {
            failReason = "Target gladiator is null.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        sellPrice = GetGladiatorSellPrice(gladiator);

        bool removed = _gladiatorManager.RemoveOwnedGladiator(gladiator);
        if (!removed)
        {
            failReason = "Failed to remove gladiator from owned list.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        _resourceManager.AddGold(sellPrice);

        if (verboseLog)
        {
            Debug.Log($"[MarketManager] Gladiator sold. Name={gladiator.DisplayName}, SellPrice={sellPrice}", this);
        }

        return true;
    }

    // 보유 무기를 판매하고 골드를 지급한다.
    // 장착 중 무기 판매를 막기 위해 삭제 전에 현재 owner를 먼저 검사함
    public bool TrySellWeapon(OwnedWeaponData weapon, out int sellPrice, out string failReason)
    {
        sellPrice = 0;
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "MarketManager is not initialized.";
            Debug.LogError("[MarketManager] " + failReason, this);
            return false;
        }

        if (weapon == null)
        {
            failReason = "Target weapon is null.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }
        //이중 방어
        OwnedGladiatorData owner =
            _gladiatorManager != null ? _gladiatorManager.FindOwnerOfEquippedWeapon(weapon) : null;

        if (owner != null)
        {
            failReason = "You can't sell equipped items.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        sellPrice = GetWeaponSellPrice(weapon);

        bool removed = _inventoryManager.RemoveOwnedWeapon(weapon);

        if (!removed)
        {
            failReason = "Failed to remove weapon from owned list.";
            Debug.LogWarning("[MarketManager] " + failReason, this);
            return false;
        }

        _resourceManager.AddGold(sellPrice);

        if (verboseLog)
        {
            Debug.Log($"[MarketManager] Weapon sold. Name={weapon.DisplayName}, SellPrice={sellPrice}", this);
        }

        return true;
    }

    private int GetConfiguredGladiatorSlotCount()
    {
        BalanceSO balance = _recruitFactory != null ? _recruitFactory.Balance : null;
        if (balance == null)
        {
            return 4;
        }

        return Mathf.Max(0, balance.marketGladiatorSlots);
    }

    private static int CalculateApproximateGladiatorPrice(OwnedGladiatorData gladiator, BalanceSO balance)
    {
        if (gladiator == null || balance == null)
        {
            return 0;
        }

        int baseMarketPrice = Mathf.Max(0, balance.gladiatorBaseMarketPrice);
        int averagePerLevel = Mathf.RoundToInt(
            (
                Mathf.Max(0, balance.gladiatorMarketPricePerLevelMin)
                + Mathf.Max(0, balance.gladiatorMarketPricePerLevelMax)
            ) * 0.5f
        );

        int levelPrice = averagePerLevel * Mathf.Max(1, gladiator.Level);
        int statDeltaPrice = CalculateGladiatorStatDeltaPrice(gladiator);
        return Mathf.Max(0, baseMarketPrice + levelPrice + statDeltaPrice);
    }

    private static int CalculateGladiatorStatDeltaPrice(OwnedGladiatorData gladiator)
    {
        if (gladiator == null || gladiator.GladiatorClass == null)
        {
            return 0;
        }

        float baseHealth = gladiator.GladiatorClass.baseHealth;
        float baseAttack = gladiator.GladiatorClass.baseAttack;
        float baseAttackSpeed = gladiator.GladiatorClass.attackSpeed;
        float baseMoveSpeed = gladiator.GladiatorClass.moveSpeed;
        float baseAttackRange = gladiator.GladiatorClass.attackRange;

        // 추가 스탯 계산
        float additionalHealth = Mathf.Max(0f, gladiator.CachedMaxHealth - baseHealth);
        float additionalMoveSpeed = Mathf.Max(0f, gladiator.CachedMoveSpeed - baseMoveSpeed);
        float additionalAttackRange = Mathf.Max(0f, gladiator.CachedAttackRange - baseAttackRange);

        // DPS 계산
        float currentAttack = gladiator.CachedAttack;
        float currentAttackSpeed = gladiator.CachedAttackSpeed;
        float baseDps = baseAttack * baseAttackSpeed;
        float currentDps = currentAttack * currentAttackSpeed;

        float offensivePrice = 0f;
        if (baseDps > 0f)
        {
            // RecruitFactory와 동일한 완벽한 DPS 환산 공식 적용
            offensivePrice = (currentDps / baseDps) * baseAttack * 50f;
        }

        return Mathf.RoundToInt(
            (additionalHealth * 1f) + offensivePrice + (additionalMoveSpeed * 700f) + (additionalAttackRange * 700f)
        );
    }

    private int GetConfiguredWeaponSlotCount()
    {
        BalanceSO balance = _equipmentFactory != null ? _equipmentFactory.Balance : null;
        if (balance == null)
        {
            return 4;
        }

        return Mathf.Max(0, balance.marketWeaponSlots);
    }
}
