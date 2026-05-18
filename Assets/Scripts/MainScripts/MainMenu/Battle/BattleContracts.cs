using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleEncounterDifficulty
{
    VeryLow = -1,
    Low = 0,
    Medium = 1,
    High = 2,
}

[Serializable]
public sealed class BattleUnitSnapshot
{
    public int SourceRuntimeId { get; } // 원본인 OwnedGladiatorData의 RuntimeId.
    public BattleTeamId TeamId { get; }

    // 전투 중 유닛이 어떤 실제로 중인 보유 검투사에서 복사됐는지 추적할 때 기준이 된다.
    public string DisplayName { get; }
    public int Level { get; }
    public int Loyalty { get; }
    public float MaxHealth { get; }
    public float CurrentHealth { get; }
    public float Attack { get; }
    public float AttackSpeed { get; }
    public float MoveSpeed { get; }
    public float AttackRange { get; }

    public GladiatorClassSO GladiatorClass { get; }
    public TraitSO Trait { get; }
    public PersonalitySO Personality { get; }
    public ArtifactSO EquippedArtifact { get; }
    public IReadOnlyList<ArtifactId> ArtifactIds { get; }
    public WeaponType WeaponType { get; }

    // 무기 왼쪽 오른쪽 추가
    public GameObject LeftWeaponPrefab { get; }
    public GameObject RightWeaponPrefab { get; }

    //무기 스킬
    public WeaponSkillId WeaponSkillId { get; }

    //커스터마이징 배열
    public int[] CustomizeIndicates { get; }

    public bool IsRanged { get; }
    public bool UseProjectile { get; }
    public Sprite PortraitSprite { get; }

    public bool DefaultDur { get; set; }
    public float Duration { get; set; }

    public bool SkillDefaultDur { get; }
    public float SkillDuration { get; }

    public BattleUnitSnapshot(
        int sourceRuntimeId,
        BattleTeamId teamId,
        string displayName,
        int level,
        int loyalty,
        float maxHealth,
        float currentHealth,
        float attack,
        float attackSpeed,
        float moveSpeed,
        float attackRange,
        GladiatorClassSO gladiatorClass,
        TraitSO trait,
        PersonalitySO personality,
        ArtifactSO equippedArtifact,
        WeaponType weaponType,
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab,
        WeaponSkillId weaponSkillId,
        int[] customizeIndicates,
        bool isRanged,
        bool useProjectile,
        Sprite portraitSprite,
        bool defaultDur = false,
        float duration = 1f,
        bool skillDefaultDur = true,
        float skillDuration = 1f
    )
    {
        SourceRuntimeId = sourceRuntimeId;
        TeamId = teamId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Gladiator" : displayName;
        Level = Mathf.Max(1, level);
        Loyalty = Mathf.Max(0, loyalty);
        MaxHealth = Mathf.Max(0f, maxHealth);
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        Attack = Mathf.Max(0f, attack);
        AttackSpeed = Mathf.Max(0f, attackSpeed);
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        AttackRange = Mathf.Max(0f, attackRange);

        GladiatorClass = gladiatorClass;
        Trait = trait;
        Personality = personality;
        EquippedArtifact = equippedArtifact;
        ArtifactIds = BuildArtifactIds(equippedArtifact);
        WeaponType = weaponType;

        // 생성자에 추가
        LeftWeaponPrefab = leftWeaponPrefab;
        RightWeaponPrefab = rightWeaponPrefab;

        WeaponSkillId = weaponSkillId;

        CustomizeIndicates = customizeIndicates;

        IsRanged = isRanged;
        UseProjectile = useProjectile;
        PortraitSprite = portraitSprite;

        DefaultDur = defaultDur;
        Duration = duration;

        SkillDefaultDur = skillDefaultDur;
        SkillDuration = skillDuration;
    }

    // snapshot을 그대로 카피.
    // 전투 시작 payload 구성 시 원본 preview를 직접 공유하지 않기 위해
    public BattleUnitSnapshot Clone()
    {
        return new BattleUnitSnapshot(
            SourceRuntimeId,
            TeamId,
            DisplayName,
            Level,
            Loyalty,
            MaxHealth,
            CurrentHealth,
            Attack,
            AttackSpeed,
            MoveSpeed,
            AttackRange,
            GladiatorClass,
            Trait,
            Personality,
            EquippedArtifact,
            WeaponType,
            LeftWeaponPrefab,
            RightWeaponPrefab,
            WeaponSkillId,
            CustomizeIndicates,
            IsRanged,
            UseProjectile,
            PortraitSprite,
            DefaultDur,
            Duration,
            SkillDefaultDur,
            SkillDuration
        );
    }

    // 실제 보유 검투사 데이터를 전투 시작용 snapshot으로 변환.
    // 현재 보유 스탯, 장착 무기 타입/프리팹/스킬, 초상화까지 여기서 복사됨
    public static BattleUnitSnapshot FromOwnedGladiator(
        OwnedGladiatorData source,
        BattleTeamId teamId,
        Sprite portraitSprite = null
    )
    {
        if (source == null)
        {
            return null;
        }

        Sprite resolvedPortrait = portraitSprite;

        if (resolvedPortrait == null && source.GladiatorClass != null)
        {
            resolvedPortrait = source.GladiatorClass.icon;
        }
        WeaponType weaponType = WeaponType.None;

        GameObject leftPrefab = null;
        GameObject rightPrefab = null;

        WeaponSkillId weaponSkillId = WeaponSkillId.None;
        bool isRanged = false;
        bool useProjectile = false;

        bool defaultDur = false;
        float duration = 1f;

        bool skillDefaultDur = true;
        float skillDuration = 1f;

        if (source.EquippedWeapon != null)
        {
            WeaponSO weapon = source.EquippedWeapon.Weapon;
            WeaponSkillSO weaponSkill = source.EquippedWeapon.WeaponSkill;

            if (weapon != null)
            {
                weaponType = weapon.weaponType;
                // 무기 추가
                leftPrefab = weapon.leftWeaponPrefab;
                rightPrefab = weapon.rightWeaponPrefab;

                isRanged = weapon.isRanged;
                useProjectile = weapon.useProjectile;

                defaultDur = weapon.defaultDur;
                duration = weapon.duration;
            }

            if (weaponSkill != null)
            {
                weaponSkillId = weaponSkill.skillId;
                skillDefaultDur = weaponSkill.defaultDur;
                skillDuration = weaponSkill.duration;
            }
        }

        return new BattleUnitSnapshot(
            source.RuntimeId,
            teamId,
            source.DisplayName,
            source.Level,
            source.Loyalty,
            source.CachedMaxHealth,
            source.CurrentHealth,
            source.CachedAttack,
            source.CachedAttackSpeed,
            source.CachedMoveSpeed,
            source.CachedAttackRange,
            source.GladiatorClass,
            source.Trait,
            source.Personality,
            source.EquippedArtifact,
            weaponType,
            leftPrefab,
            rightPrefab,
            weaponSkillId,
            source.CustomizeIndicates,
            isRanged,
            useProjectile,
            resolvedPortrait,
            defaultDur,
            duration,
            skillDefaultDur,
            skillDuration
        );
    }

    private static IReadOnlyList<ArtifactId> BuildArtifactIds(ArtifactSO equippedArtifact)
    {
        if (equippedArtifact == null || equippedArtifact.artifactId == ArtifactId.None)
            return Array.Empty<ArtifactId>();

        return new[] { equippedArtifact.artifactId };
    }
}

[Serializable]
public sealed class BattleEncounterPreview
{
    private readonly List<BattleUnitSnapshot> _enemyUnits = new List<BattleUnitSnapshot>();

    public int EncounterIndex { get; set; } // '메인 씬의 전투 패널 상의' 전투 후보 목록에서 이 적 팀이 몇 번째 줄인지 나타내는 인덱스
    public BattleEncounterDifficulty Difficulty { get; set; }
    public float AverageLevel { get; }
    public int PreviewRewardGold { get; set; }

    // 전투 준비 화면에 보여주고,
    // 실제 전투 시작 시 enemy payload의 원본이 되는 적 팀 snapshot 목록임
    public IReadOnlyList<BattleUnitSnapshot> EnemyUnits => _enemyUnits;

    // 하루치 전투 후보 1줄을 구성하는 데이터들
    // 적 유닛 목록, 평균레벨, 보상, 난이도를 모두 들고 있음
    public BattleEncounterPreview(
        int encounterIndex,
        IEnumerable<BattleUnitSnapshot> enemyUnits,
        float averageLevel,
        int previewRewardGold,
        BattleEncounterDifficulty difficulty
    )
    {
        EncounterIndex = Mathf.Max(0, encounterIndex);
        AverageLevel = Mathf.Max(0f, averageLevel);
        PreviewRewardGold = Mathf.Max(0, previewRewardGold);
        Difficulty = difficulty;

        if (enemyUnits != null)
        {
            foreach (BattleUnitSnapshot unit in enemyUnits)
            {
                if (unit != null)
                {
                    _enemyUnits.Add(unit);
                }
            }
        }
    }
}

[Serializable]
public sealed class BattleStartPayload
{
    private readonly List<BattleTeamEntry> _teams = new List<BattleTeamEntry>();
    private readonly Dictionary<BattleTeamId, BattleTeamEntry> _teamById =
        new Dictionary<BattleTeamId, BattleTeamEntry>();
    private readonly Dictionary<BattleTeamId, int> _teamStartUnitNumberById = new Dictionary<BattleTeamId, int>();

    // 팀 local unit index와 전투/관측 슬롯 index를 분리해, sparse slot layout을 episode 시작 시 고정한다.
    // _teamSlotIndexById[teamId][localUnitIndex] = slotIndex
    // localUnitIndex는 팀 내 유닛 번호(0부터 시작), slotIndex는 전투 시스템에서 유닛이 실제로 차지하는 슬롯 번호(0부터 시작, 최대 BattleTeamConstants.MaxUnitsPerTeam - 1)
    private readonly Dictionary<BattleTeamId, int[]> _teamSlotIndexById = new Dictionary<BattleTeamId, int[]>();

    public IReadOnlyList<BattleTeamEntry> Teams => _teams;
    public BattleTeamId PlayerTeamId { get; }
    public int SelectedEncounterIndex { get; }
    public float EnemyAverageLevel { get; }
    public int PreviewRewardGold { get; }

    // 랜덤매니저에서 쓸 시드
    public int BattleSeed { get; }

    private BattleTeamEntry _playerTeam;
    private BattleTeamEntry _hostileTeam;

    public BattleStartPayload(
        IEnumerable<BattleTeamEntry> teams,
        BattleTeamId playerTeamId,
        int selectedEncounterIndex,
        float enemyAverageLevel,
        int previewRewardGold,
        int battleSeed,
        IReadOnlyDictionary<BattleTeamId, IReadOnlyList<int>> teamSlotIndicesById = null
    )
    {
        if (teams != null)
        {
            foreach (BattleTeamEntry team in teams)
            {
                if (team != null)
                {
                    _teams.Add(team);
                }
            }
        }

        if (_teams.Count == 0)
        {
            throw new ArgumentException("BattleStartPayload requires at least one team.", nameof(teams));
        }

        if (_teams.Count != BattleTeamConstants.TeamCount)
        {
            throw new ArgumentException(
                $"BattleStartPayload requires exactly {BattleTeamConstants.TeamCount} teams.",
                nameof(teams)
            );
        }

        int nextUnitNumber = 1;
        for (int i = 0; i < _teams.Count; i++)
        {
            BattleTeamEntry team = _teams[i];
            if (team == null)
            {
                continue;
            }

            int unitCount = team.Units != null ? team.Units.Count : 0;
            if (unitCount <= 0)
            {
                throw new ArgumentException(
                    $"BattleStartPayload requires at least one unit for team {team.TeamId.Value}.",
                    nameof(teams)
                );
            }

            if (unitCount > BattleTeamConstants.MaxUnitsPerTeam)
            {
                throw new ArgumentException(
                    $"BattleStartPayload supports up to {BattleTeamConstants.MaxUnitsPerTeam} units per team.",
                    nameof(teams)
                );
            }

            if (_teamById.ContainsKey(team.TeamId))
            {
                throw new ArgumentException($"Duplicate team id {team.TeamId.Value} detected.", nameof(teams));
            }

            _teamById[team.TeamId] = team;
            _teamStartUnitNumberById[team.TeamId] = nextUnitNumber;
            _teamSlotIndexById[team.TeamId] = ResolveTeamSlotIndices(team, teamSlotIndicesById);
            nextUnitNumber += unitCount;

            if (team.TeamId == playerTeamId)
            {
                _playerTeam = team;
            }
            else if (_hostileTeam == null)
            {
                _hostileTeam = team;
            }
            else
            {
                throw new ArgumentException("BattleStartPayload supports only one hostile team.", nameof(teams));
            }
        }

        if (_playerTeam == null)
        {
            throw new ArgumentException(
                $"BattleStartPayload requires a player team entry for team id {playerTeamId.Value}.",
                nameof(playerTeamId)
            );
        }

        if (_hostileTeam == null)
        {
            throw new ArgumentException("BattleStartPayload requires exactly one hostile team.", nameof(teams));
        }

        PlayerTeamId = playerTeamId;
        SelectedEncounterIndex = Mathf.Max(0, selectedEncounterIndex);
        EnemyAverageLevel = Mathf.Max(0f, enemyAverageLevel);
        PreviewRewardGold = Mathf.Max(0, previewRewardGold);
        BattleSeed = battleSeed;
    }

    public BattleTeamEntry GetPlayerTeam() => _playerTeam;

    public BattleTeamEntry GetHostileTeam() => _hostileTeam;

    public bool TryGetTeam(BattleTeamId teamId, out BattleTeamEntry team) => _teamById.TryGetValue(teamId, out team);

    public int GetTeamUnitCount(BattleTeamId teamId) =>
        TryGetTeam(teamId, out BattleTeamEntry team) && team.Units != null ? team.Units.Count : 0;

    public int GetTeamStartUnitNumber(BattleTeamId teamId) =>
        _teamStartUnitNumberById.TryGetValue(teamId, out int startUnitNumber) ? startUnitNumber : -1;

    public int AllocateUnitNumber(BattleTeamId teamId, int localUnitIndex)
    {
        if (!TryGetTeam(teamId, out BattleTeamEntry team))
        {
            throw new ArgumentException($"Unknown team id {teamId.Value}.", nameof(teamId));
        }

        if (localUnitIndex < 0 || localUnitIndex >= team.Units.Count)
            throw new ArgumentOutOfRangeException(nameof(localUnitIndex));
        return GetTeamStartUnitNumber(teamId) + localUnitIndex;
    }

    public bool TryGetTeamSlotIndex(BattleTeamId teamId, int unitNumber, out int slotIndex)
    {
        slotIndex = -1;
        if (!TryGetTeamLocalUnitIndex(teamId, unitNumber, out int localUnitIndex))
        {
            return false;
        }

        if (!_teamSlotIndexById.TryGetValue(teamId, out int[] slotIndices))
        {
            return false;
        }

        if (localUnitIndex < 0 || localUnitIndex >= slotIndices.Length)
        {
            return false;
        }

        slotIndex = slotIndices[localUnitIndex];
        return slotIndex >= 0;
    }

    public IReadOnlyList<int> GetTeamSlotIndices(BattleTeamId teamId)
    {
        return _teamSlotIndexById.TryGetValue(teamId, out int[] slotIndices) ? slotIndices : Array.Empty<int>();
    }

    public bool TryGetTeamLocalUnitIndex(BattleTeamId teamId, int unitNumber, out int localUnitIndex)
    {
        localUnitIndex = -1;

        if (!TryGetTeam(teamId, out BattleTeamEntry team))
        {
            return false;
        }

        int startUnitNumber = GetTeamStartUnitNumber(teamId);
        if (startUnitNumber <= 0)
        {
            return false;
        }

        int unitIndex = unitNumber - startUnitNumber;
        if (unitIndex < 0 || unitIndex >= team.Units.Count)
        {
            return false;
        }

        localUnitIndex = unitIndex;
        return true;
    }

    private static int[] ResolveTeamSlotIndices(
        BattleTeamEntry team,
        IReadOnlyDictionary<BattleTeamId, IReadOnlyList<int>> teamSlotIndicesById
    )
    {
        int unitCount = team != null && team.Units != null ? team.Units.Count : 0;
        var slotIndices = new int[unitCount];
        if (
            team != null
            && teamSlotIndicesById != null
            && teamSlotIndicesById.TryGetValue(team.TeamId, out IReadOnlyList<int> configuredSlots)
            && configuredSlots != null
        )
        {
            if (configuredSlots.Count != unitCount)
            {
                throw new ArgumentException(
                    $"Configured slot count for team {team.TeamId.Value} must match unit count.",
                    nameof(teamSlotIndicesById)
                );
            }

            var usedSlots = new HashSet<int>();
            for (int i = 0; i < configuredSlots.Count; i++)
            {
                int slotIndex = configuredSlots[i];
                if (slotIndex < 0 || slotIndex >= BattleTeamConstants.MaxUnitsPerTeam)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(teamSlotIndicesById),
                        $"Configured slot {slotIndex} is out of range for team {team.TeamId.Value}."
                    );
                }

                if (!usedSlots.Add(slotIndex))
                {
                    throw new ArgumentException(
                        $"Configured slots for team {team.TeamId.Value} must be unique.",
                        nameof(teamSlotIndicesById)
                    );
                }

                slotIndices[i] = slotIndex;
            }

            return slotIndices;
        }

        for (int i = 0; i < unitCount; i++)
        {
            slotIndices[i] = i;
        }

        return slotIndices;
    }
}
