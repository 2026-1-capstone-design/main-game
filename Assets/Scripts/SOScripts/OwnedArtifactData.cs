public sealed class OwnedArtifactData
{
    public ArtifactSO Artifact { get; }
    public ArtifactId ArtifactId => Artifact != null ? Artifact.artifactId : ArtifactId.None;

    public OwnedArtifactData(ArtifactSO artifact)
    {
        Artifact = artifact;
    }
}
