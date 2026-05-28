using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Prototype/Content/Artifact")]
public sealed class ArtifactSO : ScriptableObject
{
    public Sprite icon;
    public string artifactName;

    [FormerlySerializedAs("artifactId")]
    public ArtifactId artifactPerkId;

    public int artifactLevel = 1;

    [TextArea]
    [FormerlySerializedAs("description")]
    public string artifactLore;

    public ArtifactId ArtifactPerkId => artifactPerkId;
}
