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
    Parrying = 18,
    ContinuousSlash = 18,
    HighHeal = 20,
    OminousStar = 21,
    DarkShroud = 22,
    Consecration = 23,
    HuntStart = 24,
    WarCommander = 25,
    Submersion = 26,
    LeapOfFaith = 27,
    HookThrow = 28,
    ManaCollapse = 29,
    HolyBarrier = 30,
    Fear = 31,
    UnstoppableForce = 32,
    Juggernaut = 33,
    Harvest = 34,
    HolyRevive = 35,
    DeathWhirlpool = 36,
    Retribution = 37,
    Surge = 38,

    MagicExplosion = 39,

    Curse = 40,
    GlyphOfCounterattack = 41,

    NobleSacrifice = 42,
    Duel = 43,
    Freeze = 44,
    MindControl = 45,
    Oblivion = 46,
    Respite = 47,
    MoonlightDance = 48,
    FanaticalObsession = 49,
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
