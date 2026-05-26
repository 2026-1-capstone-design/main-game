public sealed class OwnedArtifactData
{
    public int RuntimeId { get; }
    public ArtifactSO Artifact { get; }
    public string DisplayName { get; }
    public ArtifactId ArtifactPerkId => Artifact != null ? Artifact.ArtifactPerkId : ArtifactId.None;

    public OwnedArtifactData(int runtimeId, ArtifactSO artifact)
    {
        RuntimeId = runtimeId;
        Artifact = artifact;
        DisplayName =
            artifact != null && !string.IsNullOrWhiteSpace(artifact.artifactName) ? artifact.artifactName : "Artifact";
    }
}
