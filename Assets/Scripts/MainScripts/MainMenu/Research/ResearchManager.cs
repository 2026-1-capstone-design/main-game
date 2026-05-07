using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResearchManager : MonoBehaviour
{
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<ArtifactSO> _unlockedArtifacts = new List<ArtifactSO>();
    private bool _initialized;

    public IReadOnlyList<ArtifactSO> UnlockedArtifacts => _unlockedArtifacts;

    public void Initialize(ContentDatabaseProvider contentDatabaseProvider)
    {
        if (_initialized)
        {
            return;
        }

        if (contentDatabaseProvider == null)
        {
            Debug.LogError("[ResearchManager] contentDatabaseProvider is null.", this);
            return;
        }

        _unlockedArtifacts.Clear();

        IReadOnlyList<ArtifactSO> artifacts = contentDatabaseProvider.Artifacts;
        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactSO artifact = artifacts[i];
            if (artifact != null)
            {
                _unlockedArtifacts.Add(artifact);
            }
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log($"[ResearchManager] Initialized. UnlockedArtifactCount={_unlockedArtifacts.Count}", this);
        }
    }

    public int GetUnlockedArtifactCount()
    {
        return _unlockedArtifacts.Count;
    }

    public void RestoreUnlockedArtifactsForLoad(List<ArtifactSO> unlockedArtifacts)
    {
        _unlockedArtifacts.Clear();

        if (unlockedArtifacts != null)
        {
            for (int i = 0; i < unlockedArtifacts.Count; i++)
            {
                ArtifactSO artifact = unlockedArtifacts[i];
                if (artifact != null)
                {
                    _unlockedArtifacts.Add(artifact);
                }
            }
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[ResearchManager] Unlocked artifacts restored from save. Count={_unlockedArtifacts.Count}",
                this
            );
        }
    }
}
