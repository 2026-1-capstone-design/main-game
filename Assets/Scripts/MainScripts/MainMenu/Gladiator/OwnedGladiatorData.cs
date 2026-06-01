using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OwnedGladiatorData
{
    public const int MaxEquippedArtifactCount = 3;

    private readonly OwnedArtifactData[] _equippedArtifacts = new OwnedArtifactData[MaxEquippedArtifactCount];

    public int RuntimeId { get; } // 실제 보유 중인 검투사를 식별하는 런타임 ID

    public string DisplayName { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int Loyalty { get; set; }
    public int Upkeep { get; set; }

    public GladiatorClassSO GladiatorClass { get; }
    public TraitSO Trait { get; }
    public PersonalitySO Personality { get; }

    // 기존 단일 슬롯 코드를 위한 첫 슬롯 호환 프로퍼티다.
    public OwnedArtifactData EquippedArtifact
    {
        get => _equippedArtifacts[0];
        set => _equippedArtifacts[0] = value;
    }

    public IReadOnlyList<OwnedArtifactData> EquippedArtifacts => _equippedArtifacts;

    public bool HasAvailableArtifactSlot => Array.Exists(_equippedArtifacts, artifact => artifact == null);
    public OwnedWeaponData EquippedWeapon { get; set; } // 현재 이 검투사가 장착 중인 실제 owned 무기 !!참조!!.

    // 클래스, 레벨, 개체 분산, 장비 보너스를 반영한 실제 전투용 캐시 스탯들.
    // !!UI 표시와 전투 snapshot 생성 시 이 값을 그대로 사용함!!
    public float CachedMaxHealth { get; set; }
    public float CurrentHealth { get; set; }
    public float CachedAttack { get; set; }
    public float CachedAttackSpeed { get; set; }
    public float CachedMoveSpeed { get; set; }
    public float CachedAttackRange { get; set; }

    public float FinalHealthVariancePercent { get; set; }
    public float FinalAttackVariancePercent { get; set; }

    //커스터마이즈 정보
    public int[] CustomizeIndicates { get; set; }

    // 실제 보유 검투사 1명의 기본 정보를 담는 런타임 데이터 생성자
    // 초기엔 캐시 스탯이 비어 있고, 이후 별도 RefreshDerivedStats로 완성이되는 것
    public OwnedGladiatorData(
        int runtimeId,
        string displayName,
        int level,
        int exp,
        int loyalty,
        int upkeep,
        GladiatorClassSO gladiatorClass,
        TraitSO trait,
        PersonalitySO personality,
        OwnedArtifactData equippedArtifact,
        OwnedWeaponData equippedWeapon,
        int[] customizeIndicates
    )
    {
        RuntimeId = runtimeId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Gladiator" : displayName;
        Level = Mathf.Max(1, level);
        Exp = Mathf.Max(0, exp);
        Loyalty = Mathf.Max(0, loyalty);
        Upkeep = Mathf.Max(0, upkeep);

        GladiatorClass = gladiatorClass;
        Trait = trait;
        Personality = personality;

        EquippedArtifact = equippedArtifact;
        EquippedWeapon = equippedWeapon;

        CachedMaxHealth = 0f;
        CurrentHealth = 0f;
        CachedAttack = 0f;
        CachedAttackSpeed = 0f;
        CachedMoveSpeed = 0f;
        CachedAttackRange = 0f;

        FinalHealthVariancePercent = 0f;
        FinalAttackVariancePercent = 0f;

        CustomizeIndicates = customizeIndicates;
    }

    public OwnedArtifactData GetEquippedArtifact(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _equippedArtifacts.Length ? _equippedArtifacts[slotIndex] : null;
    }

    public bool ContainsEquippedArtifact(OwnedArtifactData artifact)
    {
        return IndexOfEquippedArtifact(artifact) >= 0;
    }

    public int IndexOfEquippedArtifact(OwnedArtifactData artifact)
    {
        if (artifact == null)
        {
            return -1;
        }

        for (int i = 0; i < _equippedArtifacts.Length; i++)
        {
            if (_equippedArtifacts[i] == artifact)
            {
                return i;
            }
        }

        return -1;
    }

    public bool TryEquipArtifact(OwnedArtifactData artifact)
    {
        if (artifact == null || ContainsEquippedArtifact(artifact))
        {
            return false;
        }

        for (int i = 0; i < _equippedArtifacts.Length; i++)
        {
            if (_equippedArtifacts[i] == null)
            {
                _equippedArtifacts[i] = artifact;
                return true;
            }
        }

        return false;
    }

    public bool UnequipArtifact(OwnedArtifactData artifact)
    {
        int slotIndex = IndexOfEquippedArtifact(artifact);
        if (slotIndex < 0)
        {
            return false;
        }

        _equippedArtifacts[slotIndex] = null;
        CompactEquippedArtifacts();
        return true;
    }

    public void SetEquippedArtifacts(IReadOnlyList<OwnedArtifactData> artifacts)
    {
        ClearEquippedArtifacts();
        if (artifacts == null)
        {
            return;
        }

        for (int i = 0; i < artifacts.Count && i < _equippedArtifacts.Length; i++)
        {
            _equippedArtifacts[i] = artifacts[i];
        }

        CompactEquippedArtifacts();
    }

    public void ClearEquippedArtifacts()
    {
        for (int i = 0; i < _equippedArtifacts.Length; i++)
        {
            _equippedArtifacts[i] = null;
        }
    }

    private void CompactEquippedArtifacts()
    {
        int writeIndex = 0;
        for (int readIndex = 0; readIndex < _equippedArtifacts.Length; readIndex++)
        {
            OwnedArtifactData artifact = _equippedArtifacts[readIndex];
            if (artifact == null)
            {
                continue;
            }

            _equippedArtifacts[writeIndex++] = artifact;
        }

        while (writeIndex < _equippedArtifacts.Length)
        {
            _equippedArtifacts[writeIndex++] = null;
        }
    }
}
