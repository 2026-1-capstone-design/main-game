using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleEncounterDifficulty
{
    VeryLow = -1,
    Low = 0,
    Medium = 1,
    High = 2
}

[Serializable]
public sealed class BattleUnitSnapshot
{
    public int SourceRuntimeId { get; }
    public bool IsEnemy { get; }
    public string DisplayName { get; }
    public int Level { get; }
    public int Loyalty { get; }
    public float MaxHealth { get; }
    public float CurrentHealth { get; }
    public float Attack { get; }
    public float AttackSpeed { get; }
    public float MoveSpeed { get; }
    public float AttackRange { get; }

    public GladiatorClassSO GladiatorClass { get; }
    public TraitSO Trait { get; }
    public PersonalitySO Personality { get; }
    public PerkSO EquippedPerk { get; }
    public WeaponType WeaponType { get; }

    // 무기 왼쪽 오른쪽 추가
    public GameObject LeftWeaponPrefab { get; }
    public GameObject RightWeaponPrefab { get; }

    public WeaponSkillId WeaponSkillId { get; }
    public bool IsRanged { get; }
    public bool UseProjectile { get; }
    public Sprite PortraitSprite { get; }

    public BattleUnitSnapshot(
        int sourceRuntimeId,
        bool isEnemy,
        string displayName,
        int level,
        int loyalty,
        float maxHealth,
        float currentHealth,
        float attack,
        float attackSpeed,
        float moveSpeed,
        float attackRange,
        GladiatorClassSO gladiatorClass,
        TraitSO trait,
        PersonalitySO personality,
        PerkSO equippedPerk,
        WeaponType weaponType,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab,
        WeaponSkillId weaponSkillId,
        bool isRanged,
        bool useProjectile,
        Sprite portraitSprite)
    {
        SourceRuntimeId = sourceRuntimeId;
        IsEnemy = isEnemy;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Gladiator" : displayName;
        Level = Mathf.Max(1, level);
        Loyalty = Mathf.Max(0, loyalty);
        MaxHealth = Mathf.Max(0f, maxHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        Attack = Mathf.Max(0f, attack);
        AttackSpeed = Mathf.Max(0f, attackSpeed);
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        AttackRange = Mathf.Max(0f, attackRange);

        GladiatorClass = gladiatorClass;
        Trait = trait;
        Personality = personality;
        EquippedPerk = equippedPerk;
        WeaponType = weaponType;

        // 생성자에 추가
        LeftWeaponPrefab = leftWeaponPrefab;
        RightWeaponPrefab = rightWeaponPrefab;


        WeaponSkillId = weaponSkillId;
        IsRanged = isRanged;
        UseProjectile = useProjectile;
        PortraitSprite = portraitSprite;
    }

    public BattleUnitSnapshot Clone()
    {
        return new BattleUnitSnapshot(
            SourceRuntimeId,
            IsEnemy,
            DisplayName,
            Level,
            Loyalty,
            MaxHealth,
            CurrentHealth,
            Attack,
            AttackSpeed,
            MoveSpeed,
            AttackRange,
            GladiatorClass,
            Trait,
            Personality,
            EquippedPerk,
            WeaponType,
            LeftWeaponPrefab,
            RightWeaponPrefab,
            WeaponSkillId,
            IsRanged,
            UseProjectile,
            PortraitSprite
        );
    }

    public static BattleUnitSnapshot FromOwnedGladiator(
        OwnedGladiatorData source,
        bool isEnemy,
        Sprite portraitSprite = null)
    {
        if (source == null)
        {
            return null;
        }

        Sprite resolvedPortrait = portraitSprite;

        if (resolvedPortrait == null && source.GladiatorClass != null)
        {
            resolvedPortrait = source.GladiatorClass.icon;
        }
        WeaponType weaponType = WeaponType.None;

        GameObject leftPrefab = null;
        GameObject rightPrefab = null;


        WeaponSkillId weaponSkillId = WeaponSkillId.None;
        bool isRanged = false;
        bool useProjectile = false;

        if (source.EquippedWeapon != null)
        {
            if (source.EquippedWeapon.Weapon != null)
            {
                weaponType = source.EquippedWeapon.Weapon.weaponType;
                // 무기 추가
                leftPrefab = source.EquippedWeapon.Weapon.leftWeaponPrefab;
                rightPrefab = source.EquippedWeapon.Weapon.rightWeaponPrefab;

                isRanged = source.EquippedWeapon.Weapon.isRanged;
                useProjectile = source.EquippedWeapon.Weapon.useProjectile;
            }

            if (source.EquippedWeapon.WeaponSkill != null)
            {
                weaponSkillId = source.EquippedWeapon.WeaponSkill.skillId;
            }
        }

        return new BattleUnitSnapshot(
            source.RuntimeId,
            isEnemy,
            source.DisplayName,
            source.Level,
            source.Loyalty,
            source.CachedMaxHealth,
            source.CurrentHealth,
            source.CachedAttack,
            source.CachedAttackSpeed,
            source.CachedMoveSpeed,
            source.CachedAttackRange,
            source.GladiatorClass,
            source.Trait,
            source.Personality,
            source.EquippedPerk,
            weaponType,
            leftPrefab,
            rightPrefab,
            weaponSkillId,
            isRanged,
            useProjectile,
            resolvedPortrait
        );
    }
}

[Serializable]
public sealed class BattleEncounterPreview
{
    private readonly List<BattleUnitSnapshot> _enemyUnits = new List<BattleUnitSnapshot>();

    public int EncounterIndex { get; set; }
    public BattleEncounterDifficulty Difficulty { get; set; }
    public float AverageLevel { get; }
    public int PreviewRewardGold { get; set; }
    public IReadOnlyList<BattleUnitSnapshot> EnemyUnits => _enemyUnits;

    public BattleEncounterPreview(
        int encounterIndex,
        IEnumerable<BattleUnitSnapshot> enemyUnits,
        float averageLevel,
        int previewRewardGold,
        BattleEncounterDifficulty difficulty)
    {
        EncounterIndex = Mathf.Max(0, encounterIndex);
        AverageLevel = Mathf.Max(0f, averageLevel);
        PreviewRewardGold = Mathf.Max(0, previewRewardGold);
        Difficulty = difficulty;

        if (enemyUnits != null)
        {
            foreach (BattleUnitSnapshot unit in enemyUnits)
            {
                if (unit != null)
                {
                    _enemyUnits.Add(unit);
                }
            }
        }
    }
}

[Serializable]
public sealed class BattleStartPayload
{
    private readonly List<BattleUnitSnapshot> _allyUnits = new List<BattleUnitSnapshot>();
    private readonly List<BattleUnitSnapshot> _enemyUnits = new List<BattleUnitSnapshot>();

    public IReadOnlyList<BattleUnitSnapshot> AllyUnits => _allyUnits;
    public IReadOnlyList<BattleUnitSnapshot> EnemyUnits => _enemyUnits;

    public int SelectedEncounterIndex { get; }
    public float EnemyAverageLevel { get; }
    public int PreviewRewardGold { get; }
    public int BattleSeed { get; }

    public BattleStartPayload(
        IEnumerable<BattleUnitSnapshot> allyUnits,
        IEnumerable<BattleUnitSnapshot> enemyUnits,
        int selectedEncounterIndex,
        float enemyAverageLevel,
        int previewRewardGold,
        int battleSeed)
    {
        if (allyUnits != null)
        {
            foreach (BattleUnitSnapshot unit in allyUnits)
            {
                if (unit != null)
                {
                    _allyUnits.Add(unit);
                }
            }
        }

        if (enemyUnits != null)
        {
            foreach (BattleUnitSnapshot unit in enemyUnits)
            {
                if (unit != null)
                {
                    _enemyUnits.Add(unit);
                }
            }
        }

        SelectedEncounterIndex = Mathf.Max(0, selectedEncounterIndex);
        EnemyAverageLevel = Mathf.Max(0f, enemyAverageLevel);
        PreviewRewardGold = Mathf.Max(0, previewRewardGold);
        BattleSeed = battleSeed;
    }
}
