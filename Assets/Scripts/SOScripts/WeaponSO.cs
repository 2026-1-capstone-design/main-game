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
    bow,
}

public enum WeaponGrade
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Unique = 3,
    Legend = 4,
}

[CreateAssetMenu(menuName = "Prototype/Content/Weapon")]
public sealed class WeaponSO : ScriptableObject
{
    private const float DefaultAttack = 20f;
    private const float DefaultAttackSpeed = 1f;
    private const float HealthPrice = 1f;
    private const float AttackPrice = 50f;
    private const float MoveSpeedPrice = 1000f;
    private const float AttackRangePrice = 1000f;

    public Sprite icon;
    public string weaponName = "Sword";
    public WeaponType weaponType = WeaponType.oneHand;

    [TextArea]
    public string lore;

    public GameObject leftWeaponPrefab;
    public GameObject rightWeaponPrefab;

    public bool isRanged = false;
    public bool useProjectile = false;

    [Header("AttackAnimationTiming")]
    public bool defaultDur = false;
    public float duration = 1f;

    public float baseAttackBonus = 5f;
    public float baseHealthBonus = 0f;
    public float baseAttackSpeedBonus = 0f;
    public float baseMoveSpeedBonus = 0f;
    public float baseAttackRangeBonus = 0f;

    [Header("Auto Calculated Value")]
    [Tooltip("인스펙터 수정 시 자동으로 계산되는 무기 가치입니다.")]
    public int calculatedPrice;
    public WeaponGrade weaponGrade;

    // 인스펙터에서 값이 바뀔 때마다 실행되는 함수
    private void OnValidate()
    {
        UpdatePrice();
    }

    private void UpdatePrice()
    {
        calculatedPrice = CalculatePrice(
            baseHealthBonus,
            baseAttackBonus,
            baseAttackSpeedBonus,
            baseMoveSpeedBonus,
            baseAttackRangeBonus
        );
        weaponGrade = newGrade(calculatedPrice);
    }

    public static int CalculatePrice(
        float addedHealth,
        float addedAttack,
        float addedAttackSpeed,
        float addedMoveSpeed,
        float addedAttackRange
    )
    {
        // 체력 1 = 1원, 공격력 1 = 50원, 공속 0.1 = 100원,
        // 이속/사거리 0.1 = 70원 기준.
        // 공격력과 공격속도는 DPS 비율을 기준 공격력 가치로 환산해 가격화한다.
        float hpPrice = Mathf.Max(0f, addedHealth) * HealthPrice;
        float movePrice = Mathf.Max(0f, addedMoveSpeed) * MoveSpeedPrice;
        float rangePrice = Mathf.Max(0f, addedAttackRange) * AttackRangePrice;

        float baseDps = DefaultAttack * DefaultAttackSpeed;
        float newDps = (DefaultAttack + Mathf.Max(0f, addedAttack)) * (1f + Mathf.Max(0f, addedAttackSpeed));
        float combatPrice = (newDps / baseDps) * DefaultAttack * AttackPrice;

        float finalPrice = hpPrice + movePrice + rangePrice + combatPrice;
        return Mathf.RoundToInt(finalPrice);
    }

    public WeaponGrade newGrade(int price) =>
        price switch
        {
            < 5000 => WeaponGrade.Common,
            < 6000 => WeaponGrade.Uncommon,
            < 7000 => WeaponGrade.Rare,
            < 8000 => WeaponGrade.Unique,
            _ => WeaponGrade.Legend, // '_'는 그 외의 모든 경우(>= 8000)를 의미합니다.
        };
}
