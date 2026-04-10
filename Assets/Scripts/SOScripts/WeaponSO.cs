using Unity.VisualScripting;
using UnityEngine;

public enum WeaponType
{
    None = 0,
    oneHand,
    twoHand,
    dualHand,
    spear,
    shield,
    dagger,
    handGun,
    dualGun,
    rifle,
    staff,
    bow
}

public enum WeaponSkillId
{
    None = 0,
    SwipeAttack = 1,
    Madness = 2,
    LongGrip = 3,
    Taunt = 4,
    HeartAttack = 5
}

public enum skillType
{
    None = 0,
    attack = 1,
    tank = 2,
    support = 3,
    enhance = 4
}

[CreateAssetMenu(menuName = "Prototype/Content/Weapon")]
public sealed class WeaponSO : ScriptableObject
{
    public Sprite icon;
    public string weaponName = "Sword";
    public WeaponType weaponType = WeaponType.oneHand;

    public GameObject leftWeaponPrefab;
    public GameObject rightWeaponPrefab;

    public bool isRanged = false;
    public bool useProjectile = false;

    public float baseAttackBonus = 5f;
    public float baseHealthBonus = 0f;
    public float baseAttackSpeedBonus = 0f;
    public float baseMoveSpeedBonus = 0f;
    public float baseAttackRangeBonus = 0f;
}
