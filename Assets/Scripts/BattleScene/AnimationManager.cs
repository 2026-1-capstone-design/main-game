using UnityEngine;


public class AnimationManager : MonoBehaviour, IAnimationProvider
{
    public static AnimationManager Instance { get; private set; }

    [Header("Weapon Animator Controllers")]
    public AnimatorOverrideController noneController;
    public AnimatorOverrideController oneHandController;
    public AnimatorOverrideController twoHandController;
    public AnimatorOverrideController dualHandController;
    public AnimatorOverrideController spearController;
    public AnimatorOverrideController shieldController;
    public AnimatorOverrideController daggerController;
    public AnimatorOverrideController handgunController;
    public AnimatorOverrideController dualgunController;
    public AnimatorOverrideController rifleController;
    public AnimatorOverrideController staffController;
    public AnimatorOverrideController bowController;

    [Header("Weapon Skill Animation Clip")]
    public AnimationClip DefaultSkill;
    public float DefaultCool;
    public AnimationClip HeartAttack;
    public float HeartAttackCool;

    private void Awake()
    {
        Instance = this;
    }
    public AnimatorOverrideController GetControllerByWeaponType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.oneHand:
                return oneHandController;
            case WeaponType.twoHand:
                return twoHandController;
            case WeaponType.dualHand:
                return dualHandController;
            case WeaponType.spear:
                return spearController;
            case WeaponType.shield:
                return shieldController;
            case WeaponType.dagger:
                return daggerController;
            case WeaponType.handGun:
                return handgunController;
            case WeaponType.dualGun:
                return dualgunController;
            case WeaponType.rifle:
                return rifleController;
            case WeaponType.staff:
                return staffController;
            case WeaponType.bow:
                return bowController;
            case WeaponType.None:
            default:
                return noneController;
        }
    }


    public AnimationClip getAnimation(WeaponSkillId id)
    {
        switch (id)
        {
            case WeaponSkillId.HeartAttack:
                return HeartAttack;
            default:
            case WeaponSkillId.None:
                return DefaultSkill;
        }
    }
    public float getCooltime(WeaponSkillId id)
    {
        switch (id)
        {
            case WeaponSkillId.HeartAttack:
                return HeartAttackCool;
            default:
            case WeaponSkillId.None:
                return DefaultCool;
        }

    }
    public skillType getSkillType(WeaponSkillId id)
    {
        switch (id)
        {
            case WeaponSkillId.HeartAttack:
                return skillType.attack;
            default:
            case WeaponSkillId.None:
                return skillType.None;
        }
    }
}
