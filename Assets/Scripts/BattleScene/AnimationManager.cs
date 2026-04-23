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
            // 단일 공격기 (attack)
            case WeaponSkillId.HeartAttack: return 10f;
            case WeaponSkillId.ThroatSlit: return 12f;
            case WeaponSkillId.RustyBlade: return 6f;
            case WeaponSkillId.Fireball: return 8f;
            case WeaponSkillId.Lightning: return 15f;
            case WeaponSkillId.RevolverFanning: return 14f;
            case WeaponSkillId.ShieldBash: return 10f;
            case WeaponSkillId.HeadStrike: return 12f;

            // 자가 버프기 (enhance)
            case WeaponSkillId.LongGrip: return 15f;
            case WeaponSkillId.Stimpack: return 12f;
            case WeaponSkillId.BayonetCharge: return 18f;
            case WeaponSkillId.Madness: return 15f;

            // 아군 대상/광역 버프기 (support)
            case WeaponSkillId.Warcry: return 20f;

            // 대상 없는 범위 공격 (None)
            case WeaponSkillId.SpiralSlash: return 14f;

            default:
            case WeaponSkillId.None:
                return 5f; // DefaultCool
        }

    }
    public skillType getSkillType(WeaponSkillId id)
    {
        switch (id)
        {
            // attack: 적 한명 대상 (타겟을 잡고 발동하는 모든 공격)
            case WeaponSkillId.HeartAttack:
            case WeaponSkillId.ThroatSlit:
            case WeaponSkillId.RustyBlade:
            case WeaponSkillId.Fireball:
            case WeaponSkillId.Lightning: // 특정 타겟을 중심으로 터지므로 attack
            case WeaponSkillId.RevolverFanning:
            case WeaponSkillId.ShieldBash:
            case WeaponSkillId.HeadStrike:
                return skillType.attack;

            // enhance: 자가 버프
            case WeaponSkillId.Madness:
            case WeaponSkillId.LongGrip:
            case WeaponSkillId.Stimpack:
            case WeaponSkillId.BayonetCharge:
                return skillType.enhance;

            // support: 아군 대상 (전체 버프 등)
            case WeaponSkillId.Warcry:
                return skillType.support;

            // None: 대상 없이 제자리에서 발동하는 범위 공격 등
            case WeaponSkillId.SpiralSlash:
                return skillType.None;

            default:
            case WeaponSkillId.None:
                return skillType.None;
        }
    }
}
