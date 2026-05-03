using System;

[Serializable]
public sealed class SaveSlotData
{
    public int slotIndex;
    public int day;
    public int gold;
    public int sessionSeed;
    public string savedAtUtc;

    public bool hasUsedBattleToday;
    public int pendingBattleRewardAmount;

    public SaveClassCounterEntry[] classCounters;
    public SaveOwnedWeaponData[] ownedWeapons;
    public SaveOwnedGladiatorData[] ownedGladiators;

    public string[] unlockedPerkNames;

    public int marketInitializedDay;
    public SaveMarketWeaponOfferData[] marketWeaponOffers;
    public SaveMarketGladiatorOfferData[] marketGladiatorOffers;

    public int battleEncounterGeneratedDay;
    public int selectedEncounterIndex;
    public SaveBattleEncounterData[] battleEncounters;
}

[Serializable]
public sealed class SaveClassCounterEntry
{
    public string classPrefix;
    public int currentNumber;
}

[Serializable]
public sealed class SaveOwnedWeaponData
{
    public int runtimeId;
    public string displayName;
    public int level;

    public string weaponName;
    public int weaponType;
    public int weaponSkillId;

    public float cachedAttackBonus;
    public float cachedHealthBonus;
    public float cachedAttackSpeedBonus;
    public float cachedMoveSpeedBonus;
    public float cachedAttackRangeBonus;

    public float finalAttackBonusVariancePercent;
    public float finalHealthBonusVariancePercent;
}

[Serializable]
public sealed class SaveOwnedGladiatorData
{
    public int runtimeId;
    public string displayName;
    public int level;
    public int exp;
    public int loyalty;
    public int upkeep;

    public string gladiatorClassName;
    public string traitName;
    public string personalityName;
    public string equippedPerkName;
    public int equippedWeaponRuntimeId;

    public float cachedMaxHealth;
    public float currentHealth;
    public float cachedAttack;
    public float cachedAttackSpeed;
    public float cachedMoveSpeed;
    public float cachedAttackRange;

    public float finalHealthVariancePercent;
    public float finalAttackVariancePercent;

    public int[] customizeIndicates;
}

[Serializable]
public sealed class SaveMarketWeaponOfferData
{
    public int slotIndex;
    public int price;
    public bool isSold;
    public SaveOwnedWeaponData weapon;
}

[Serializable]
public sealed class SaveMarketGladiatorOfferData
{
    public int slotIndex;
    public int price;
    public bool isSold;
    public SaveOwnedGladiatorData gladiator;
}

[Serializable]
public sealed class SaveBattleEncounterUnitData
{
    public int sourceRuntimeId;
    public string displayName;
    public int level;
    public int loyalty;

    public float maxHealth;
    public float currentHealth;
    public float attack;
    public float attackSpeed;
    public float moveSpeed;
    public float attackRange;

    public string gladiatorClassName;
    public string traitName;
    public string personalityName;
    public string equippedPerkName;

    public int weaponType;
    public int weaponSkillId;
    public int[] customizeIndicates;

    public bool isRanged;
    public bool useProjectile;
}

[Serializable]
public sealed class SaveBattleEncounterData
{
    public int encounterIndex;
    public int difficulty;
    public float averageLevel;
    public int previewRewardGold;
    public SaveBattleEncounterUnitData[] enemyUnits;
}
