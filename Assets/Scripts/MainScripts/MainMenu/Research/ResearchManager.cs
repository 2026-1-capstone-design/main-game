using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResearchManager : MonoBehaviour
{
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<PerkSO> _unlockedPerks = new List<PerkSO>();
    private bool _initialized;

    public IReadOnlyList<PerkSO> UnlockedPerks => _unlockedPerks;

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

        _unlockedPerks.Clear();
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[ResearchManager] Initialized.", this);
        }
    }

    public int GetUnlockedPerkCount()
    {
        return _unlockedPerks.Count;
    }

    public bool AddArtifact(PerkSO artifact)
    {
        if (artifact == null)
        {
            return false;
        }

        if (_unlockedPerks.Contains(artifact))
        {
            return false;
        }

        _unlockedPerks.Add(artifact);

        if (verboseLog)
        {
            Debug.Log($"[ResearchManager] Artifact added. Name={artifact.perkName}", this);
        }

        return true;
    }

    public bool RemoveArtifact(PerkSO artifact)
    {
        if (artifact == null)
        {
            return false;
        }

        bool removed = _unlockedPerks.Remove(artifact);

        if (removed && verboseLog)
        {
            Debug.Log($"[ResearchManager] Artifact removed. Name={artifact.perkName}", this);
        }

        return removed;
    }

    public void RestoreUnlockedPerksForLoad(List<PerkSO> unlockedPerks)
    {
        _unlockedPerks.Clear();

        if (unlockedPerks != null)
        {
            for (int i = 0; i < unlockedPerks.Count; i++)
            {
                PerkSO perk = unlockedPerks[i];
                if (perk != null)
                {
                    _unlockedPerks.Add(perk);
                }
            }
        }

        if (verboseLog)
        {
            Debug.Log($"[ResearchManager] Unlocked perks restored from save. Count={_unlockedPerks.Count}", this);
        }
    }
}
