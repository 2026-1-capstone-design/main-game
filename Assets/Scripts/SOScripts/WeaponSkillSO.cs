using UnityEngine;

public enum WeaponSkillId
{
    None = 0,
    SwipeAttack = 1,
    Madness = 2,
    LongGrip = 3,
    Taunt = 4,
    HeartAttack = 5,
    Warcry = 6,
    ThroatSlit = 7,
    RustyBlade = 8,
    Fireball = 9,
    Lightning = 10,
    Stimpack = 11,
    RevolverFanning = 12,
    BayonetCharge = 13,
    ShieldBash = 14,
    HeadStrike = 15,
    SpiralSlash = 16,
}

public enum skillType
{
    None = 0,
    attack = 1,
    tank = 2,
    support = 3,
    enhance = 4,
}

[CreateAssetMenu(menuName = "Prototype/Content/Weapon Skill")]
public sealed class WeaponSkillSO : ScriptableObject
{
    public Sprite icon;
    public string skillName = "Weapon Skill";

    [TextArea]
    public string description;

    public WeaponType weaponType = WeaponType.oneHand;
    public WeaponSkillId skillId = WeaponSkillId.None;
}
