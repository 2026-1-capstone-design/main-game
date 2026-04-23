using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RecruitFactory : MonoBehaviour
{
    [SerializeField]
    private bool verboseLog = true;

    private ContentDatabaseProvider _contentDatabaseProvider;
    private SessionManager _sessionManager;
    private RandomManager _randomManager;
    private EquipmentFactory _equipmentFactory; // 적 프리뷰 생성 시 무기를 붙여야 되니까
    private BalanceSO _balance;
    private bool _initialized;

    public BalanceSO Balance => _balance;

    public void Initialize(
        ContentDatabaseProvider contentDatabaseProvider,
        SessionManager sessionManager,
        RandomManager randomManager,
        EquipmentFactory equipmentFactory
    )
    {
        if (_initialized)
        {
            return;
        }

        _contentDatabaseProvider = contentDatabaseProvider;
        _sessionManager = sessionManager;
        _randomManager = randomManager;
        _equipmentFactory = equipmentFactory;
        _balance = _contentDatabaseProvider != null ? _contentDatabaseProvider.Balance : null;

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError("[RecruitFactory] contentDatabaseProvider is null.", this);
            return;
        }

        if (_sessionManager == null)
        {
            Debug.LogError("[RecruitFactory] sessionManager is null.", this);
            return;
        }

        if (_randomManager == null)
        {
            Debug.LogError("[RecruitFactory] randomManager is null.", this);
            return;
        }

        if (_equipmentFactory == null)
        {
            Debug.LogError("[RecruitFactory] equipmentFactory is null.", this);
            return;
        }

        if (_balance == null)
        {
            Debug.LogError("[RecruitFactory] BalanceSO is null.", this);
            return;
        }

        _initialized = true;

        if (verboseLog)
        {
            int templateCount =
                _contentDatabaseProvider.GladiatorClasses != null ? _contentDatabaseProvider.GladiatorClasses.Count : 0;

            Debug.Log(
                $"[RecruitFactory] Initialized. "
                    + $"GladiatorTemplateCount={templateCount}, "
                    + $"TraitCount={_contentDatabaseProvider.Traits.Count}, "
                    + $"PersonalityCount={_contentDatabaseProvider.Personalities.Count},"
                    + $"EquipmentFactoryReady={(_equipmentFactory != null)}",
                this
            );
        }
    }

    // 날짜를 기준으로 해서 검투사 프리뷰를 만들고,
    // 시장 슬롯에 올릴 offer 객체로 감싼다
    public MarketGladiatorOffer CreateMarketGladiatorOffer(int currentDay, int slotIndex)
    {
        if (!_initialized)
        {
            Debug.LogError("[RecruitFactory] CreateMarketGladiatorOffer called before Initialize.", this);
            return null;
        }

        OwnedGladiatorData preview = CreatePreviewGladiatorForDay(
            currentDay,
            RandomStreamType.Recruit,
            useSessionNameCounter: true
        );

        if (preview == null)
        {
            Debug.LogError("[RecruitFactory] Failed to create market gladiator preview.", this);
            return null;
        }

        int price = Mathf.Max(0, preview.Level * _balance.gladiatorBuyPricePerLevel);
        MarketGladiatorOffer offer = new MarketGladiatorOffer(slotIndex, preview, price);

        if (verboseLog)
        {
            Debug.Log(
                $"[RecruitFactory] Market gladiator created. "
                    + $"Slot={slotIndex}, Name={preview.DisplayName}, "
                    + $"Level={preview.Level}, Personal={preview.Personality} Loyalty={preview.Loyalty}, Price={price}",
                this
            );
        }

        return offer;
    }

    // 하루치 전투 후보 전체를 생성한다. 각각 기준 레벨의 -40%, -10%, +0%, +10%
    // 실제 하나의 난이도 당 적 팀 하나는 CreateBattleEncounterPreviewForDifficulty에서 구성함.
    // 이렇게 난이도별 적 팀 preview를 만들고, BattleManager가 이를 캐시해 사용함.
    public List<BattleEncounterPreview> CreateBattleEncounterPreviewsForDay(
        int currentDay,
        int encounterCount = 4,
        int unitsPerEncounter = 6
    )
    {
        List<BattleEncounterPreview> encounters = new List<BattleEncounterPreview>(encounterCount);

        if (!_initialized)
        {
            Debug.LogError("[RecruitFactory] CreateBattleEncounterPreviewsForDay called before Initialize.", this);
            return encounters;
        }

        int safeDay = Mathf.Max(1, currentDay);
        unitsPerEncounter = Mathf.Max(1, unitsPerEncounter);

        BattleEncounterDifficulty[] orderedDifficulties =
        {
            BattleEncounterDifficulty.VeryLow,
            BattleEncounterDifficulty.Low,
            BattleEncounterDifficulty.Medium,
            BattleEncounterDifficulty.High,
        };

        int buildCount = Mathf.Min(Mathf.Max(1, encounterCount), orderedDifficulties.Length);

        for (int encounterIndex = 0; encounterIndex < buildCount; encounterIndex++)
        {
            BattleEncounterDifficulty difficulty = orderedDifficulties[encounterIndex];

            BattleEncounterPreview encounter = CreateBattleEncounterPreviewForDifficulty(
                safeDay,
                encounterIndex,
                difficulty,
                unitsPerEncounter
            );

            if (encounter != null)
            {
                encounters.Add(encounter);
            }
        }

        if (verboseLog)
        {
            for (int i = 0; i < encounters.Count; i++)
            {
                BattleEncounterPreview encounter = encounters[i];
                Debug.Log(
                    $"[RecruitFactory] Battle encounter cached. "
                        + $"Index={encounter.EncounterIndex}, Difficulty={encounter.Difficulty}, "
                        + $"AvgLv={encounter.AverageLevel:0.0}, RewardPreview={encounter.PreviewRewardGold}",
                    this
                );
            }
        }

        return encounters;
    }

    // 특정 난이도의 적 팀 1개를 실제로 구성함.
    // 적 유닛 레벨 분배, 적 검투사 preview 생성, 랜덤 무기 장착, snapshot 변환까지 담당.
    private BattleEncounterPreview CreateBattleEncounterPreviewForDifficulty(
        int currentDay,
        int encounterIndex,
        BattleEncounterDifficulty difficulty,
        int unitsPerEncounter
    )
    {
        List<BattleUnitSnapshot> units = new List<BattleUnitSnapshot>(unitsPerEncounter);
        List<int> unitLevels = BuildEncounterUnitLevels(currentDay, difficulty, unitsPerEncounter);
        float totalLevel = 0f;

        for (int unitIndex = 0; unitIndex < unitLevels.Count; unitIndex++)
        {
            OwnedGladiatorData preview = CreatePreviewGladiatorAtLevel(
                unitLevels[unitIndex],
                RandomStreamType.BattleEncounter,
                useSessionNameCounter: false
            );

            if (preview == null)
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to create battle encounter preview. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            if (!TryEquipRandomWeaponForBattlePreview(preview, currentDay))
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to equip random weapon on battle preview. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            preview.DisplayName = BuildBattleEnemyDisplayName(encounterIndex, unitIndex);

            BattleUnitSnapshot snapshot = BattleUnitSnapshot.FromOwnedGladiator(preview, true);
            if (snapshot == null)
            {
                Debug.LogError(
                    $"[RecruitFactory] Failed to convert battle preview to snapshot. "
                        + $"EncounterIndex={encounterIndex}, Difficulty={difficulty}, UnitIndex={unitIndex}",
                    this
                );
                return null;
            }

            units.Add(snapshot);
            totalLevel += snapshot.Level;
        }

        float averageLevel = units.Count > 0 ? totalLevel / units.Count : 0f;
        int previewRewardGold = CalculatePreviewRewardForDifficulty(currentDay, difficulty);

        return new BattleEncounterPreview(encounterIndex, units, averageLevel, previewRewardGold, difficulty);
    }

    // 난이도와 날짜에 맞춰 적 팀 전체 레벨 총량을 정하고,
    // 그 총량을 각 적 유닛에게 분배한다.
    // 리팩토링 대상: 현재는 총량을 정하고 분배하는 방식이므로 레벨이 정수로 딱 떨어진다.
    // 추후 약간의 노이즈를 위해 개별 분포를 적용할 수 있음.
    private List<int> BuildEncounterUnitLevels(
        int currentDay,
        BattleEncounterDifficulty difficulty,
        int unitsPerEncounter
    )
    {
        List<int> levels = new List<int>(unitsPerEncounter);

        if (unitsPerEncounter <= 0)
        {
            return levels;
        }

        float targetAverageLevel = Mathf.Max(1f, currentDay * GetAverageLevelMultiplierForDifficulty(difficulty));
        int targetTotalLevel = Mathf.Max(unitsPerEncounter, Mathf.RoundToInt(targetAverageLevel * unitsPerEncounter));

        int baseLevel = Mathf.Max(1, targetTotalLevel / unitsPerEncounter);
        int remainder = Mathf.Max(0, targetTotalLevel - (baseLevel * unitsPerEncounter));

        for (int i = 0; i < unitsPerEncounter; i++)
        {
            levels.Add(baseLevel);
        }

        int startIndex =
            _randomManager != null ? _randomManager.NextInt(RandomStreamType.BattleEncounter, 0, unitsPerEncounter) : 0;

        for (int i = 0; i < remainder; i++)
        {
            int index = (startIndex + i) % unitsPerEncounter;
            levels[index]++;
        }

        return levels;
    }

    private float GetAverageLevelMultiplierForDifficulty(BattleEncounterDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BattleEncounterDifficulty.VeryLow:
                return 0.6f;

            case BattleEncounterDifficulty.Low:
                return 0.9f;

            case BattleEncounterDifficulty.Medium:
                return 1.0f;

            case BattleEncounterDifficulty.High:
                return 1.1f;

            default:
                return 1.0f;
        }
    }

    private OwnedGladiatorData CreatePreviewGladiatorAtLevel(
        int fixedLevel,
        RandomStreamType streamType,
        bool useSessionNameCounter
    )
    {
        GladiatorClassSO gladiatorTemplate = _contentDatabaseProvider.GladiatorTemplate;
        TraitSO trait = PickRandomNonNull(_contentDatabaseProvider.Traits, streamType);
        PersonalitySO personality = PickRandomNonNull(_contentDatabaseProvider.Personalities, streamType);

        if (gladiatorTemplate == null)
        {
            Debug.LogError("[RecruitFactory] Exactly one valid GladiatorClassSO is required.", this);
            return null;
        }

        if (trait == null)
        {
            Debug.LogError("[RecruitFactory] Failed because TraitSO is null.", this);
            return null;
        }

        if (personality == null)
        {
            Debug.LogError("[RecruitFactory] Failed because PersonalitySO is null.", this);
            return null;
        }

        int level = Mathf.Max(1, fixedLevel);
        int loyalty = RollLoyaltyFromPersonality(personality, streamType);
        int upkeep = Mathf.Max(0, _balance.upkeepPerLevel * level);

        string displayName;
        if (useSessionNameCounter && _sessionManager != null)
        {
            displayName = _sessionManager.ConsumeNextClassName(gladiatorTemplate.className);
        }
        else
        {
            displayName = "Enemy";
        }

        OwnedGladiatorData preview = new OwnedGladiatorData(
            0,
            displayName,
            level,
            0,
            loyalty,
            upkeep,
            gladiatorTemplate,
            trait,
            personality,
            null,
            null
        );

        preview.FinalHealthVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        preview.FinalAttackVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        RefreshDerivedStats(preview, true);
        return preview;
    }

    // 날짜 기반 레벨의 검투사 preview를 생성함.
    // 시장애 진열되는 검투사와 일부 적 preview 생성의 시작점
    private OwnedGladiatorData CreatePreviewGladiatorForDay(
        int currentDay,
        RandomStreamType streamType,
        bool useSessionNameCounter
    )
    {
        GladiatorClassSO gladiatorTemplate = _contentDatabaseProvider.GladiatorTemplate;
        TraitSO trait = PickRandomNonNull(_contentDatabaseProvider.Traits, streamType);
        PersonalitySO personality = PickRandomNonNull(_contentDatabaseProvider.Personalities, streamType);

        if (gladiatorTemplate == null)
        {
            Debug.LogError("[RecruitFactory] Exactly one valid GladiatorClassSO is required.", this);
            return null;
        }

        if (trait == null)
        {
            Debug.LogError("[RecruitFactory] Failed because TraitSO is null.", this);
            return null;
        }

        if (personality == null)
        {
            Debug.LogError("[RecruitFactory] Failed because PersonalitySO is null.", this);
            return null;
        }

        int level = CalculateDayBasedLevel(currentDay, streamType);
        int loyalty = RollLoyaltyFromPersonality(personality, streamType);
        int upkeep = Mathf.Max(0, _balance.upkeepPerLevel * level);

        string displayName;
        if (useSessionNameCounter && _sessionManager != null)
        {
            displayName = _sessionManager.ConsumeNextClassName(gladiatorTemplate.className);
        }
        else
        {
            displayName = "Enemy";
        }

        OwnedGladiatorData preview = new OwnedGladiatorData(
            0,
            displayName,
            level,
            0,
            loyalty,
            upkeep,
            gladiatorTemplate,
            trait,
            personality,
            null,
            null
        );

        preview.FinalHealthVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        preview.FinalAttackVariancePercent = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorFinalStatVarianceMinPercent,
            _balance.gladiatorFinalStatVarianceMaxPercent
        );

        RefreshDerivedStats(preview, true);
        return preview;
    }

    private int CalculateDayBasedLevel(int currentDay, RandomStreamType streamType)
    {
        int safeDay = Mathf.Max(1, currentDay);

        float levelVariance = _randomManager.NextFloatRange(
            streamType,
            _balance.gladiatorLevelVarianceMinPercent,
            _balance.gladiatorLevelVarianceMaxPercent
        );

        int level = Mathf.FloorToInt(safeDay * (1f + levelVariance));
        return Mathf.Max(1, level);
    }

    private int CalculatePreviewRewardForDifficulty(int currentDay, BattleEncounterDifficulty difficulty)
    {
        int baseReward = Mathf.Max(0, Mathf.Max(1, currentDay) * _balance.battleVictoryRewardPerDay);

        float multiplier = difficulty switch
        {
            BattleEncounterDifficulty.VeryLow => 0.4f,
            BattleEncounterDifficulty.Low => 0.9f,
            BattleEncounterDifficulty.Medium => 1.0f,
            BattleEncounterDifficulty.High => 1.1f,
            _ => 1.0f,
        };

        return Mathf.Max(0, Mathf.RoundToInt(baseReward * multiplier));
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

    // 검투사 클래스 기본치 + 레벨 성장 + 최종 분산 + 장착 무기 보너스를 반영해
    // 전투용 캐시용 스탯을 계산함.
    // 프리뷰 단계에서도 이 함수로 실제 전투 진입 전 능력치가 확정됨.
    private void RefreshDerivedStats(OwnedGladiatorData gladiator, bool fullyHeal)
    {
        if (gladiator == null)
        {
            Debug.LogError("[RecruitFactory] RefreshDerivedStats received null gladiator.", this);
            return;
        }

        if (gladiator.GladiatorClass == null)
        {
            Debug.LogError("[RecruitFactory] RefreshDerivedStats failed because GladiatorClass is null.", this);
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
                gladiator.CurrentHealth = Mathf.Clamp(
                    oldCurrentHealth + gainedMaxHealth,
                    0f,
                    gladiator.CachedMaxHealth
                );
            }
            else
            {
                gladiator.CurrentHealth = Mathf.Clamp(oldCurrentHealth, 0f, gladiator.CachedMaxHealth);
            }
        }
    }

    private T PickRandomNonNull<T>(IReadOnlyList<T> list, RandomStreamType streamType)
        where T : class
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

    // 적 검투사 preview에 랜덤 무기 preview를 장착하고,
    // 그 무기 보너스를 포함해 최종 스탯을 다시 계산해야함
    private bool TryEquipRandomWeaponForBattlePreview(OwnedGladiatorData preview, int currentDay)
    {
        if (preview == null)
        {
            return false;
        }

        if (_equipmentFactory == null)
        {
            Debug.LogError(
                "[RecruitFactory] TryEquipRandomWeaponForBattlePreview failed because equipmentFactory is null.",
                this
            );
            return false;
        }

        OwnedWeaponData weaponPreview = _equipmentFactory.CreateRandomWeaponPreviewForDay(currentDay);
        if (weaponPreview == null)
        {
            Debug.LogError("[RecruitFactory] Failed to create random weapon preview for battle enemy.", this);
            return false;
        }

        preview.EquippedWeapon = weaponPreview;
        RefreshDerivedStats(preview, true);
        return true;
    }

    private static string BuildBattleEnemyDisplayName(int encounterIndex, int unitIndex)
    {
        return $"Enemy E{encounterIndex + 1}-{unitIndex + 1}";
    }
}
