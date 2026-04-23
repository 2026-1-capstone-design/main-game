using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Synergy")]
public sealed class SynergySO : ScriptableObject
{
    public Sprite icon;
    public string synergyName;

    [TextArea]
    public string description;
}
