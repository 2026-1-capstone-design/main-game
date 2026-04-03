using UnityEngine;

[CreateAssetMenu(menuName = "Prototype/Content/Weapon Skill")]
public sealed class WeaponSkillSO : ScriptableObject
{
    public Sprite icon;
    public string skillName = "Weapon Skill";
    [TextArea] public string description;

    public WeaponType weaponType = WeaponType.Sword;
    public WeaponSkillId skillId = WeaponSkillId.None;
}