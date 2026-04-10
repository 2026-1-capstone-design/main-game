using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ContentDatabaseProvider : SingletonBehaviour<ContentDatabaseProvider>
{
    [SerializeField] private ContentDatabaseSO contentDatabase;

    public ContentDatabaseSO Database => contentDatabase;
    public BalanceSO Balance => contentDatabase != null ? contentDatabase.balance : null;

    public GladiatorClassSO GladiatorTemplate =>
        contentDatabase != null &&
        contentDatabase.gladiatorClasses != null &&
        contentDatabase.gladiatorClasses.Count == 1
            ? contentDatabase.gladiatorClasses[0]
            : null;

    public IReadOnlyList<GladiatorClassSO> GladiatorClasses =>
        contentDatabase != null && contentDatabase.gladiatorClasses != null
            ? contentDatabase.gladiatorClasses
            : Array.Empty<GladiatorClassSO>();

    public IReadOnlyList<WeaponSO> Weapons =>
        contentDatabase != null && contentDatabase.weapons != null
            ? contentDatabase.weapons
            : Array.Empty<WeaponSO>();

    public IReadOnlyList<WeaponSkillSO> WeaponSkills =>
    contentDatabase != null && contentDatabase.weaponSkills != null
        ? contentDatabase.weaponSkills
        : Array.Empty<WeaponSkillSO>();

    public IReadOnlyList<TraitSO> Traits =>
        contentDatabase != null && contentDatabase.traits != null
            ? contentDatabase.traits
            : Array.Empty<TraitSO>();

    public IReadOnlyList<SynergySO> Synergies =>
        contentDatabase != null && contentDatabase.synergies != null
            ? contentDatabase.synergies
            : Array.Empty<SynergySO>();

    public IReadOnlyList<PerkSO> Perks =>
        contentDatabase != null && contentDatabase.perks != null
            ? contentDatabase.perks
            : Array.Empty<PerkSO>();

    public IReadOnlyList<PersonalitySO> Personalities =>
        contentDatabase != null && contentDatabase.personalities != null
            ? contentDatabase.personalities
            : Array.Empty<PersonalitySO>();

    public void Initialize()
    {
        if (contentDatabase == null)
        {
            Debug.LogError("[ContentDatabaseProvider] ContentDatabaseSO is not assigned.", this);
            return;
        }

        if (contentDatabase.balance == null)
        {
            Debug.LogWarning("[ContentDatabaseProvider] BalanceSO is not assigned inside ContentDatabaseSO.", this);
        }

        if (contentDatabase.gladiatorClasses == null ||
            contentDatabase.gladiatorClasses.Count != 1 ||
            contentDatabase.gladiatorClasses[0] == null)
        {
            Debug.LogError("[ContentDatabaseProvider] Exactly one valid GladiatorClassSO must be assigned in ContentDatabaseSO.", this);
        }
    }
}
