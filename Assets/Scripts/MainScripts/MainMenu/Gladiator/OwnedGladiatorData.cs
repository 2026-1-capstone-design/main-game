using UnityEngine;

public sealed class OwnedGladiatorData
{
    public int RuntimeId { get; }

    public string DisplayName { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Loyalty { get; set; }
    public int Upkeep { get; set; }

    public GladiatorClassSO GladiatorClass { get; }
    public TraitSO Trait { get; }
    public PersonalitySO Personality { get; }

    public PerkSO EquippedPerk { get; set; }
    public OwnedWeaponData EquippedWeapon { get; set; }

    public float CachedMaxHealth { get; set; }
    public float CurrentHealth { get; set; }
    public float CachedAttack { get; set; }
    public float CachedAttackSpeed { get; set; }
    public float CachedMoveSpeed { get; set; }
    public float CachedAttackRange { get; set; }

    public float FinalHealthVariancePercent { get; set; }
    public float FinalAttackVariancePercent { get; set; }

    public OwnedGladiatorData(
        int runtimeId,
        string displayName,
        int level,
        int exp,
        int loyalty,
        int upkeep,
        GladiatorClassSO gladiatorClass,
        TraitSO trait,
        PersonalitySO personality,
        PerkSO equippedPerk,
        OwnedWeaponData equippedWeapon)
    {
        RuntimeId = runtimeId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Gladiator" : displayName;
        Level = Mathf.Max(1, level);
        Exp = Mathf.Max(0, exp);
        Loyalty = Mathf.Max(0, loyalty);
        Upkeep = Mathf.Max(0, upkeep);

        GladiatorClass = gladiatorClass;
        Trait = trait;
        Personality = personality;

        EquippedPerk = equippedPerk;
        EquippedWeapon = equippedWeapon;

        CachedMaxHealth = 0f;
        CurrentHealth = 0f;
        CachedAttack = 0f;
        CachedAttackSpeed = 0f;
        CachedMoveSpeed = 0f;
        CachedAttackRange = 0f;

        FinalHealthVariancePercent = 0f;
        FinalAttackVariancePercent = 0f;
    }
}
