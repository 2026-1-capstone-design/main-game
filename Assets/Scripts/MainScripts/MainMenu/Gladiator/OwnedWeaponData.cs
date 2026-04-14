/*
OwnedGladiatorData 와 같은 형식
*/

using UnityEngine;

public sealed class OwnedWeaponData
{
    public int RuntimeId { get; }

    public string DisplayName { get; set; }
    public int Level { get; set; }

    public WeaponSO Weapon { get; }
    public WeaponSkillSO WeaponSkill { get; set; }

    public float CachedAttackBonus { get; set; }
    public float CachedHealthBonus { get; set; }
    public float CachedAttackSpeedBonus { get; set; }
    public float CachedMoveSpeedBonus { get; set; }
    public float CachedAttackRangeBonus { get; set; }

    public float FinalAttackBonusVariancePercent { get; set; }
    public float FinalHealthBonusVariancePercent { get; set; }

    public OwnedWeaponData(
        int runtimeId,
        string displayName,
        int level,
        WeaponSO weapon)
    {
        RuntimeId = runtimeId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Weapon" : displayName;
        Level = Mathf.Max(1, level);
        Weapon = weapon;
        WeaponSkill = null;

        CachedAttackBonus = 0f;
        CachedHealthBonus = 0f;
        CachedAttackSpeedBonus = 0f;
        CachedMoveSpeedBonus = 0f;
        CachedAttackRangeBonus = 0f;

        FinalAttackBonusVariancePercent = 0f;
        FinalHealthBonusVariancePercent = 0f;
    }
}
