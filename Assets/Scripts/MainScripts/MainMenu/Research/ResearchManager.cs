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
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[ResearchManager] Initialized.", this);
        }
    }

    public int GetUnlockedArtifactCount()
    {
        return _unlockedArtifacts.Count;
    }

    public bool AddArtifact(ArtifactSO artifact)
    {
        if (artifact == null)
        {
            return false;
        }

        if (_unlockedArtifacts.Contains(artifact))
        {
            return false;
        }

        _unlockedArtifacts.Add(artifact);

        if (verboseLog)
        {
            Debug.Log($"[ResearchManager] Artifact added. Name={artifact.artifactName}", this);
        }

        return true;
    }

    public bool RemoveArtifact(ArtifactSO artifact)
    {
        if (artifact == null)
        {
            return false;
        }

        bool removed = _unlockedArtifacts.Remove(artifact);

        if (removed && verboseLog)
        {
            Debug.Log($"[ResearchManager] Artifact removed. Name={artifact.artifactName}", this);
        }

        return removed;
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
