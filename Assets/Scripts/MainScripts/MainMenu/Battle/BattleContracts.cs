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

// 메인 씬 배치 화면에서 선택한 스쿼드와 보드 칸 정보를 전투 payload 생성까지 전달한다.
// 런타임 ID 배열은 인덱스가 배치 칸이고 값이 해당 칸에 놓인 검투사 ID다.
public sealed class BattleDeploymentPlan
{
    public int SquadTeamIndex { get; }
    public int[] AllyRuntimeIdsBySlot { get; }
    public int[] EnemyUnitIndicesBySlot { get; }
    public Vector2[] AllyNormalizedPositionsBySlot { get; }
    public Vector2[] EnemyNormalizedPositionsBySlot { get; }

    public BattleDeploymentPlan(
        int squadTeamIndex,
        int[] allyRuntimeIdsBySlot,
        int[] enemyUnitIndicesBySlot,
        Vector2[] allyNormalizedPositionsBySlot = null,
        Vector2[] enemyNormalizedPositionsBySlot = null
    )
    {
        SquadTeamIndex = Mathf.Max(0, squadTeamIndex);
        AllyRuntimeIdsBySlot = allyRuntimeIdsBySlot ?? Array.Empty<int>();
        EnemyUnitIndicesBySlot = enemyUnitIndicesBySlot ?? Array.Empty<int>();
        AllyNormalizedPositionsBySlot = allyNormalizedPositionsBySlot ?? Array.Empty<Vector2>();
        EnemyNormalizedPositionsBySlot = enemyNormalizedPositionsBySlot ?? Array.Empty<Vector2>();
    }
}

// 메인 씬의 원형 배치 UI와 전투 씬의 battlefield bounds가 같은 좌표계를 공유하도록 정규화 좌표를 만든다.
public static class BattleDeploymentPositionUtility
{
    public static Vector2 ClampToDeploymentHalf(Vector2 normalizedPosition, bool isPlayerTeam)
    {
        if (
            float.IsNaN(normalizedPosition.x)
            || float.IsNaN(normalizedPosition.y)
            || float.IsInfinity(normalizedPosition.x)
            || float.IsInfinity(normalizedPosition.y)
        )
        {
            normalizedPosition = Vector2.zero;
        }

        normalizedPosition.x = Mathf.Clamp(normalizedPosition.x, -1f, 1f);
        normalizedPosition.y = Mathf.Clamp(normalizedPosition.y, -1f, 1f);

        float magnitude = normalizedPosition.magnitude;
        if (magnitude > 1f)
        {
            normalizedPosition /= magnitude;
        }

        normalizedPosition.x = isPlayerTeam
            ? Mathf.Clamp(normalizedPosition.x, 0f, 1f)
            : Mathf.Clamp(normalizedPosition.x, -1f, 0f);
        return normalizedPosition;
    }

    public static Vector2 BuildDefaultPosition(int index, int count, bool isPlayerTeam)
    {
        count = Mathf.Clamp(count, 1, BattleTeamConstants.MaxUnitsPerTeam);
        index = Mathf.Clamp(index, 0, count - 1);

        float x = isPlayerTeam ? 0.45f : -0.45f;
        float y = count <= 1 ? 0f : Mathf.Lerp(0.65f, -0.65f, index / (float)(count - 1));
        return ClampToDeploymentHalf(new Vector2(x, y), isPlayerTeam);
    }

    public static IReadOnlyList<Vector2> BuildRandomEnemyPositions(
        RandomManager randomManager,
        int count,
        RandomStreamType streamType
    )
    {
        const float minRadius = 0.28f;
        const float maxRadius = 0.82f;
        const float minSpacing = 0.24f;
        const int maxAttemptsPerUnit = 64;

        count = Mathf.Clamp(count, 0, BattleTeamConstants.MaxUnitsPerTeam);
        List<Vector2> positions = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxAttemptsPerUnit; attempt++)
            {
                float angle = NextFloatRange(randomManager, streamType, 90f, 270f) * Mathf.Deg2Rad;
                float radius = Mathf.Sqrt(
                    NextFloatRange(randomManager, streamType, minRadius * minRadius, maxRadius * maxRadius)
                );
                Vector2 candidate = ClampToDeploymentHalf(
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    false
                );

                if (HasMinimumSpacing(positions, candidate, minSpacing))
                {
                    positions.Add(candidate);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                positions.Add(BuildDefaultPosition(i, count, false));
            }
        }

        return positions;
    }

    public static IReadOnlyList<Vector2> AssignEnemyPositionsByAttackRange(
        IReadOnlyList<Vector2> positions,
        IReadOnlyList<BattleUnitSnapshot> units
    )
    {
        int count = units != null ? units.Count : 0;
        List<Vector2> assignedPositions = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            assignedPositions.Add(BuildDefaultPosition(i, count, false));
        }

        if (positions == null || positions.Count < count || count <= 1)
        {
            return assignedPositions;
        }

        List<Vector2> sortedPositions = new List<Vector2>(positions);
        sortedPositions.Sort(CompareByDistanceFromCenterLine);

        List<int> unitIndices = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            unitIndices.Add(i);
        }

        unitIndices.Sort(
            (left, right) =>
            {
                int rangeComparison = units[left].AttackRange.CompareTo(units[right].AttackRange);
                return rangeComparison != 0 ? rangeComparison : left.CompareTo(right);
            }
        );

        for (int i = 0; i < count; i++)
        {
            assignedPositions[unitIndices[i]] = sortedPositions[i];
        }

        return assignedPositions;
    }

    private static int CompareByDistanceFromCenterLine(Vector2 left, Vector2 right)
    {
        int centerLineDistanceComparison = Mathf.Abs(left.x).CompareTo(Mathf.Abs(right.x));
        if (centerLineDistanceComparison != 0)
        {
            return centerLineDistanceComparison;
        }

        int centerDistanceComparison = left.sqrMagnitude.CompareTo(right.sqrMagnitude);
        return centerDistanceComparison != 0 ? centerDistanceComparison : left.y.CompareTo(right.y);
    }

    private static bool HasMinimumSpacing(IReadOnlyList<Vector2> positions, Vector2 candidate, float minSpacing)
    {
        float minSpacingSquared = minSpacing * minSpacing;
        for (int i = 0; i < positions.Count; i++)
        {
            if ((positions[i] - candidate).sqrMagnitude < minSpacingSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static float NextFloatRange(
        RandomManager randomManager,
        RandomStreamType streamType,
        float minInclusive,
        float maxInclusive
    )
    {
        return randomManager != null
            ? randomManager.NextFloatRange(streamType, minInclusive, maxInclusive)
            : UnityEngine.Random.Range(minInclusive, maxInclusive);
    }
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
    public IReadOnlyList<ArtifactSO> EquippedArtifacts { get; }
    public IReadOnlyList<ArtifactId> ArtifactIds { get; }
    public WeaponType WeaponType { get; }
    public string WeaponName { get; }
    public Sprite WeaponIconSprite { get; }

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
    public GladiatorPersonalityBias PersonalityBias { get; }

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
        string weaponName,
        Sprite weaponIconSprite,
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
        float skillDuration = 1f,
        GladiatorPersonalityBias personalityBias = default,
        IReadOnlyList<ArtifactSO> equippedArtifacts = null
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
        EquippedArtifacts = BuildEquippedArtifacts(equippedArtifact, equippedArtifacts);
        EquippedArtifact = EquippedArtifacts.Count > 0 ? EquippedArtifacts[0] : null;
        ArtifactIds = BuildArtifactIds(EquippedArtifacts);
        WeaponType = weaponType;
        WeaponName = string.IsNullOrWhiteSpace(weaponName) ? string.Empty : weaponName;
        WeaponIconSprite = weaponIconSprite;

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
        PersonalityBias = personalityBias.IsValid ? personalityBias : GladiatorPersonalityBias.Neutral;
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
            WeaponName,
            WeaponIconSprite,
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
            SkillDuration,
            PersonalityBias,
            EquippedArtifacts
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
        string weaponName = string.Empty;

        GameObject leftPrefab = null;
        GameObject rightPrefab = null;
        Sprite weaponIconSprite = null;

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
                weaponName = weapon.weaponName;
                // 무기 추가
                leftPrefab = weapon.leftWeaponPrefab;
                rightPrefab = weapon.rightWeaponPrefab;
                weaponIconSprite = weapon.icon;

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
            source.EquippedArtifact != null ? source.EquippedArtifact.Artifact : null,
            weaponType,
            weaponName,
            weaponIconSprite,
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
            skillDuration,
            equippedArtifacts: BuildArtifactSOs(source.EquippedArtifacts)
        );
    }

    private static IReadOnlyList<ArtifactSO> BuildEquippedArtifacts(
        ArtifactSO legacyEquippedArtifact,
        IReadOnlyList<ArtifactSO> equippedArtifacts
    )
    {
        List<ArtifactSO> result = new List<ArtifactSO>(OwnedGladiatorData.MaxEquippedArtifactCount);
        if (equippedArtifacts != null)
        {
            for (
                int i = 0;
                i < equippedArtifacts.Count && result.Count < OwnedGladiatorData.MaxEquippedArtifactCount;
                i++
            )
            {
                ArtifactSO artifact = equippedArtifacts[i];
                if (artifact != null)
                {
                    result.Add(artifact);
                }
            }
        }

        if (result.Count == 0 && legacyEquippedArtifact != null)
        {
            result.Add(legacyEquippedArtifact);
        }

        return result;
    }

    private static IReadOnlyList<ArtifactSO> BuildArtifactSOs(IReadOnlyList<OwnedArtifactData> equippedArtifacts)
    {
        List<ArtifactSO> result = new List<ArtifactSO>(OwnedGladiatorData.MaxEquippedArtifactCount);
        if (equippedArtifacts == null)
        {
            return result;
        }

        for (int i = 0; i < equippedArtifacts.Count && result.Count < OwnedGladiatorData.MaxEquippedArtifactCount; i++)
        {
            ArtifactSO artifact = equippedArtifacts[i]?.Artifact;
            if (artifact != null)
            {
                result.Add(artifact);
            }
        }

        return result;
    }

    private static IReadOnlyList<ArtifactId> BuildArtifactIds(IReadOnlyList<ArtifactSO> equippedArtifacts)
    {
        List<ArtifactId> result = new List<ArtifactId>();
        if (equippedArtifacts == null)
        {
            return result;
        }

        for (int i = 0; i < equippedArtifacts.Count; i++)
        {
            ArtifactSO artifact = equippedArtifacts[i];
            if (artifact != null && artifact.ArtifactPerkId != ArtifactId.None)
            {
                result.Add(artifact.ArtifactPerkId);
            }
        }

        return result;
    }
}

[Serializable]
public sealed class BattleEncounterPreview
{
    private readonly List<BattleUnitSnapshot> _enemyUnits = new List<BattleUnitSnapshot>();
    private readonly List<Vector2> _enemyDeploymentNormalizedPositions = new List<Vector2>();

    public int EncounterIndex { get; set; } // '메인 씬의 전투 패널 상의' 전투 후보 목록에서 이 적 팀이 몇 번째 줄인지 나타내는 인덱스
    public BattleEncounterDifficulty Difficulty { get; set; }
    public float AverageLevel { get; }
    public int PreviewRewardGold { get; set; }

    // 전투 준비 화면에 보여주고,
    // 실제 전투 시작 시 enemy payload의 원본이 되는 적 팀 snapshot 목록임
    public IReadOnlyList<BattleUnitSnapshot> EnemyUnits => _enemyUnits;
    public IReadOnlyList<Vector2> EnemyDeploymentNormalizedPositions => _enemyDeploymentNormalizedPositions;

    // 하루치 전투 후보 1줄을 구성하는 데이터들
    // 적 유닛 목록, 평균레벨, 보상, 난이도를 모두 들고 있음
    public BattleEncounterPreview(
        int encounterIndex,
        IEnumerable<BattleUnitSnapshot> enemyUnits,
        float averageLevel,
        int previewRewardGold,
        BattleEncounterDifficulty difficulty,
        IEnumerable<Vector2> enemyDeploymentNormalizedPositions = null
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

        if (enemyDeploymentNormalizedPositions != null)
        {
            foreach (Vector2 position in enemyDeploymentNormalizedPositions)
            {
                if (_enemyDeploymentNormalizedPositions.Count >= _enemyUnits.Count)
                {
                    break;
                }

                _enemyDeploymentNormalizedPositions.Add(
                    BattleDeploymentPositionUtility.ClampToDeploymentHalf(position, false)
                );
            }
        }

        while (_enemyDeploymentNormalizedPositions.Count < _enemyUnits.Count)
        {
            _enemyDeploymentNormalizedPositions.Add(
                BattleDeploymentPositionUtility.BuildDefaultPosition(
                    _enemyDeploymentNormalizedPositions.Count,
                    _enemyUnits.Count,
                    false
                )
            );
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
    public int CurrentDay { get; }
    public BattleEncounterDifficulty Difficulty { get; }
    public int PlayerSquadTeamIndex { get; }
    public IReadOnlyList<int> PlayerDeploymentSlotIndices { get; }
    public IReadOnlyList<int> EnemyDeploymentSlotIndices { get; }
    public IReadOnlyList<Vector2> PlayerDeploymentNormalizedPositions { get; }
    public IReadOnlyList<Vector2> EnemyDeploymentNormalizedPositions { get; }

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
        IEnumerable<int> playerDeploymentSlotIndices = null,
        IEnumerable<int> enemyDeploymentSlotIndices = null,
        IEnumerable<Vector2> playerDeploymentNormalizedPositions = null,
        IEnumerable<Vector2> enemyDeploymentNormalizedPositions = null,
        IReadOnlyDictionary<BattleTeamId, IReadOnlyList<int>> teamSlotIndicesById = null,
        int currentDay = 1,
        BattleEncounterDifficulty difficulty = BattleEncounterDifficulty.Medium,
        int playerSquadTeamIndex = 0
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
        CurrentDay = Mathf.Max(1, currentDay);
        Difficulty = difficulty;
        PlayerSquadTeamIndex = Mathf.Max(0, playerSquadTeamIndex);
        BattleSeed = battleSeed;
        PlayerDeploymentSlotIndices = BuildDeploymentSlotIndices(playerDeploymentSlotIndices, _playerTeam.Units.Count);
        EnemyDeploymentSlotIndices = BuildDeploymentSlotIndices(enemyDeploymentSlotIndices, _hostileTeam.Units.Count);
        if (teamSlotIndicesById == null)
        {
            _teamSlotIndexById[_playerTeam.TeamId] = BuildTeamSlotIndices(PlayerDeploymentSlotIndices, _playerTeam);
            _teamSlotIndexById[_hostileTeam.TeamId] = BuildTeamSlotIndices(EnemyDeploymentSlotIndices, _hostileTeam);
        }

        PlayerDeploymentNormalizedPositions = BuildDeploymentNormalizedPositions(
            playerDeploymentNormalizedPositions,
            _playerTeam.Units.Count,
            true
        );
        EnemyDeploymentNormalizedPositions = BuildDeploymentNormalizedPositions(
            enemyDeploymentNormalizedPositions,
            _hostileTeam.Units.Count,
            false
        );
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

    private static int[] BuildTeamSlotIndices(IReadOnlyList<int> deploymentSlotIndices, BattleTeamEntry team)
    {
        int unitCount = team != null && team.Units != null ? team.Units.Count : 0;
        var slotIndices = new int[unitCount];
        var usedSlots = new HashSet<int>();

        for (int i = 0; i < unitCount; i++)
        {
            int slotIndex =
                deploymentSlotIndices != null && i < deploymentSlotIndices.Count ? deploymentSlotIndices[i] : i;
            if (slotIndex < 0 || slotIndex >= BattleTeamConstants.MaxUnitsPerTeam)
            {
                slotIndex = i;
            }

            if (!usedSlots.Add(slotIndex))
            {
                slotIndex = FindFirstUnusedSlot(usedSlots);
                usedSlots.Add(slotIndex);
            }

            slotIndices[i] = slotIndex;
        }

        return slotIndices;
    }

    private static int FindFirstUnusedSlot(HashSet<int> usedSlots)
    {
        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            if (!usedSlots.Contains(i))
            {
                return i;
            }
        }

        return 0;
    }

    private static IReadOnlyList<int> BuildDeploymentSlotIndices(IEnumerable<int> source, int unitCount)
    {
        List<int> result = new List<int>(Mathf.Max(0, unitCount));
        if (source != null)
        {
            foreach (int slotIndex in source)
            {
                if (result.Count >= unitCount)
                {
                    break;
                }

                result.Add(Mathf.Max(0, slotIndex));
            }
        }

        while (result.Count < unitCount)
        {
            result.Add(result.Count);
        }

        return result;
    }

    private static IReadOnlyList<Vector2> BuildDeploymentNormalizedPositions(
        IEnumerable<Vector2> source,
        int unitCount,
        bool isPlayerTeam
    )
    {
        List<Vector2> result = new List<Vector2>(Mathf.Max(0, unitCount));
        if (source != null)
        {
            foreach (Vector2 normalizedPosition in source)
            {
                if (result.Count >= unitCount)
                {
                    break;
                }

                result.Add(BattleDeploymentPositionUtility.ClampToDeploymentHalf(normalizedPosition, isPlayerTeam));
            }
        }

        while (result.Count < unitCount)
        {
            result.Add(BattleDeploymentPositionUtility.BuildDefaultPosition(result.Count, unitCount, isPlayerTeam));
        }

        return result;
    }
}
