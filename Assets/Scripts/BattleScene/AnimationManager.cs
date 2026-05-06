using System;
using System.Collections.Generic;
using UnityEngine;

// 🌟 인스펙터에서 관리할 스킬 데이터 구조체
[Serializable]
public struct SkillAnimationData
{
    public WeaponSkillId skillId;
    public AnimationClip clip;
    public float cooltime;
    public skillType type;
}

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

    [Header("Default Skill Settings")]
    public AnimationClip DefaultSkill;
    public float DefaultCool = 5f;
    public skillType DefaultSkillType = skillType.None;

    [Header("Skill Database (Inspector)")]
    // 🌟 인스펙터에서 편하게 추가/수정할 수 있는 리스트
    [SerializeField] private List<SkillAnimationData> skillDatabase;

    // 🌟 런타임에 빠른 검색을 위한 딕셔너리
    private Dictionary<WeaponSkillId, SkillAnimationData> _skillDict;

    private void Awake()
    {
        Instance = this;

        // 인스펙터의 리스트 데이터를 딕셔너리로 변환하여 초기화
        _skillDict = new Dictionary<WeaponSkillId, SkillAnimationData>();
        if (skillDatabase != null)
        {
            foreach (var data in skillDatabase)
            {
                if (!_skillDict.ContainsKey(data.skillId))
                {
                    _skillDict.Add(data.skillId, data);
                }
            }
        }
    }

    // (기존 코드 그대로 유지)
    public AnimatorOverrideController GetControllerByWeaponType(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.oneHand: return oneHandController;
            case WeaponType.twoHand: return twoHandController;
            case WeaponType.dualHand: return dualHandController;
            case WeaponType.spear: return spearController;
            case WeaponType.shield: return shieldController;
            case WeaponType.dagger: return daggerController;
            case WeaponType.handGun: return handgunController;
            case WeaponType.dualGun: return dualgunController;
            case WeaponType.rifle: return rifleController;
            case WeaponType.staff: return staffController;
            case WeaponType.bow: return bowController;
            case WeaponType.None:
            default: return noneController;
        }
    }

    // 🌟 딕셔너리 기반으로 변경된 함수들 (함수명, 매개변수 유지)
    public AnimationClip getAnimation(WeaponSkillId id)
    {
        if (_skillDict != null && _skillDict.TryGetValue(id, out SkillAnimationData data))
        {
            return data.clip != null ? data.clip : DefaultSkill;
        }
        return DefaultSkill;
    }

    public float getCooltime(WeaponSkillId id)
    {
        if (_skillDict != null && _skillDict.TryGetValue(id, out SkillAnimationData data))
        {
            return data.cooltime;
        }
        return DefaultCool;
    }

    public skillType getSkillType(WeaponSkillId id)
    {
        if (_skillDict != null && _skillDict.TryGetValue(id, out SkillAnimationData data))
        {
            return data.type;
        }
        return DefaultSkillType;
    }
}
