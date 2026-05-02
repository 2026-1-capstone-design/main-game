using System.Collections.Generic;
using BattleTest;
using Unity.MLAgents;
using UnityEngine;

public sealed class TrainingBattlePayloadFactory
{
    private readonly Object _logContext;

    public TrainingBattlePayloadFactory(Object logContext)
    {
        _logContext = logContext;
    }

    public BattleStartPayload Create(TrainingBattlePayloadSettings settings)
    {
        var allySnapshots = new List<BattleUnitSnapshot>();
        var enemySnapshots = new List<BattleUnitSnapshot>();
        int teamSize = ResolveTeamSize(settings);

        for (int i = 0; i < teamSize; i++)
        {
            if (!TryGetTrainingUnitConfig(settings.Preset?.allyTeam, i, settings, out BattleTestUnitConfig entry))
            {
                continue;
            }

            allySnapshots.Add(
                CreateSnapshot(i + 1, BattleTeamIds.Player, "Ally", entry, PickRandomClass(entry.classSO, settings))
            );
        }

        for (int i = 0; i < teamSize; i++)
        {
            if (!TryGetTrainingUnitConfig(settings.Preset?.enemyTeam, i, settings, out BattleTestUnitConfig entry))
            {
                continue;
            }

            enemySnapshots.Add(
                CreateSnapshot(i + 1, BattleTeamIds.Enemy, "Enemy", entry, PickRandomClass(entry.classSO, settings))
            );
        }

        ShuffleList(allySnapshots);
        ShuffleList(enemySnapshots);

        BattleTeamEntry playerTeam = new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, allySnapshots);
        BattleTeamEntry hostileTeam = new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots);

        return new BattleStartPayload(
            new[] { playerTeam, hostileTeam },
            BattleTeamIds.Player,
            selectedEncounterIndex: 0,
            enemyAverageLevel: settings.Preset != null ? settings.Preset.enemyAverageLevel : settings.DefaultUnitLevel,
            previewRewardGold: settings.Preset != null ? settings.Preset.previewRewardGold : 0,
            battleSeed: Random.Range(1, 1000000)
        );
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

    private bool TryGetTrainingUnitConfig(
        IReadOnlyList<BattleTestUnitConfig> teamConfig,
        int unitIndex,
        TrainingBattlePayloadSettings settings,
        out BattleTestUnitConfig entry
    )
    {
        entry = default;
        if (teamConfig == null || teamConfig.Count == 0)
        {
            entry = CreateDefaultTrainingUnitConfig(settings);
            if (!HasRandomClassPool(settings))
            {
                Debug.LogError(
                    "[TrainingBattlePayloadFactory] Random class pool is required when no training preset team config exists.",
                    _logContext
                );
                return false;
            }

            return true;
        }

        entry = teamConfig[unitIndex % teamConfig.Count];
        if (entry.classSO == null && !HasRandomClassPool(settings))
        {
            Debug.LogError(
                "[TrainingBattlePayloadFactory] Training unit requires either a preset class or random class pool entry.",
                _logContext
            );
            return false;
        }

        if (entry.level <= 0)
            entry.level = settings.DefaultUnitLevel;

        if (entry.statMultiplier <= 0f)
            entry.statMultiplier = settings.DefaultStatMultiplier;

        if (entry.weaponData == null)
            entry.weaponData = PickRandomWeapon(null, settings);

        return true;
    }

    private BattleTestUnitConfig CreateDefaultTrainingUnitConfig(TrainingBattlePayloadSettings settings)
    {
        return new BattleTestUnitConfig
        {
            level = settings.DefaultUnitLevel,
            weaponData = PickRandomWeapon(null, settings),
            statMultiplier = settings.DefaultStatMultiplier,
        };
    }

    private static GladiatorClassSO PickRandomClass(GladiatorClassSO fallback, TrainingBattlePayloadSettings settings)
    {
        if (!HasRandomClassPool(settings))
            return fallback;

        int startIndex = Random.Range(0, settings.RandomClassPool.Length);
        for (int offset = 0; offset < settings.RandomClassPool.Length; offset++)
        {
            GladiatorClassSO classSO = settings.RandomClassPool[
                (startIndex + offset) % settings.RandomClassPool.Length
            ];
            if (classSO != null)
                return classSO;
        }

        return fallback;
    }

    private static bool HasRandomClassPool(TrainingBattlePayloadSettings settings)
    {
        if (settings.RandomClassPool == null)
            return false;

        for (int i = 0; i < settings.RandomClassPool.Length; i++)
        {
            if (settings.RandomClassPool[i] != null)
                return true;
        }

        return false;
    }

    private static WeaponSO PickRandomWeapon(WeaponSO fallback, TrainingBattlePayloadSettings settings)
    {
        if (settings.RandomWeaponPool == null || settings.RandomWeaponPool.Length == 0)
            return fallback;

        int startIndex = Random.Range(0, settings.RandomWeaponPool.Length);
        for (int offset = 0; offset < settings.RandomWeaponPool.Length; offset++)
        {
            WeaponSO weaponSO = settings.RandomWeaponPool[(startIndex + offset) % settings.RandomWeaponPool.Length];
            if (weaponSO != null)
                return weaponSO;
        }

        return fallback;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private BattleUnitSnapshot CreateSnapshot(
        int sourceRuntimeId,
        BattleTeamId teamId,
        string displayPrefix,
        BattleTestUnitConfig entry,
        GladiatorClassSO classOverride
    )
    {
        GladiatorClassSO classSO = classOverride != null ? classOverride : entry.classSO;
        if (classSO == null)
        {
            Debug.LogError(
                "[TrainingBattlePayloadFactory] Cannot create snapshot without a gladiator class.",
                _logContext
            );
            return null;
        }

        int lv = Mathf.Max(1, entry.level);
        float mult = entry.statMultiplier <= 0 ? 1f : entry.statMultiplier;

        float baseHp = classSO.baseHealth + classSO.healthGrowthPerLevel * (lv - 1);
        float baseAtk = classSO.baseAttack + classSO.attackGrowthPerLevel * (lv - 1);

        float finalHp = (entry.healthOverride > 0 ? entry.healthOverride : baseHp) * mult;
        float finalAtk = (entry.attackOverride > 0 ? entry.attackOverride : baseAtk) * mult;
        float finalAtkSpeed = (entry.attackSpeedOverride > 0 ? entry.attackSpeedOverride : classSO.attackSpeed) * mult;
        float finalMove = (entry.moveSpeedOverride > 0 ? entry.moveSpeedOverride : classSO.moveSpeed) * mult;
        float finalRange = entry.attackRangeOverride > 0 ? entry.attackRangeOverride : classSO.attackRange;

        WeaponType resolvedType = WeaponType.None;
        GameObject leftPrefab = null;
        GameObject rightPrefab = null;
        bool isRanged = false;
        bool useProjectile = false;

        if (entry.weaponData != null)
        {
            resolvedType = entry.weaponData.weaponType;
            leftPrefab = entry.weaponData.leftWeaponPrefab;
            rightPrefab = entry.weaponData.rightWeaponPrefab;
            isRanged = entry.weaponData.isRanged;
            useProjectile = entry.weaponData.useProjectile;
        }

        if (entry.overrideWeaponSettings)
        {
            isRanged = entry.isRanged;
            useProjectile = entry.useProjectile;
        }

        int[] randomSkins =
            GladiatorSkinManager.Instance != null ? GladiatorSkinManager.Instance.GenerateRandomSkinIndicates() : null;

        return new BattleUnitSnapshot(
            sourceRuntimeId: sourceRuntimeId,
            teamId: teamId,
            displayName: $"{displayPrefix} {classSO.className}",
            level: lv,
            loyalty: 100,
            maxHealth: finalHp,
            currentHealth: finalHp,
            attack: finalAtk,
            attackSpeed: finalAtkSpeed,
            moveSpeed: finalMove,
            attackRange: finalRange,
            gladiatorClass: classSO,
            trait: null,
            personality: null,
            equippedPerk: null,
            weaponType: resolvedType,
            leftWeaponPrefab: leftPrefab,
            rightWeaponPrefab: rightPrefab,
            weaponSkillId: entry.weaponSkillId,
            customizeIndicates: randomSkins,
            isRanged: isRanged,
            useProjectile: useProjectile,
            portraitSprite: classSO.icon
        );
    }
}
