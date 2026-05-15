using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EquipmentFactory : MonoBehaviour
{
    [SerializeField]
    private bool verboseLog = true;

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
        {
            Debug.LogError("[EquipmentFactory] CreateMarketWeaponOffer called before Initialize.", this);
            return null;
        }

        OwnedWeaponData preview = CreateRandomWeaponPreviewForDay(currentDay);
        if (preview == null)
        {
            Debug.LogError("[EquipmentFactory] Failed to create market weapon preview.", this);
            return null;
        }

        int price = CalculateWeaponPrice(preview, currentDay);
        MarketWeaponOffer offer = new MarketWeaponOffer(slotIndex, preview, price);

        if (verboseLog)
        {
            string skillName = preview.WeaponSkill != null ? preview.WeaponSkill.skillName : "(None)";
            WeaponType weaponType = preview.Weapon != null ? preview.Weapon.weaponType : WeaponType.None;

            Debug.Log(
                $"[EquipmentFactory] Market weapon created. "
                    + $"Slot={slotIndex}, Name={preview.DisplayName}, Type={weaponType}, Level={preview.Level}, Skill={skillName}, Price={price}",
                this
            );
        }

        return offer;
    }

    // 날짜 기반 레벨, 무기 종류, 무기 스킬, 최종 공격/체력 분산값을 포함한
    // '시장/적 장비용 무기 프리뷰'를 생성함.
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

        float finalAttackVariancePercent = _randomManager.NextFloatRange(
            RandomStreamType.Equipment,
            _balance.weaponFinalStatVarianceMinPercent,
            _balance.weaponFinalStatVarianceMaxPercent
        );

        float finalHealthVariancePercent = _randomManager.NextFloatRange(
            RandomStreamType.Equipment,
            _balance.weaponFinalStatVarianceMinPercent,
            _balance.weaponFinalStatVarianceMaxPercent
        );

        return BuildWeaponPreview(
            weapon,
            weaponSkill,
            dayValue,
            finalAttackVariancePercent,
            finalHealthVariancePercent
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

        return BuildWeaponPreview(weapon, weaponSkill, Mathf.Max(1, level), 0f, 0f);
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
        if (weapon == null)
        {
            return 0;
        }

        if (weapon.Weapon == null || _balance == null)
        {
            return 0;
        }

        int dayValue = Mathf.Max(1, currentDay);
        int gradeBasePrice = GetWeaponGradeBasePrice(weapon.Weapon.weaponGrade);
        int dayPrice = GetWeaponGradePricePerLevel(weapon.Weapon.weaponGrade) * dayValue;

        return Mathf.Max(0, gradeBasePrice + dayPrice);
    }

    // OwnedWeaponData preview를 실제로 조립하는 함수
    // 무기 본체, 스킬, 레벨, 최종 분산값을 묶고 캐시 스탯까지 계산함.
    private OwnedWeaponData BuildWeaponPreview(
        WeaponSO weapon,
        WeaponSkillSO weaponSkill,
        int level,
        float finalAttackVariancePercent,
        float finalHealthVariancePercent
    )
    {
        if (weapon == null)
        {
            return null;
        }

        OwnedWeaponData preview = new OwnedWeaponData(0, weapon.weaponName, Mathf.Max(1, level), weapon);

        preview.WeaponSkill = weaponSkill;
        preview.FinalAttackBonusVariancePercent = finalAttackVariancePercent;
        preview.FinalHealthBonusVariancePercent = finalHealthVariancePercent;

        RefreshDerivedStats(preview, level);
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
        RefreshDerivedStats(ownedWeapon, ownedWeapon != null ? ownedWeapon.Level : 0);
    }

    private void RefreshDerivedStats(OwnedWeaponData ownedWeapon, int currentDay)
    {
        if (ownedWeapon == null || ownedWeapon.Weapon == null)
        {
            Debug.LogError("[EquipmentFactory] RefreshDerivedStats failed because weapon data is invalid.", this);
            return;
        }

        int dayValue = Mathf.Max(1, currentDay);

        float baseAttackBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackBonus);
        float baseHealthBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseHealthBonus);
        float baseAttackSpeedBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackSpeedBonus);
        float baseMoveSpeedBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseMoveSpeedBonus);
        float baseAttackRangeBonus = Mathf.Max(0f, ownedWeapon.Weapon.baseAttackRangeBonus);

        float growthRatio = CalculateWeaponGrowthRatio(ownedWeapon.Weapon, dayValue);

        float scaledAttackBonus = baseAttackBonus * (1f + growthRatio);
        float scaledHealthBonus = baseHealthBonus * (1f + growthRatio);
        float scaledAttackSpeedBonus = baseAttackSpeedBonus * (1f + growthRatio);
        float scaledMoveSpeedBonus = baseMoveSpeedBonus;
        float scaledAttackRangeBonus = baseAttackRangeBonus * (1f + growthRatio);

        float finalAttackMultiplier = 1f + ownedWeapon.FinalAttackBonusVariancePercent;
        float finalHealthMultiplier = 1f + ownedWeapon.FinalHealthBonusVariancePercent;

        if (finalAttackMultiplier < 0f)
        {
            finalAttackMultiplier = 0f;
        }

        if (finalHealthMultiplier < 0f)
        {
            finalHealthMultiplier = 0f;
        }

        ownedWeapon.CachedAttackBonus = scaledAttackBonus * finalAttackMultiplier;
        ownedWeapon.CachedHealthBonus = scaledHealthBonus * finalHealthMultiplier;
        ownedWeapon.CachedAttackSpeedBonus = scaledAttackSpeedBonus;
        ownedWeapon.CachedMoveSpeedBonus = scaledMoveSpeedBonus;
        ownedWeapon.CachedAttackRangeBonus = scaledAttackRangeBonus;
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
