using UnityEngine;

// IAnimationProvider: 스킬/무기 애니메이션 데이터를 제공하는 인터페이스.
// 프로덕션에서는 AnimationManager가 구현하고, 테스트에서는 Mock으로 교체 가능하다.
public interface IAnimationProvider
{
    AnimationClip getAnimation(WeaponSkillId id);
    float getCooltime(WeaponSkillId id);
    skillType getSkillType(WeaponSkillId id);
    AnimatorOverrideController GetControllerByWeaponType(WeaponType type);
}
