using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GladiatorManager : SingletonBehaviour<GladiatorManager>
{
    [SerializeField] private bool verboseLog = true;

    private readonly List<OwnedGladiatorData> _ownedGladiators = new List<OwnedGladiatorData>();        // 플레이어가 실제로 보유 중인 검투사 목록.
                                                                                                        // 시장 preview나 전투 snapshot과 분리된 실제 소유 데이터다.
                                                                                                        // 이렇게 분리한 이유는, 다른 로직들 내에서 어떤 방식으로든 변형되는 걸 방지하기 위해

    private BalanceSO _balance;
    private RandomManager _randomManager;
    private bool _initialized;
    private int _nextRuntimeId = 1;            // 새로 보유하게 되는 검투사에게 부여할 고유 런타임 ID

    public IReadOnlyList<OwnedGladiatorData> OwnedGladiators => _ownedGladiators;

    protected override void Awake()
    {
        base.Awake();

        if (!IsPrimaryInstance)
        {
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    public void Initialize(BalanceSO balance, RandomManager randomManager)
    {
        if (_initialized)
        {
            return;
        }

        _balance = balance;
        _randomManager = randomManager;
        _ownedGladiators.Clear();
        _nextRuntimeId = 1;

        if (_balance == null)
        {
            Debug.LogError("[GladiatorManager] balance is null.", this);
            return;
        }

        if (_randomManager == null)
        {
            Debug.LogError("[GladiatorManager] randomManager is null.", this);
            return;
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[GladiatorManager] Initialized. Owned gladiator contract ready.", this);
        }
    }

    public int GetOwnedGladiatorCount()
    {
        return _ownedGladiators.Count;
    }

    // 보유 검투사를 목록에서 제거함.
    // 제거 전에 장착 무기를 자동 해제해서 장착 참조가 남지 않게 한다.
    public bool RemoveOwnedGladiator(OwnedGladiatorData gladiator)
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] RemoveOwnedGladiator called before Initialize.", this);
            return false;
        }

        if (gladiator == null)
        {
            Debug.LogError("[GladiatorManager] gladiator is null.", this);
            return false;
        }

        UnequipWeaponIfAny(gladiator);

        bool removed = _ownedGladiators.Remove(gladiator);

        if (!removed)
        {
            Debug.LogWarning(
                $"[GladiatorManager] RemoveOwnedGladiator failed. Name={gladiator.DisplayName}",
                this
            );
            return false;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[GladiatorManager] Gladiator removed. Name={gladiator.DisplayName}, RuntimeId={gladiator.RuntimeId}",
                this
            );
        }

        return true;
    }

    // 특정 무기를 현재 누가 장착 중인지 찾음
    // 무기 중복 장착 방지와 '장착중인 무기 판매' 차단을 위해
    public OwnedGladiatorData FindOwnerOfEquippedWeapon(OwnedWeaponData weapon)
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] FindOwnerOfEquippedWeapon called before Initialize.", this);
            return null;
        }

        if (weapon == null)
        {
            return null;
        }

        for (int i = 0; i < _ownedGladiators.Count; i++)
        {
            OwnedGladiatorData gladiator = _ownedGladiators[i];
            if (gladiator == null)
            {
                continue;
            }

            if (gladiator.EquippedWeapon == weapon)
            {
                return gladiator;
            }
        }

        return null;
    }

    // 무기를 해당 검투사에게 장ㅊ착함.
    // 이미 다른 검투사가 쓰는 무기면 막음.
    // 장착 성공 시 장비 보너스를 포함해 스탯을 다시 계산한다
    public bool TryEquipWeapon(OwnedGladiatorData gladiator, OwnedWeaponData weapon, out string failReason)
    {
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "GladiatorManager is not initialized.";
            Debug.LogError("[GladiatorManager] " + failReason, this);
            return false;
        }

        if (gladiator == null)
        {
            failReason = "Target gladiator is null.";
            Debug.LogError("[GladiatorManager] " + failReason, this);
            return false;
        }

        if (weapon == null)
        {
            failReason = "Target weapon is null.";
            Debug.LogError("[GladiatorManager] " + failReason, this);
            return false;
        }

        OwnedGladiatorData currentOwner = FindOwnerOfEquippedWeapon(weapon);
        if (currentOwner != null && currentOwner != gladiator)
        {
            failReason = "already equipped!";
            return false;
        }

        if (gladiator.EquippedWeapon == weapon)
        {
            failReason = "This gladiator already has this weapon equipped.";
            return false;
        }

        gladiator.EquippedWeapon = weapon;
        RefreshDerivedStats(gladiator, false);

        if (verboseLog)
        {
            Debug.Log(
                $"[GladiatorManager] Weapon equipped. Gladiator={gladiator.DisplayName}, Weapon={weapon.DisplayName}",
                this
            );
        }

        return true;
    }

    // 무기 해제, 기준 스탯 다시 계산
    public bool TryUnequipWeapon(OwnedGladiatorData gladiator, out string failReason)
    {
        failReason = string.Empty;

        if (!_initialized)
        {
            failReason = "GladiatorManager is not initialized.";
            Debug.LogError("[GladiatorManager] " + failReason, this);
            return false;
        }

        if (gladiator == null)
        {
            failReason = "Target gladiator is null.";
            Debug.LogError("[GladiatorManager] " + failReason, this);
            return false;
        }

        if (gladiator.EquippedWeapon == null)
        {
            failReason = "There is no equipped weapon to unequip.";
            return false;
        }

        OwnedWeaponData removedWeapon = gladiator.EquippedWeapon;
        gladiator.EquippedWeapon = null;
        RefreshDerivedStats(gladiator, false);

        if (verboseLog)
        {
            Debug.Log(
                $"[GladiatorManager] Weapon unequipped. Gladiator={gladiator.DisplayName}, Weapon={removedWeapon.DisplayName}",
                this
            );
        }

        return true;
    }

    // 판매/삭제 같은 검투사 상태 변경 전에 강제로 무기를 떼는 방어용 함수
    public void UnequipWeaponIfAny(OwnedGladiatorData gladiator)
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] UnequipWeaponIfAny called before Initialize.", this);
            return;
        }

        if (gladiator == null)
        {
            Debug.LogError("[GladiatorManager] gladiator is null.", this);
            return;
        }

        if (gladiator.EquippedWeapon == null)
        {
            return;
        }

        gladiator.EquippedWeapon = null;
        RefreshDerivedStats(gladiator, false);

        if (verboseLog)
        {
            Debug.Log($"[GladiatorManager] Weapon forcibly unequipped due to owner state change. Gladiator={gladiator.DisplayName}", this);
        }
    }

    // 시장 preview를 실제 보유 검투사 데이터로 복사해 목록에 추가함.
    // 즉, 시장 데이터 자체를 들고 오는 게 아니라 새 owned 인스턴스를 만든다
    // 시장에서 인스턴스를 factory에 요청해서 떼어오고 -> 시장에서 가지고 있거나 참조를 들고 있다가 -> 검투사나 인벤토리 매니저에 직접 인스턴스를 넘겨주는 방식은
    // 참조 관련 버그나 인스턴스 관련 버그가 일어나는 경우가 있어서 아예 새 인스턴스로 깔끔하게 다시 만드는 쪽을 쓴 것
    public bool AddPurchasedGladiatorFromMarketPreview(OwnedGladiatorData marketPreview)
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] AddPurchasedGladiatorFromMarketPreview called before Initialize.", this);
            return false;
        }

        if (marketPreview == null)
        {
            Debug.LogError("[GladiatorManager] marketPreview is null.", this);
            return false;
        }

        if (marketPreview.GladiatorClass == null)
        {
            Debug.LogError("[GladiatorManager] marketPreview.GladiatorClass is null.", this);
            return false;
        }

        OwnedGladiatorData purchased = new OwnedGladiatorData(
            _nextRuntimeId++,
            marketPreview.DisplayName,
            marketPreview.Level,
            marketPreview.Exp,
            marketPreview.Loyalty,
            marketPreview.Upkeep,
            marketPreview.GladiatorClass,
            marketPreview.Trait,
            marketPreview.Personality,
            marketPreview.EquippedPerk,
            marketPreview.EquippedWeapon
        );

        purchased.FinalHealthVariancePercent = marketPreview.FinalHealthVariancePercent;
        purchased.FinalAttackVariancePercent = marketPreview.FinalAttackVariancePercent;

        purchased.CachedMaxHealth = marketPreview.CachedMaxHealth;
        purchased.CurrentHealth = marketPreview.CachedMaxHealth;
        purchased.CachedAttack = marketPreview.CachedAttack;
        purchased.CachedAttackSpeed = marketPreview.CachedAttackSpeed;
        purchased.CachedMoveSpeed = marketPreview.CachedMoveSpeed;
        purchased.CachedAttackRange = marketPreview.CachedAttackRange;

        _ownedGladiators.Add(purchased);

        if (verboseLog)
        {
            Debug.Log(
                $"[GladiatorManager] Purchased gladiator added. " +
                $"Name={purchased.DisplayName}, Level={purchased.Level}, Loyalty={purchased.Loyalty}",
                this
            );
        }

        return true;
    }

    // 게임 시작 시 스타터 검투사들을 생성해 보유 목록에 넣음.
    // 이미 보유 검투사가 있으면 중복 지급 아ㅣㄴ함
    public void GrantRandomStarterGladiator(ContentDatabaseProvider contentDatabaseProvider, SessionManager sessionManager)
    {
        GrantRandomStarterGladiators(contentDatabaseProvider, sessionManager, 1);
    }

    public void GrantRandomStarterGladiators(
        ContentDatabaseProvider contentDatabaseProvider,
        SessionManager sessionManager,
        int count = 6)
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] GrantRandomStarterGladiators called before Initialize.", this);
            return;
        }

        if (contentDatabaseProvider == null)
        {
            Debug.LogError("[GladiatorManager] contentDatabaseProvider is null.", this);
            return;
        }

        if (sessionManager == null)
        {
            Debug.LogError("[GladiatorManager] sessionManager is null.", this);
            return;
        }

        if (_ownedGladiators.Count > 0)
        {
            if (verboseLog)
            {
                Debug.Log("[GladiatorManager] Starter grant skipped because owned gladiator list is already populated.", this);
            }

            return;
        }

        int safeCount = Mathf.Max(1, count);
        int grantedCount = 0;

        for (int i = 0; i < safeCount; i++)
        {
            OwnedGladiatorData starter = CreateStarterGladiatorInternal(contentDatabaseProvider, sessionManager);
            if (starter == null)
            {
                continue;
            }

            _ownedGladiators.Add(starter);
            grantedCount++;

            if (verboseLog)
            {
                Debug.Log(
                    $"[GladiatorManager] Starter gladiator granted. " +
                    $"Name={starter.DisplayName}, Trait={starter.Trait.traitName}, " +
                    $"Personality={starter.Personality.personalityName}, " +
                    $"Level={starter.Level}, Loyalty={starter.Loyalty}",
                    this
                );
            }
        }

        if (verboseLog)
        {
            Debug.Log($"[GladiatorManager] Starter gladiator batch complete. Requested={safeCount}, Granted={grantedCount}", this);
        }
    }

    // 스타터 검투사 한명을 실제로 생성
    private OwnedGladiatorData CreateStarterGladiatorInternal(
        ContentDatabaseProvider contentDatabaseProvider,
        SessionManager sessionManager)
    {
        GladiatorClassSO gladiatorTemplate = contentDatabaseProvider.GladiatorTemplate;
        TraitSO trait = PickRandomNonNull(contentDatabaseProvider.Traits, RandomStreamType.Recruit);
        PersonalitySO personality = PickRandomNonNull(contentDatabaseProvider.Personalities, RandomStreamType.Recruit);

        if (gladiatorTemplate == null)
        {
            Debug.LogError("[GladiatorManager] Exactly one valid GladiatorClassSO is required for starter grant.", this);
            return null;
        }

        if (trait == null)
        {
            Debug.LogError("[GladiatorManager] No valid TraitSO found for starter grant.", this);
            return null;
        }

        if (personality == null)
        {
            Debug.LogError("[GladiatorManager] No valid PersonalitySO found for starter grant.", this);
            return null;
        }

        string displayName = sessionManager.ConsumeNextClassName(gladiatorTemplate.className);

        int level = 1;
        int exp = 0;
        int loyalty = RollLoyaltyFromPersonality(personality, RandomStreamType.Recruit);

        int upkeepPerLevel = _balance != null ? _balance.upkeepPerLevel : 10;
        int upkeep = Mathf.Max(0, upkeepPerLevel * level);

        OwnedGladiatorData starter = new OwnedGladiatorData(
            _nextRuntimeId++,
            displayName,
            level,
            exp,
            loyalty,
            upkeep,
            gladiatorTemplate,
            trait,
            personality,
            null,
            null
        );

        starter.FinalHealthVariancePercent = 0f;
        starter.FinalAttackVariancePercent = 0f;

        RefreshDerivedStats(starter, true);
        return starter;
    }

    // 전투 보상 정산이 끝난 뒤,
    // 모든 보유 검투사에게 승리 XP를 일괄 지급.
    // MainScene에서 펜딩 보상 지급 성공 직후 호출됨.
    public void GrantVictoryXpToAllOwnedGladiators()
    {
        int victoryXpAmount = _balance != null ? _balance.eodXpGainAmount : 500;
        GrantXpToAllOwnedGladiators(victoryXpAmount, "Victory XP");
    }

    // 전체 보유 검투사에게 XP를 주고,
    // (레벨업을 한 검투사가 존재할 시) 레벨업과 스탯 재계산까지 처리함
    // 리팩토링 대상: 검투사 하나에게 경험치를 지급하는 함수는 현재 존재 안함
    public void GrantXpToAllOwnedGladiators(int xpAmount, string logReason = "Cheat XP")
    {
        if (!_initialized)
        {
            Debug.LogError("[GladiatorManager] GrantXpToAllOwnedGladiators called before Initialize.", this);
            return;
        }

        if (_ownedGladiators.Count == 0)
        {
            if (verboseLog)
            {
                Debug.Log("[GladiatorManager] GrantXpToAllOwnedGladiators skipped because there are no owned gladiators.", this);
            }

            return;
        }

        int safeXpAmount = Mathf.Max(0, xpAmount);
        int totalLevelUps = 0;

        for (int i = 0; i < _ownedGladiators.Count; i++)
        {
            OwnedGladiatorData gladiator = _ownedGladiators[i];
            if (gladiator == null)
            {
                continue;
            }

            gladiator.Exp += safeXpAmount;

            int levelUpCount = ProcessLevelUps(gladiator);
            totalLevelUps += levelUpCount;

            if (verboseLog)
            {
                Debug.Log(
                    $"[GladiatorManager] {logReason} granted. " +
                    $"Name={gladiator.DisplayName}, AddedXp={safeXpAmount}, Level={gladiator.Level}, Exp={gladiator.Exp}, LevelUps={levelUpCount}",
                    this
                );
            }
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[GladiatorManager] {logReason} batch complete. OwnedCount={_ownedGladiators.Count}, TotalLevelUps={totalLevelUps}",
                this
            );
        }
    }

    // 레벨 상승 후 유지비와 캐시 스탯을 다시 계산
    private int ProcessLevelUps(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return 0;
        }

        int levelUpCount = 0;

        while (true)
        {
            int requiredXp = GetRequiredXpForCurrentLevel(gladiator.Level);
            if (gladiator.Exp < requiredXp)
            {
                break;
            }

            gladiator.Exp -= requiredXp;
            gladiator.Level++;
            levelUpCount++;
        }

        if (levelUpCount > 0)
        {
            int upkeepPerLevel = _balance != null ? _balance.upkeepPerLevel : 10;
            gladiator.Upkeep = Mathf.Max(0, upkeepPerLevel * gladiator.Level);
            RefreshDerivedStats(gladiator, true);
        }

        return levelUpCount;
    }

    private int GetRequiredXpForCurrentLevel(int currentLevel)
    {
        int xpPerLevelMultiplier = _balance != null ? _balance.xpPerLevelMultiplier : 100;
        return Mathf.Max(1, currentLevel) * Mathf.Max(1, xpPerLevelMultiplier);
    }

    // 검투사 최종 스탯 계산.
    // 클래스 기본치, 성장치, 개체 분산, 장착 무기 보너스를 합쳐 캐시 스탯을 만든다
    private void RefreshDerivedStats(OwnedGladiatorData gladiator, bool fullyHeal)
    {
        if (gladiator == null)
        {
            Debug.LogError("[GladiatorManager] RefreshDerivedStats received null gladiator.", this);
            return;
        }

        if (gladiator.GladiatorClass == null)
        {
            Debug.LogError("[GladiatorManager] RefreshDerivedStats failed because GladiatorClass is null.", this);
            return;
        }

        float oldCurrentHealth = gladiator.CurrentHealth;
        float oldMaxHealth = gladiator.CachedMaxHealth;

        int levelOffset = Mathf.Max(0, gladiator.Level - 1);

        float baseHealth = Mathf.Max(0f, gladiator.GladiatorClass.baseHealth);
        float healthGrowthPerLevel = Mathf.Max(0f, gladiator.GladiatorClass.healthGrowthPerLevel);
        float scaledHealth = baseHealth + (healthGrowthPerLevel * levelOffset);

        float baseAttack = Mathf.Max(0f, gladiator.GladiatorClass.baseAttack);
        float attackGrowthPerLevel = Mathf.Max(0f, gladiator.GladiatorClass.attackGrowthPerLevel);
        float scaledAttack = baseAttack + (attackGrowthPerLevel * levelOffset);

        float baseAttackSpeed = Mathf.Max(0f, gladiator.GladiatorClass.attackSpeed);
        float baseMoveSpeed = Mathf.Max(0f, gladiator.GladiatorClass.moveSpeed);
        float baseAttackRange = Mathf.Max(0f, gladiator.GladiatorClass.attackRange);

        float finalHealthMultiplier = 1f + gladiator.FinalHealthVariancePercent;
        float finalAttackMultiplier = 1f + gladiator.FinalAttackVariancePercent;

        if (finalHealthMultiplier < 0f)
        {
            finalHealthMultiplier = 0f;
        }

        if (finalAttackMultiplier < 0f)
        {
            finalAttackMultiplier = 0f;
        }

        float weaponHealthBonus = 0f;
        float weaponAttackBonus = 0f;
        float weaponAttackSpeedBonus = 0f;
        float weaponMoveSpeedBonus = 0f;
        float weaponAttackRangeBonus = 0f;

        if (gladiator.EquippedWeapon != null)
        {
            weaponHealthBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedHealthBonus);
            weaponAttackBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackBonus);
            weaponAttackSpeedBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackSpeedBonus);
            weaponMoveSpeedBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedMoveSpeedBonus);
            weaponAttackRangeBonus = Mathf.Max(0f, gladiator.EquippedWeapon.CachedAttackRangeBonus);
        }

        float newMaxHealth = (scaledHealth * finalHealthMultiplier) + weaponHealthBonus;
        float newAttack = (scaledAttack * finalAttackMultiplier) + weaponAttackBonus;
        float newAttackSpeed = baseAttackSpeed + weaponAttackSpeedBonus;
        float newMoveSpeed = baseMoveSpeed + weaponMoveSpeedBonus;
        float newAttackRange = baseAttackRange + weaponAttackRangeBonus;

        gladiator.CachedMaxHealth = Mathf.Max(0f, newMaxHealth);
        gladiator.CachedAttack = Mathf.Max(0f, newAttack);
        gladiator.CachedAttackSpeed = Mathf.Max(0f, newAttackSpeed);
        gladiator.CachedMoveSpeed = Mathf.Max(0f, newMoveSpeed);
        gladiator.CachedAttackRange = Mathf.Max(0f, newAttackRange);

        if (fullyHeal)
        {
            gladiator.CurrentHealth = gladiator.CachedMaxHealth;
        }
        else
        {
            if (gladiator.CachedMaxHealth > oldMaxHealth)
            {
                float gainedMaxHealth = gladiator.CachedMaxHealth - oldMaxHealth;
                gladiator.CurrentHealth = Mathf.Clamp(oldCurrentHealth + gainedMaxHealth, 0f, gladiator.CachedMaxHealth);
            }
            else
            {
                gladiator.CurrentHealth = Mathf.Clamp(oldCurrentHealth, 0f, gladiator.CachedMaxHealth);
            }
        }
    }

    private int RollLoyaltyFromPersonality(PersonalitySO personality, RandomStreamType streamType)
    {
        int min = _balance != null ? _balance.loyaltyMin : 0;
        int max = _balance != null ? _balance.loyaltyMax : 100;

        if (max < min)
        {
            max = min;
        }

        int mean = personality != null ? personality.baseLoyalty : min;
        mean = Mathf.Clamp(mean, min, max);

        float sigma = mean / 3f;
        if (sigma <= 0f)
        {
            return mean;
        }

        float sampled = NextGaussian(mean, sigma, streamType);
        int rounded = Mathf.RoundToInt(sampled);
        return Mathf.Clamp(rounded, min, max);
    }

    private float NextGaussian(float mean, float standardDeviation, RandomStreamType streamType)
    {
        if (standardDeviation <= 0f)
        {
            return mean;
        }

        float u1 = Mathf.Clamp(_randomManager.NextFloatRange(streamType, 0.0001f, 1f), 0.0001f, 1f);
        float u2 = Mathf.Clamp01(_randomManager.NextFloatRange(streamType, 0f, 1f));

        float radius = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float theta = 2f * Mathf.PI * u2;
        float standardNormal = radius * Mathf.Cos(theta);

        return mean + standardDeviation * standardNormal;
    }

    private T PickRandomNonNull<T>(IReadOnlyList<T> list, RandomStreamType streamType) where T : class
    {
        if (list == null || list.Count == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int targetIndex = _randomManager.NextInt(streamType, 0, validCount);

        for (int i = 0; i < list.Count; i++)
        {
            T item = list[i];
            if (item == null)
            {
                continue;
            }

            if (targetIndex == 0)
            {
                return item;
            }

            targetIndex--;
        }

        return null;
    }
}
