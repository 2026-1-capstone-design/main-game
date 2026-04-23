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
}
