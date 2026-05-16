using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public readonly struct TrainingBattlePayloadSettings
{
    public readonly bool UseCurriculumTeamSize;
    public readonly string TeamSizeEnvironmentParameter;
    public readonly int DefaultTeamSize;
    public readonly TrainingGladiatorPreset[] RandomPresetPool;
    public readonly string AllyStatMultiplierEnvironmentParameter;
    public readonly string EnemyStatMultiplierEnvironmentParameter;
    public readonly float DefaultAllyStatMultiplier;
    public readonly float DefaultEnemyStatMultiplier;

    public TrainingBattlePayloadSettings(
        bool useCurriculumTeamSize,
        string teamSizeEnvironmentParameter,
        int defaultTeamSize,
        TrainingGladiatorPreset[] randomPresetPool,
        string allyStatMultiplierEnvironmentParameter,
        string enemyStatMultiplierEnvironmentParameter,
        float defaultAllyStatMultiplier,
        float defaultEnemyStatMultiplier
    )
    {
        UseCurriculumTeamSize = useCurriculumTeamSize;
        TeamSizeEnvironmentParameter = teamSizeEnvironmentParameter;
        DefaultTeamSize = defaultTeamSize;
        RandomPresetPool = randomPresetPool;
        AllyStatMultiplierEnvironmentParameter = allyStatMultiplierEnvironmentParameter;
        EnemyStatMultiplierEnvironmentParameter = enemyStatMultiplierEnvironmentParameter;
        DefaultAllyStatMultiplier = defaultAllyStatMultiplier;
        DefaultEnemyStatMultiplier = defaultEnemyStatMultiplier;
    }
}

public sealed class TrainingBattlePayloadFactory
{
    private readonly Object _logContext;

    public TrainingBattlePayloadFactory(Object logContext)
    {
        _logContext = logContext;
    }

    public BattleStartPayload Create(TrainingBattlePayloadSettings settings)
    {
        List<TrainingGladiatorPreset> validPresetPool = BuildValidPresetPool(settings.RandomPresetPool);
        if (validPresetPool.Count == 0)
        {
            Debug.LogError(
                "[TrainingBattlePayloadFactory] Cannot create payload because RandomPresetPool has no valid presets.",
                _logContext
            );
            return null;
        }

        var allySnapshots = new List<BattleUnitSnapshot>();
        var enemySnapshots = new List<BattleUnitSnapshot>();
        int teamSize = ResolveTeamSize(settings);
        float allyStatMultiplier = ResolveTeamStatMultiplier(
            settings.AllyStatMultiplierEnvironmentParameter,
            settings.DefaultAllyStatMultiplier
        );
        float enemyStatMultiplier = ResolveTeamStatMultiplier(
            settings.EnemyStatMultiplierEnvironmentParameter,
            settings.DefaultEnemyStatMultiplier
        );

        for (int i = 0; i < teamSize; i++)
        {
            TrainingGladiatorPreset preset = PickRandomPreset(validPresetPool);
            BattleUnitSnapshot snapshot = CreateSnapshot(i + 1, BattleTeamIds.Player, "Ally", preset, allyStatMultiplier);
            if (snapshot != null)
            {
                allySnapshots.Add(snapshot);
            }
        }

        for (int i = 0; i < teamSize; i++)
        {
            TrainingGladiatorPreset preset = PickRandomPreset(validPresetPool);
            BattleUnitSnapshot snapshot = CreateSnapshot(i + 1, BattleTeamIds.Enemy, "Enemy", preset, enemyStatMultiplier);
            if (snapshot != null)
            {
                enemySnapshots.Add(snapshot);
            }
        }

        if (allySnapshots.Count == 0 || enemySnapshots.Count == 0)
        {
            Debug.LogError(
                $"[TrainingBattlePayloadFactory] Cannot create payload. AllyCount={allySnapshots.Count}, EnemyCount={enemySnapshots.Count}.",
                _logContext
            );
            return null;
        }

        int battleSeed = Random.Range(1, 1000000);
        Dictionary<BattleTeamId, IReadOnlyList<int>> teamSlotIndicesById = CreateRandomizedTeamSlotIndices(
            allySnapshots.Count,
            enemySnapshots.Count
        );

        BattleTeamEntry playerTeam = new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, allySnapshots);
        BattleTeamEntry hostileTeam = new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots);

        return new BattleStartPayload(
            new[] { playerTeam, hostileTeam },
            BattleTeamIds.Player,
            selectedEncounterIndex: 0,
            enemyAverageLevel: CalculateAverageLevel(enemySnapshots),
            previewRewardGold: 0,
            battleSeed: battleSeed,
            teamSlotIndicesById: teamSlotIndicesById
        );
    }

    private List<TrainingGladiatorPreset> BuildValidPresetPool(TrainingGladiatorPreset[] source)
    {
        var validPresets = new List<TrainingGladiatorPreset>();
        if (source == null || source.Length == 0)
        {
            return validPresets;
        }

        for (int i = 0; i < source.Length; i++)
        {
            TrainingGladiatorPreset preset = source[i];
            if (preset == null)
            {
                continue;
            }

            string validationError = preset.GetValidationError();
            if (validationError != null)
            {
                Debug.LogError(
                    $"[TrainingBattlePayloadFactory] Ignoring invalid TrainingGladiatorPreset at index {i}. "
                        + $"Name={preset.name}, Reason={validationError}",
                    _logContext
                );
                continue;
            }

            validPresets.Add(preset);
        }

        return validPresets;
    }

    private static int ResolveTeamSize(TrainingBattlePayloadSettings settings)
    {
        float requestedTeamSize = settings.DefaultTeamSize;
        if (settings.UseCurriculumTeamSize && !string.IsNullOrWhiteSpace(settings.TeamSizeEnvironmentParameter))
        {
            requestedTeamSize = Academy.Instance.EnvironmentParameters.GetWithDefault(
                settings.TeamSizeEnvironmentParameter,
                settings.DefaultTeamSize
            );
        }

        return Mathf.Clamp(Mathf.RoundToInt(requestedTeamSize), 1, BattleTeamConstants.MaxUnitsPerTeam);
    }

    private static float ResolveTeamStatMultiplier(string environmentParameter, float fallback)
    {
        float requestedMultiplier = Mathf.Max(0f, fallback);
        if (!string.IsNullOrWhiteSpace(environmentParameter))
        {
            requestedMultiplier = Academy.Instance.EnvironmentParameters.GetWithDefault(
                environmentParameter,
                requestedMultiplier
            );
        }

        return Mathf.Max(0f, requestedMultiplier);
    }

    private static TrainingGladiatorPreset PickRandomPreset(IReadOnlyList<TrainingGladiatorPreset> presets)
    {
        return presets[Random.Range(0, presets.Count)];
    }

    private static Dictionary<BattleTeamId, IReadOnlyList<int>> CreateRandomizedTeamSlotIndices(
        int allyUnitCount,
        int enemyUnitCount
    )
    {
        return new Dictionary<BattleTeamId, IReadOnlyList<int>>
        {
            { BattleTeamIds.Player, CreateRandomSlotIndices(allyUnitCount) },
            { BattleTeamIds.Enemy, CreateRandomSlotIndices(enemyUnitCount) },
        };
    }

    private static int[] CreateRandomSlotIndices(int unitCount)
    {
        int clampedUnitCount = Mathf.Clamp(unitCount, 0, BattleTeamConstants.MaxUnitsPerTeam);
        int[] allSlots = new int[BattleTeamConstants.MaxUnitsPerTeam];
        for (int i = 0; i < allSlots.Length; i++)
        {
            allSlots[i] = i;
        }

        for (int i = allSlots.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (allSlots[i], allSlots[j]) = (allSlots[j], allSlots[i]);
        }

        var slotIndices = new int[clampedUnitCount];
        for (int i = 0; i < clampedUnitCount; i++)
        {
            slotIndices[i] = allSlots[i];
        }

        return slotIndices;
    }

    private BattleUnitSnapshot CreateSnapshot(
        int sourceRuntimeId,
        BattleTeamId teamId,
        string displayPrefix,
        TrainingGladiatorPreset preset,
        float teamStatMultiplier
    )
    {
        if (preset == null || !preset.IsValid)
        {
            Debug.LogError("[TrainingBattlePayloadFactory] Cannot create snapshot from an invalid preset.", _logContext);
            return null;
        }

        float mult = Mathf.Max(0f, teamStatMultiplier);
        WeaponSO weapon = preset.weapon;
        string presetName = string.IsNullOrWhiteSpace(preset.displayNamePrefix)
            ? preset.gladiatorClass.className
            : preset.displayNamePrefix;

        return new BattleUnitSnapshot(
            sourceRuntimeId: sourceRuntimeId,
            teamId: teamId,
            displayName: $"{displayPrefix} {presetName}",
            level: preset.level,
            loyalty: 100,
            maxHealth: preset.maxHealth * mult,
            currentHealth: preset.maxHealth * mult,
            attack: preset.attack * mult,
            attackSpeed: preset.attackSpeed * mult,
            moveSpeed: preset.moveSpeed * mult,
            attackRange: preset.attackRange * mult,
            gladiatorClass: preset.gladiatorClass,
            trait: null,
            personality: null,
            equippedArtifact: null,
            weaponType: weapon.weaponType,
            leftWeaponPrefab: weapon.leftWeaponPrefab,
            rightWeaponPrefab: weapon.rightWeaponPrefab,
            // TrainingScene은 이동/기본공격만 학습하므로 payload 단계에서 스킬을 제거한다.
            weaponSkillId: WeaponSkillId.None,
            customizeIndicates: preset.CloneCustomizeIndicates(),
            isRanged: preset.ResolveIsRanged(),
            useProjectile: preset.ResolveUseProjectile(),
            portraitSprite: preset.gladiatorClass.icon,
            defaultDur: weapon.defaultDur,
            duration: weapon.duration
        );
    }

    private static float CalculateAverageLevel(IReadOnlyList<BattleUnitSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            return 0f;
        }

        float totalLevel = 0f;
        for (int i = 0; i < snapshots.Count; i++)
        {
            totalLevel += snapshots[i] != null ? snapshots[i].Level : 0f;
        }

        return totalLevel / snapshots.Count;
    }
}
