using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Perk")]
public sealed class PerkSO : ScriptableObject
{
    public Sprite icon;
    public string perkName;
    [TextArea] public string description;
}