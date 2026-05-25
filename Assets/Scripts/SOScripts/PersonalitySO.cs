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
    [Range(0, 2)]
    public int speechStyle = 0;

    [Header("SOT Command Obedience")]
    public int[] obedienceRates =
    {
        90, 80, 80, 80, 90, 80, 95, 90
    };

    public int[] fallbackWeights =
    {
        30, 20, 30, 20, 100, 30, 5, 0
    };
}
