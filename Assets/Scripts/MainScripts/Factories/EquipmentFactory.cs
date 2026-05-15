using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EquipmentFactory : MonoBehaviour
{
    [SerializeField]
    private bool verboseLog = true;

    private const float PricePerAtk = 50f;
    private const float PricePerHP = 1f;
    private const float PricePerRange = 700f;
    private const float PricePerMoveSpd = 700f;

    private ContentDatabaseProvider _contentDatabaseProvider;
    private RandomManager _randomManager;
    private BalanceSO _balance;
    private bool _initialized;

    public BalanceSO Balance => _balance;

    public void Initialize(ContentDatabaseProvider contentDatabaseProvider, RandomManager randomManager)
    {
        if (_initialized)
        {
            return;
        }

        _contentDatabaseProvider = contentDatabaseProvider;
        _randomManager = randomManager;
        _balance = _contentDatabaseProvider != null ? _contentDatabaseProvider.Balance : null;

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError("[EquipmentFactory] contentDatabaseProvider is null.", this);
            return;
        }

        if (_randomManager == null)
        {
            Debug.LogError("[EquipmentFactory] randomManager is null.", this);
            return;
        }

        if (_balance == null)
        {
            Debug.LogError("[EquipmentFactory] BalanceSO is null.", this);
            return;
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[EquipmentFactory] Initialized. WeaponCount={_contentDatabaseProvider.Weapons.Count}, WeaponSkillCount={_contentDatabaseProvider.WeaponSkills.Count}",
                this
            );
        }
    }

    // 현재 날짜 기준 무기 preview를 만든다. 이는 recruit factory도 비슷하게 동작함.
    // 시장 슬롯에 올릴 판매용 offer 객체로 감싼다
    public MarketWeaponOffer CreateMarketWeaponOffer(int currentDay, int slotIndex)
    {
        if (!_initialized)
            return null;

        // 1. 무기 및 스킬 선정
        WeaponSO weapon = PickRandomNonNull(_contentDatabaseProvider.Weapons);
        WeaponSkillSO weaponSkill = PickRandomMatchingWeaponSkill(weapon.weaponType);

        // 2. 🌟 가격(예산) 먼저 결정 (등급가 + 레벨가)
        int dayValue = Mathf.Max(1, currentDay);
        int baseBudget =
            GetWeaponGradeBasePrice(weapon.weaponGrade) + (GetWeaponGradePricePerLevel(weapon.weaponGrade) * dayValue);

        // 3. 🌟 비슷한 가격 유지를 위한 소폭의 예산 편차 (±5%)
        float budgetVariance = _randomManager.NextFloatRange(RandomStreamType.Equipment, 0.95f, 1.05f);
        int finalPrice = Mathf.RoundToInt(baseBudget * budgetVariance);

        // 4. 무기 데이터 생성 및 예산 기반 스탯 분배
        OwnedWeaponData preview = new OwnedWeaponData(0, weapon.weaponName, dayValue, weapon);
        preview.WeaponSkill = weaponSkill;

        // 🌟 [추가] 예산에 맞춰 스탯 비율 할당
        RefreshStatsByBudget(preview, finalPrice);

        return new MarketWeaponOffer(slotIndex, preview, finalPrice);
    }

    private void RefreshStatsByBudget(OwnedWeaponData ownedWeapon, int budget)
    {
        WeaponSO so = ownedWeapon.Weapon;

        // A. 기본 스탯들의 골드 가치 합산
        float baseValue =
            (so.baseAttackBonus * PricePerAtk)
            + (so.baseHealthBonus * PricePerHP)
            + (so.baseAttackRangeBonus * PricePerRange);

        // B. 🌟 보너스 배분 비율 (성장률) 계산
        // (전체 예산 - 기본 가치) / 기본 가치 = 추가 성장비율
        float bonusBudget = Mathf.Max(0, budget - baseValue);
        float growthRatio = (baseValue > 0) ? (bonusBudget / baseValue) : 0;

        // C. 🌟 기본 스탯에 비례하여 할당 (GrowthRatio 적용)
        ownedWeapon.CachedAttackBonus = so.baseAttackBonus * (1f + growthRatio);
        ownedWeapon.CachedHealthBonus = so.baseHealthBonus * (1f + growthRatio);
        ownedWeapon.CachedAttackRangeBonus = so.baseAttackRangeBonus * (1f + growthRatio);

        // 고정 스탯
        ownedWeapon.CachedAttackSpeedBonus = so.baseAttackSpeedBonus;
        ownedWeapon.CachedMoveSpeedBonus = so.baseMoveSpeedBonus;
    }

    public OwnedWeaponData CreateRandomWeaponPreviewForDay(int currentDay)
    {
        if (!_initialized)
        {
            Debug.LogError("[EquipmentFactory] CreateRandomWeaponPreviewForDay called before Initialize.", this);
            return null;
        }

        WeaponSO weapon = PickRandomNonNull(_contentDatabaseProvider.Weapons);
        if (weapon == null)
        {
            Debug.LogError("[EquipmentFactory] Failed to create random weapon preview because WeaponSO is null.", this);
            return null;
        }

        int dayValue = CalculateMarketLevel(currentDay);
        WeaponSkillSO weaponSkill = PickRandomMatchingWeaponSkill(weapon.weaponType);

        // 🌟 스탯 개별 편차가 아닌, 전체 가격 예산에 대한 편차(0.95 ~ 1.05)를 구합니다.
        float budgetVariance = _randomManager.NextFloatRange(RandomStreamType.Equipment, 0.95f, 1.05f);

        return BuildWeaponPreview(
            weapon,
            weaponSkill,
            dayValue,
            budgetVariance // 🌟 2개의 variance 대신 1개만 전달
        );
    }

    // 랜덤이 아니라 지정된 무기 타입/스킬/레벨로 무기 preview를 만든다.
    // 치트코드용.
    public OwnedWeaponData CreateWeaponPreviewFromSpec(WeaponType weaponType, WeaponSkillId weaponSkillId, int level)
    {
        if (!_initialized)
        {
            Debug.LogError("[EquipmentFactory] CreateWeaponPreviewFromSpec called before Initialize.", this);
            return null;
        }

        if (weaponType == WeaponType.None)
        {
            Debug.LogError("[EquipmentFactory] CreateWeaponPreviewFromSpec failed because weaponType is None.", this);
            return null;
        }

        WeaponSO weapon = FindFirstWeaponByType(weaponType);
        if (weapon == null)
        {
            Debug.LogError($"[EquipmentFactory] No WeaponSO found for WeaponType={weaponType}.", this);
            return null;
        }

        WeaponSkillSO weaponSkill = null;
        if (weaponSkillId != WeaponSkillId.None)
        {
            weaponSkill = FindWeaponSkillById(weaponSkillId);
            if (weaponSkill == null)
            {
                Debug.LogError($"[EquipmentFactory] No WeaponSkillSO found for WeaponSkillId={weaponSkillId}.", this);
                return null;
            }

            if (weaponSkill.weaponType != weaponType)
            {
                Debug.LogError(
                    $"[EquipmentFactory] Weapon type / skill mismatch. "
                        + $"WeaponType={weaponType}, SkillId={weaponSkillId}, SkillWeaponType={weaponSkill.weaponType}",
                    this
                );
                return null;
            }
        }

        return BuildWeaponPreview(weapon, weaponSkill, Mathf.Max(1, level), 1f);
    }

    private int CalculateMarketLevel(int currentDay)
    {
        return Mathf.Max(1, currentDay);
    }

    public int CalculateWeaponPrice(OwnedWeaponData weapon)
    {
        return CalculateWeaponPrice(weapon, weapon != null ? weapon.Level : 0);
    }

    public int CalculateWeaponPrice(OwnedWeaponData weapon, int currentDay)
    {
        if (weapon == null || weapon.Weapon == null)
            return 0;

        WeaponSO so = weapon.Weapon;
        float baseDps = Mathf.Max(0.01f, so.baseAttackBonus * so.baseAttackSpeedBonus);
        float currentDps = weapon.CachedAttackBonus * weapon.CachedAttackSpeedBonus;

        float offensiveValue = 0f;
        if (baseDps > 0.001f)
        {
            offensiveValue = (currentDps / baseDps) * so.baseAttackBonus * 50f;
        }

        float otherValue =
            (weapon.CachedHealthBonus * 1f)
            + (weapon.CachedMoveSpeedBonus * 700f)
            + (weapon.CachedAttackRangeBonus * 700f);

        return Mathf.RoundToInt(offensiveValue + otherValue);
    }

    // OwnedWeaponData preview를 실제로 조립하는 함수
    // 무기 본체, 스킬, 레벨, 최종 분산값을 묶고 캐시 스탯까지 계산함.
    private OwnedWeaponData BuildWeaponPreview(
        WeaponSO weapon,
        WeaponSkillSO weaponSkill,
        int level,
        float budgetVariance // 🌟 수정됨
    )
    {
        if (weapon == null)
        {
            return null;
        }

        OwnedWeaponData preview = new OwnedWeaponData(0, weapon.weaponName, Mathf.Max(1, level), weapon);
        preview.WeaponSkill = weaponSkill;

        // 예산 편차값을 RefreshDerivedStats로 넘겨줍니다.
        RefreshDerivedStats(preview, level, budgetVariance);
        return preview;
    }

    private WeaponSO FindFirstWeaponByType(WeaponType weaponType)
    {
        IReadOnlyList<WeaponSO> allWeapons = _contentDatabaseProvider.Weapons;
        if (allWeapons == null || allWeapons.Count == 0)
        {
            return null;
        }

        WeaponSO firstMatch = null;
        int matchCount = 0;

        for (int i = 0; i < allWeapons.Count; i++)
        {
            WeaponSO weapon = allWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            if (weapon.weaponType != weaponType)
            {
                continue;
            }

            matchCount++;

            if (firstMatch == null)
            {
                firstMatch = weapon;
            }
        }

        if (matchCount > 1 && verboseLog)
        {
            Debug.LogWarning(
                $"[EquipmentFactory] Multiple WeaponSO assets found for WeaponType={weaponType}. "
                    + $"CreateWeaponPreviewFromSpec will use the first match: {firstMatch.weaponName}",
                this
            );
        }

        return firstMatch;
    }

    private WeaponSkillSO FindWeaponSkillById(WeaponSkillId weaponSkillId)
    {
        IReadOnlyList<WeaponSkillSO> allSkills = _contentDatabaseProvider.WeaponSkills;
        if (allSkills == null || allSkills.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < allSkills.Count; i++)
        {
            WeaponSkillSO skill = allSkills[i];
            if (skill == null)
            {
                continue;
            }

            if (skill.skillId == weaponSkillId)
            {
                return skill;
            }
        }

        return null;
    }

    // 무기 레벨과 최종 분산값을 반영해
    // 공격/체력/공속/이속/사거리 보너스 캐시를 계산함.
    // 이후 검투사 쪽 최종 스탯 계산에서 그대로 더해짐
    private void RefreshDerivedStats(OwnedWeaponData ownedWeapon)
    {
        RefreshDerivedStats(ownedWeapon, ownedWeapon != null ? ownedWeapon.Level : 0, 1f);
    }

    private void RefreshDerivedStats(OwnedWeaponData ownedWeapon, int currentDay, float budgetVariance)
    {
        if (ownedWeapon == null || ownedWeapon.Weapon == null)
            return;

        WeaponSO so = ownedWeapon.Weapon;
        int dayValue = Mathf.Max(1, currentDay);

        int gradeBasePrice = GetWeaponGradeBasePrice(so.weaponGrade);
        int dayPrice = GetWeaponGradePricePerLevel(so.weaponGrade) * dayValue;
        float totalBudget = (gradeBasePrice + dayPrice) * Mathf.Max(0f, budgetVariance);

        float baseValue =
            (so.baseAttackBonus * PricePerAtk)
            + (so.baseHealthBonus * PricePerHP)
            + (so.baseAttackRangeBonus * PricePerRange)
            + (so.baseMoveSpeedBonus * PricePerMoveSpd);

        float bonusBudget = Mathf.Max(0f, totalBudget - baseValue);
        float growthRatio = (baseValue > 0f) ? (bonusBudget / baseValue) : 0f;

        ownedWeapon.CachedAttackBonus = so.baseAttackBonus * (1f + growthRatio);
        ownedWeapon.CachedAttackSpeedBonus = so.baseAttackSpeedBonus * (1f + growthRatio);
        ownedWeapon.CachedHealthBonus = so.baseHealthBonus * (1f + growthRatio);
        ownedWeapon.CachedMoveSpeedBonus = so.baseMoveSpeedBonus * (1f + growthRatio);
        ownedWeapon.CachedAttackRangeBonus = so.baseAttackRangeBonus * (1f + growthRatio);
    }

    private float CalculateWeaponGrowthRatio(WeaponSO weapon, int currentDay)
    {
        if (weapon == null || currentDay <= 0)
        {
            return 0f;
        }

        int baseValue = Mathf.Max(
            1,
            WeaponSO.CalculatePrice(
                weapon.baseHealthBonus,
                weapon.baseAttackBonus,
                weapon.baseAttackSpeedBonus,
                weapon.baseMoveSpeedBonus,
                weapon.baseAttackRangeBonus
            )
        );

        int pricePerLevel = GetWeaponGradePricePerLevel(weapon.weaponGrade);
        return Mathf.Max(0f, (pricePerLevel * currentDay) / (float)baseValue);
    }

    private int GetWeaponGradeBasePrice(WeaponGrade grade)
    {
        if (_balance == null)
        {
            return 0;
        }

        return grade switch
        {
            WeaponGrade.Common => Mathf.Max(0, _balance.commonWeaponBasePrice),
            WeaponGrade.Uncommon => Mathf.Max(0, _balance.uncommonWeaponBasePrice),
            WeaponGrade.Rare => Mathf.Max(0, _balance.rareWeaponBasePrice),
            WeaponGrade.Unique => Mathf.Max(0, _balance.uniqueWeaponBasePrice),
            WeaponGrade.Legend => Mathf.Max(0, _balance.legendWeaponBasePrice),
            _ => Mathf.Max(0, _balance.commonWeaponBasePrice),
        };
    }

    private int GetWeaponGradePricePerLevel(WeaponGrade grade)
    {
        if (_balance == null)
        {
            return 0;
        }

        return grade switch
        {
            WeaponGrade.Common => Mathf.Max(0, _balance.commonWeaponPricePerLevel),
            WeaponGrade.Uncommon => Mathf.Max(0, _balance.uncommonWeaponPricePerLevel),
            WeaponGrade.Rare => Mathf.Max(0, _balance.rareWeaponPricePerLevel),
            WeaponGrade.Unique => Mathf.Max(0, _balance.uniqueWeaponPricePerLevel),
            WeaponGrade.Legend => Mathf.Max(0, _balance.legendWeaponPricePerLevel),
            _ => Mathf.Max(0, _balance.commonWeaponPricePerLevel),
        };
    }

    private WeaponSkillSO PickRandomMatchingWeaponSkill(WeaponType weaponType)
    {
        IReadOnlyList<WeaponSkillSO> allSkills = _contentDatabaseProvider.WeaponSkills;
        if (allSkills == null || allSkills.Count == 0)
        {
            return null;
        }

        List<WeaponSkillSO> candidates = new List<WeaponSkillSO>(allSkills.Count);
        for (int i = 0; i < allSkills.Count; i++)
        {
            WeaponSkillSO skill = allSkills[i];
            if (skill == null)
            {
                continue;
            }

            if (skill.weaponType == weaponType)
            {
                candidates.Add(skill);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int pickedIndex = _randomManager.NextInt(RandomStreamType.Equipment, 0, candidates.Count);
        return candidates[pickedIndex];
    }

    private T PickRandomNonNull<T>(IReadOnlyList<T> list)
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

        int targetIndex = _randomManager.NextInt(RandomStreamType.Equipment, 0, validCount);

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
