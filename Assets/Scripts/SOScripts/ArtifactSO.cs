using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Artifact")]
public sealed class ArtifactSO : ScriptableObject
{
    public Sprite icon;
    public string artifactName;
    public ArtifactId artifactId;

    [TextArea]
    public string description;
}
