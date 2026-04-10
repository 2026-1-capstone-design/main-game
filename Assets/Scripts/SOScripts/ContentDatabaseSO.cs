using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Content Database")]
public sealed class ContentDatabaseSO : ScriptableObject
{
    public List<GladiatorClassSO> gladiatorClasses = new();
    public List<WeaponSO> weapons = new();
    public List<WeaponSkillSO> weaponSkills = new();
    public List<TraitSO> traits = new();
    public List<SynergySO> synergies = new();
    public List<PerkSO> perks = new();
    public List<PersonalitySO> personalities = new();
    public BalanceSO balance;
}
