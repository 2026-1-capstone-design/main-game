using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Balance")]
public sealed class BalanceSO : ScriptableObject
{
    [Header("Resources")]
    public int initialGold = 1000;
    public int battleVictoryRewardPerDay = 100;

    [Header("Prices")]
    public int gladiatorBuyPricePerLevel = 50;
    public int weaponBuyPricePerLevel = 50;
    public int gladiatorSellPricePerLevel = 40;
    public int weaponSellPricePerLevel = 40;
    public int artifactBuyPrice = 100;
    public int artifactSellPrice = 80;

    [Header("Battle Rewards")]
    public int veryLowRewardBase = 5000;
    public int veryLowRewardPerLevel = 100;
    public int lowRewardBase = 6000;
    public int lowRewardPerLevel = 150;
    public int mediumRewardBase = 7500;
    public int mediumRewardPerLevel = 200;
    public int highRewardBase = 10000;
    public int highRewardPerLevel = 250;

    [Header("Unit Economy")]
    public int gladiatorBaseMarketPrice = 2000;
    public int gladiatorMarketPricePerLevelMin = 40;
    public int gladiatorMarketPricePerLevelMax = 60;
    public int gladiatorBaseUpkeep = 2000;
    public int gladiatorUpkeepPerLevel = 100;

    [Header("Equipment Grade Economy")]
    public int commonWeaponBasePrice = 4000;
    public int commonWeaponPricePerLevel = 100;
    public int uncommonWeaponBasePrice = 5000;
    public int uncommonWeaponPricePerLevel = 200;
    public int rareWeaponBasePrice = 6000;
    public int rareWeaponPricePerLevel = 300;
    public int uniqueWeaponBasePrice = 7000;
    public int uniqueWeaponPricePerLevel = 400;
    public int legendWeaponBasePrice = 8000;
    public int legendWeaponPricePerLevel = 500;

    [Header("Dummy Upkeep")]
    public int upkeepPerLevel = 10;

    [Header("EOD XP")]
    [Range(0f, 1f)]
    public float eodXpGainChance = 0.5f;
    public int eodXpGainAmount = 500;

    [Header("Level Curve")]
    public int xpPerLevelMultiplier = 100;

    [Header("Ranges")]
    public int loyaltyMin = 50;
    public int loyaltyMax = 100;

    public float gladiatorLevelVarianceMinPercent = -0.20f;
    public float gladiatorLevelVarianceMaxPercent = 0.05f;

    public float weaponLevelVarianceMinPercent = -0.05f;
    public float weaponLevelVarianceMaxPercent = 0.05f;

    public float weaponFinalStatVarianceMinPercent = -0.15f;
    public float weaponFinalStatVarianceMaxPercent = 0.15f;

    public float gladiatorFinalStatVarianceMinPercent = -0.15f;
    public float gladiatorFinalStatVarianceMaxPercent = 0.15f;

    [Header("Equipment Fixed Growth Multipliers")]
    public float equipmentAttackBonusPerLevel = 0.1f;
    public float equipmentHealthBonusPerLevel = 0.1f;
    public float equipmentAttackSpeedBonusPerLevel = 0.1f;
    public float equipmentMoveSpeedBonusPerLevel = 0.1f;

    [Header("Market Slots")]
    public int marketGladiatorSlots = 4;
    public int marketWeaponSlots = 4;
    public int marketArtifactSlots = 4;
}
