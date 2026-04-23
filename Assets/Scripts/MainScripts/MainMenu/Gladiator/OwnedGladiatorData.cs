using UnityEngine;

public sealed class OwnedGladiatorData
{
    public int RuntimeId { get; } // 실제 보유 중인 검투사를 식별하는 런타임 ID

    public string DisplayName { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Loyalty { get; set; }
    public int Upkeep { get; set; }

    public GladiatorClassSO GladiatorClass { get; }
    public TraitSO Trait { get; }
    public PersonalitySO Personality { get; }

    public PerkSO EquippedPerk { get; set; }
    public OwnedWeaponData EquippedWeapon { get; set; } // 현재 이 검투사가 장착 중인 실제 owned 무기 !!참조!!.

    // 클래스, 레벨, 개체 분산, 장비 보너스를 반영한 실제 전투용 캐시 스탯들.
    // !!UI 표시와 전투 snapshot 생성 시 이 값을 그대로 사용함!!
    public float CachedMaxHealth { get; set; }
    public float CurrentHealth { get; set; }
    public float CachedAttack { get; set; }
    public float CachedAttackSpeed { get; set; }
    public float CachedMoveSpeed { get; set; }
    public float CachedAttackRange { get; set; }

    public float FinalHealthVariancePercent { get; set; }
    public float FinalAttackVariancePercent { get; set; }

    // 실제 보유 검투사 1명의 기본 정보를 담는 런타임 데이터 생성자
    // 초기엔 캐시 스탯이 비어 있고, 이후 별도 RefreshDerivedStats로 완성이되는 것
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
        OwnedWeaponData equippedWeapon
    )
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
