using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Personality")]
public sealed class PersonalitySO : ScriptableObject
{
    public Sprite icon;
    public string personalityName;

    [TextArea]
    public string description;
    public int baseLoyalty = 70;

    [Header("SOT Dialog")]
    [TextArea(2, 4)]
    public string dialogPersonalityDescription =
        "침착하고 기복이 없는 성격이다. 무리한 돌격보다 확실한 교전을 선호한다.";
    [Range(0, 2)]
    public int speechStyle = 0;

    [Header("SOT Command Obedience")]
    public int[] obedienceRates = { 90, 80, 80, 80, 90, 80, 95, 90 };

    public int[] fallbackWeights = { 30, 20, 30, 20, 100, 30, 5, 0 };

    // 그냥 참고용: 순응율 총합. 대체로 650 이상 유지가 바람직해보이고, 0~4(공격과 이동 4개)는 대체로 높게 잡는것이 바람직해보임
    // 산만함/반항적과 같은 "명백하게 명령을 잘 듣지 않는 성격"의 경우에는 총합 floor를 500까지 내려가게 둬도 될 듯합니다
    public int obedienceRateSum = 685;

    private void OnValidate()
    {
        obedienceRateSum = CalculateObedienceRateSum(obedienceRates);
    }

    private static int CalculateObedienceRateSum(int[] rates)
    {
        if (rates == null)
            return 0;

        int sum = 0;

        for (int i = 0; i < rates.Length; i++)
            sum += rates[i];

        return sum;
    }
}
