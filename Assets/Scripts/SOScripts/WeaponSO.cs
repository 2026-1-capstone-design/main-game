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
    bow,
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

    // 인스펙터에서 값이 바뀔 때마다 실행되는 함수
    private void OnValidate()
    {
        UpdatePrice();
    }

    private void UpdatePrice()
    {
        // 1. 검투사 기본 스탯
        float defaultAtk = 20f;
        float defaultAs = 1f;
        float defaultHp = 1000f;

        float HPprice = 1;
        float ATKprice = 70;
        float Moveprice = 700;
        float Rangeprice = 700;

        // 2. 비-전투 스탯 가격 (단순 합산)
        float hpPrice = baseHealthBonus * HPprice;           // 체력 1당 1원
        float movePrice = baseMoveSpeedBonus * Moveprice;    // 0.1당 70원 -> 1당 700원
        float rangePrice = baseAttackRangeBonus * Rangeprice; // 0.1당 70원 -> 1당 700원

        // 3. 전투 스탯 가격 (DPS 방식 적용)
        // (기존 공격력 1당 50원 기준, 기본 공격력 20일 때 총 가치는 1000원)
        float baseDps = defaultAtk * defaultAs;
        float newDps = (defaultAtk + baseAttackBonus) * (defaultAs + baseAttackSpeedBonus);

        // DPS가 몇 % 상승했는지 계산하여 가격에 반영
        // %에다가 기존 공격력을 곱해서, 상승 비율을 DPS에 대한 상승을 단순히 공격력 증가로 가정 가능
        // 증가 가격에다가 기본 체력에 대한 가격을 뺌
        // 즉, (증가 DPS) * (공속1)번 때리는 것과 동일
        float dpsIncreaseRatio = newDps / baseDps;
        float combatPrice = (defaultAtk * ATKprice) * dpsIncreaseRatio - (defaultAtk * ATKprice);

        // 4. 최종 가격 합산
        float finalPrice = hpPrice + movePrice + rangePrice + combatPrice;

        calculatedPrice = Mathf.RoundToInt(finalPrice);
    }
}
