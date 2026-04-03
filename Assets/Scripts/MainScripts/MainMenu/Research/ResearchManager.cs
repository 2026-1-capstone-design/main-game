using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResearchManager : MonoBehaviour
{
    [SerializeField] private bool verboseLog = true;

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

        IReadOnlyList<PerkSO> perks = contentDatabaseProvider.Perks;
        for (int i = 0; i < perks.Count; i++)
        {
            PerkSO perk = perks[i];
            if (perk != null)
            {
                _unlockedPerks.Add(perk);
            }
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log($"[ResearchManager] Initialized. UnlockedPerkCount={_unlockedPerks.Count}", this);
        }
    }

    public int GetUnlockedPerkCount()
    {
        return _unlockedPerks.Count;
    }
}