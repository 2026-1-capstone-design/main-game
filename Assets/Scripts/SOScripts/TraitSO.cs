using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Trait")]
public sealed class TraitSO : ScriptableObject
{
    public Sprite icon;
    public string traitName;
    [TextArea] public string description;
}
