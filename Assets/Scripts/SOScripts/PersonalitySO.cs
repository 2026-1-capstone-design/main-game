using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Personality")]
public sealed class PersonalitySO : ScriptableObject
{
    public Sprite icon;
    public string personalityName;

    [TextArea]
    public string description;
    public int baseLoyalty = 70;
}
