using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public static class SaveGameService
{
    private const int MinSlotIndex = 1;
    private const int MaxSlotIndex = 5;
    private const string PlayerPrefsSlotJsonKeyPrefix = "SaveGameService.SlotJson.";

    private static SaveSlotData _pendingLoadedData;

    public static bool HasPendingLoadedData => _pendingLoadedData != null;

    public static void SaveToSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveGameService] Invalid slot index: {slotIndex}");
            return;
        }

        SaveSlotData data = BuildCurrentSnapshot(slotIndex);
        string json = JsonUtility.ToJson(data, true);

        string path = GetSlotFilePath(slotIndex);

        try
        {
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGameService] Failed to write save file. Path={path}, Error={ex.Message}");
        }

        // 파일 저장 실패 상황 대비 백업으로 PlayerPrefs에도 동일 JSON을 유지한다.
        PlayerPrefs.SetString(GetPlayerPrefsSlotKey(slotIndex), json);
        PlayerPrefs.Save();
    }

    public static bool TryLoadSlot(int slotIndex, out SaveSlotData data)
    {
        data = null;

        if (!IsValidSlotIndex(slotIndex))
        {
            return false;
        }

        string path = GetSlotFilePath(slotIndex);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                if (TryDeserialize(json, out data))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameService] Failed to read save file. Path={path}, Error={ex.Message}");
            }
        }

        string prefsKey = GetPlayerPrefsSlotKey(slotIndex);
        if (!PlayerPrefs.HasKey(prefsKey))
        {
            return false;
        }

        string backupJson = PlayerPrefs.GetString(prefsKey, string.Empty);
        return TryDeserialize(backupJson, out data);
    }

    public static SaveSlotPreview GetSlotPreview(int slotIndex)
    {
        SaveSlotPreview preview = SaveSlotPreview.CreateEmpty(slotIndex);

        if (!IsValidSlotIndex(slotIndex))
        {
            return preview;
        }

        if (!TryLoadSlot(slotIndex, out SaveSlotData data) || data == null)
        {
            return preview;
        }

        preview.hasData = true;
        preview.day = data.day;
        preview.gold = data.gold;
        preview.savedAtUtc = data.savedAtUtc;

        return preview;
    }

    public static void DeleteSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return;
        }

        string path = GetSlotFilePath(slotIndex);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameService] Failed to delete save file. Path={path}, Error={ex.Message}");
            }
        }

        string prefsKey = GetPlayerPrefsSlotKey(slotIndex);
        if (PlayerPrefs.HasKey(prefsKey))
        {
            PlayerPrefs.DeleteKey(prefsKey);
            PlayerPrefs.Save();
        }
    }

    public static void SetPendingLoadedData(SaveSlotData data)
    {
        if (data == null)
        {
            _pendingLoadedData = null;
            return;
        }

        _pendingLoadedData = CloneData(data);
    }

    public static bool TryConsumePendingLoadedData(out SaveSlotData data)
    {
        if (_pendingLoadedData == null)
        {
            data = null;
            return false;
        }

        data = _pendingLoadedData;
        _pendingLoadedData = null;
        return true;
    }

    private static SaveSlotData BuildCurrentSnapshot(int slotIndex)
    {
        SessionManager sessionManager = SessionManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        RandomManager randomManager = RandomManager.Instance;
        InventoryManager inventoryManager = InventoryManager.Instance;
        GladiatorManager gladiatorManager = GladiatorManager.Instance;
        ResearchManager researchManager = UnityEngine.Object.FindFirstObjectByType<ResearchManager>();
        MarketManager marketManager = MarketManager.Instance;
        BattleManager battleManager = UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        SaveOwnedWeaponData[] ownedWeapons = BuildOwnedWeaponsSnapshot(inventoryManager);
        SaveOwnedGladiatorData[] ownedGladiators = BuildOwnedGladiatorsSnapshot(gladiatorManager);
        string[] unlockedArtifactNames = BuildUnlockedArtifactNamesSnapshot(researchManager);

        SaveMarketWeaponOfferData[] marketWeaponOffers = BuildMarketWeaponOffersSnapshot(marketManager);
        SaveMarketGladiatorOfferData[] marketGladiatorOffers = BuildMarketGladiatorOffersSnapshot(marketManager);
        SaveMarketArtifactOfferData[] marketArtifactOffers = BuildMarketArtifactOffersSnapshot(marketManager);

        SaveBattleEncounterData[] battleEncounters = BuildBattleEncountersSnapshot(battleManager);

        SaveSlotData data = new()
        {
            slotIndex = slotIndex,
            day = sessionManager != null ? sessionManager.CurrentDay : 1,
            gold = resourceManager != null ? resourceManager.CurrentGold : 0,
            sessionSeed = randomManager != null ? randomManager.SessionSeed : 0,
            savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            hasUsedBattleToday = sessionManager != null && sessionManager.HasUsedBattleToday,
            pendingBattleRewardAmount = sessionManager != null ? sessionManager.PendingBattleRewardAmount : 0,
            classCounters =
                sessionManager != null
                    ? sessionManager.GetClassCounterEntriesForSave()
                    : Array.Empty<SaveClassCounterEntry>(),
            ownedWeapons = ownedWeapons,
            ownedGladiators = ownedGladiators,
            unlockedArtifactNames = unlockedArtifactNames,
            marketInitializedDay = marketManager != null ? marketManager.InitializedDay : 1,
            marketWeaponOffers = marketWeaponOffers,
            marketGladiatorOffers = marketGladiatorOffers,
            marketArtifactOffers = marketArtifactOffers,
            battleEncounterGeneratedDay = battleManager != null ? battleManager.EncounterGeneratedDay : 1,
            selectedEncounterIndex = battleManager != null ? battleManager.SelectedEncounterIndex : -1,
            battleEncounters = battleEncounters,
        };

        return data;
    }

    private static bool TryDeserialize(string json, out SaveSlotData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<SaveSlotData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGameService] Failed to deserialize save json. Error={ex.Message}");
            return false;
        }
    }

    private static SaveSlotData CloneData(SaveSlotData source)
    {
        if (source == null)
        {
            return null;
        }

        string json = JsonUtility.ToJson(source);
        return JsonUtility.FromJson<SaveSlotData>(json);
    }

    public static bool ApplyLoadedDataToRuntime(SaveSlotData data)
    {
        if (data == null)
        {
            return false;
        }

        SessionManager sessionManager = SessionManager.Instance;
        ResourceManager resourceManager = ResourceManager.Instance;
        RandomManager randomManager = RandomManager.Instance;
        ContentDatabaseProvider contentDatabaseProvider = ContentDatabaseProvider.Instance;
        InventoryManager inventoryManager = InventoryManager.Instance;
        GladiatorManager gladiatorManager = GladiatorManager.Instance;
        ResearchManager researchManager = UnityEngine.Object.FindFirstObjectByType<ResearchManager>();
        MarketManager marketManager = MarketManager.Instance;
        BattleManager battleManager = UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        if (sessionManager != null)
        {
            sessionManager.SetCurrentDayForLoad(data.day);
            sessionManager.SetBattleStateForLoad(data.hasUsedBattleToday, data.pendingBattleRewardAmount);
            sessionManager.SetClassCounterEntriesForLoad(data.classCounters);
        }

        if (randomManager != null)
        {
            randomManager.InitializeForNewSession(data.sessionSeed);
        }

        if (resourceManager != null)
        {
            resourceManager.SetCurrentGoldForLoad(data.gold);
        }

        if (contentDatabaseProvider != null)
        {
            Dictionary<int, OwnedWeaponData> weaponByRuntimeId = new Dictionary<int, OwnedWeaponData>();
            int nextWeaponRuntimeId;
            List<OwnedWeaponData> ownedWeapons = BuildOwnedWeaponsFromSave(
                data.ownedWeapons,
                contentDatabaseProvider,
                weaponByRuntimeId,
                out nextWeaponRuntimeId
            );

            if (inventoryManager != null)
            {
                inventoryManager.RestoreOwnedWeaponsForLoad(ownedWeapons, nextWeaponRuntimeId);
            }

            int nextGladiatorRuntimeId;
            List<OwnedGladiatorData> ownedGladiators = BuildOwnedGladiatorsFromSave(
                data.ownedGladiators,
                contentDatabaseProvider,
                weaponByRuntimeId,
                out nextGladiatorRuntimeId
            );

            if (gladiatorManager != null)
            {
                gladiatorManager.RestoreOwnedGladiatorsForLoad(ownedGladiators, nextGladiatorRuntimeId);
            }

            if (researchManager != null)
            {
                List<ArtifactSO> unlockedArtifacts = BuildUnlockedArtifactsFromSave(
                    data.unlockedArtifactNames,
                    contentDatabaseProvider
                );
                researchManager.RestoreUnlockedArtifactsForLoad(unlockedArtifacts);
            }

            if (marketManager != null)
            {
                List<MarketWeaponOffer> marketWeaponOffers = BuildMarketWeaponOffersFromSave(
                    data.marketWeaponOffers,
                    contentDatabaseProvider
                );
                List<MarketGladiatorOffer> marketGladiatorOffers = BuildMarketGladiatorOffersFromSave(
                    data.marketGladiatorOffers,
                    contentDatabaseProvider
                );
                List<MarketArtifactOffer> marketArtifactOffers = BuildMarketArtifactOffersFromSave(
                    data.marketArtifactOffers,
                    contentDatabaseProvider
                );

                if (marketWeaponOffers.Count > 0 || marketGladiatorOffers.Count > 0 || marketArtifactOffers.Count > 0)
                {
                    marketManager.RestoreOffersForLoad(
                        data.marketInitializedDay,
                        marketGladiatorOffers,
                        marketWeaponOffers,
                        marketArtifactOffers
                    );
                }
                else if (sessionManager != null)
                {
                    marketManager.InitializeDay(sessionManager.CurrentDay);
                }
            }

            if (battleManager != null)
            {
                List<BattleEncounterPreview> encounters = BuildBattleEncountersFromSave(
                    data.battleEncounters,
                    contentDatabaseProvider
                );

                if (encounters.Count > 0)
                {
                    battleManager.RestoreEncountersForLoad(
                        data.battleEncounterGeneratedDay,
                        data.selectedEncounterIndex,
                        encounters
                    );
                }
                else if (sessionManager != null)
                {
                    battleManager.InitializeDay(sessionManager.CurrentDay);
                }
            }
        }

        return true;
    }

    private static SaveOwnedWeaponData[] BuildOwnedWeaponsSnapshot(InventoryManager inventoryManager)
    {
        if (
            inventoryManager == null
            || inventoryManager.OwnedWeapons == null
            || inventoryManager.OwnedWeapons.Count == 0
        )
        {
            return Array.Empty<SaveOwnedWeaponData>();
        }

        SaveOwnedWeaponData[] result = new SaveOwnedWeaponData[inventoryManager.OwnedWeapons.Count];

        for (int i = 0; i < inventoryManager.OwnedWeapons.Count; i++)
        {
            OwnedWeaponData weapon = inventoryManager.OwnedWeapons[i];
            if (weapon == null)
            {
                continue;
            }

            result[i] = ToSaveOwnedWeaponData(weapon);
        }

        return result;
    }

    private static SaveOwnedGladiatorData[] BuildOwnedGladiatorsSnapshot(GladiatorManager gladiatorManager)
    {
        if (
            gladiatorManager == null
            || gladiatorManager.OwnedGladiators == null
            || gladiatorManager.OwnedGladiators.Count == 0
        )
        {
            return Array.Empty<SaveOwnedGladiatorData>();
        }

        SaveOwnedGladiatorData[] result = new SaveOwnedGladiatorData[gladiatorManager.OwnedGladiators.Count];

        for (int i = 0; i < gladiatorManager.OwnedGladiators.Count; i++)
        {
            OwnedGladiatorData gladiator = gladiatorManager.OwnedGladiators[i];
            if (gladiator == null)
            {
                continue;
            }

            result[i] = ToSaveOwnedGladiatorData(gladiator);
        }

        return result;
    }

    private static string[] BuildUnlockedArtifactNamesSnapshot(ResearchManager researchManager)
    {
        if (
            researchManager == null
            || researchManager.UnlockedArtifacts == null
            || researchManager.UnlockedArtifacts.Count == 0
        )
        {
            return Array.Empty<string>();
        }

        List<string> names = new List<string>(researchManager.UnlockedArtifacts.Count);
        for (int i = 0; i < researchManager.UnlockedArtifacts.Count; i++)
        {
            ArtifactSO artifact = researchManager.UnlockedArtifacts[i];
            if (artifact == null || string.IsNullOrWhiteSpace(artifact.artifactName))
            {
                continue;
            }

            names.Add(artifact.artifactName);
        }

        return names.ToArray();
    }

    private static SaveMarketWeaponOfferData[] BuildMarketWeaponOffersSnapshot(MarketManager marketManager)
    {
        if (marketManager == null || marketManager.WeaponOffers == null || marketManager.WeaponOffers.Count == 0)
        {
            return Array.Empty<SaveMarketWeaponOfferData>();
        }

        SaveMarketWeaponOfferData[] result = new SaveMarketWeaponOfferData[marketManager.WeaponOffers.Count];

        for (int i = 0; i < marketManager.WeaponOffers.Count; i++)
        {
            MarketWeaponOffer offer = marketManager.WeaponOffers[i];
            if (offer == null)
            {
                continue;
            }

            result[i] = new SaveMarketWeaponOfferData
            {
                slotIndex = offer.SlotIndex,
                price = offer.Price,
                isSold = offer.IsSold,
                weapon = offer.Weapon != null ? ToSaveOwnedWeaponData(offer.Weapon) : null,
            };
        }

        return result;
    }

    private static SaveMarketGladiatorOfferData[] BuildMarketGladiatorOffersSnapshot(MarketManager marketManager)
    {
        if (marketManager == null || marketManager.GladiatorOffers == null || marketManager.GladiatorOffers.Count == 0)
        {
            return Array.Empty<SaveMarketGladiatorOfferData>();
        }

        SaveMarketGladiatorOfferData[] result = new SaveMarketGladiatorOfferData[marketManager.GladiatorOffers.Count];

        for (int i = 0; i < marketManager.GladiatorOffers.Count; i++)
        {
            MarketGladiatorOffer offer = marketManager.GladiatorOffers[i];
            if (offer == null)
            {
                continue;
            }

            result[i] = new SaveMarketGladiatorOfferData
            {
                slotIndex = offer.SlotIndex,
                price = offer.Price,
                isSold = offer.IsSold,
                gladiator = offer.Gladiator != null ? ToSaveOwnedGladiatorData(offer.Gladiator) : null,
            };
        }

        return result;
    }

    private static SaveMarketArtifactOfferData[] BuildMarketArtifactOffersSnapshot(MarketManager marketManager)
    {
        if (marketManager == null || marketManager.ArtifactOffers == null || marketManager.ArtifactOffers.Count == 0)
        {
            return Array.Empty<SaveMarketArtifactOfferData>();
        }

        SaveMarketArtifactOfferData[] result = new SaveMarketArtifactOfferData[marketManager.ArtifactOffers.Count];

        for (int i = 0; i < marketManager.ArtifactOffers.Count; i++)
        {
            MarketArtifactOffer offer = marketManager.ArtifactOffers[i];
            if (offer == null)
            {
                continue;
            }

            result[i] = new SaveMarketArtifactOfferData
            {
                slotIndex = offer.SlotIndex,
                price = offer.Price,
                isSold = offer.IsSold,
                artifactName = offer.Artifact != null ? offer.Artifact.artifactName : string.Empty,
            };
        }

        return result;
    }

    private static SaveBattleEncounterData[] BuildBattleEncountersSnapshot(BattleManager battleManager)
    {
        if (battleManager == null || battleManager.DailyEncounters == null || battleManager.DailyEncounters.Count == 0)
        {
            return Array.Empty<SaveBattleEncounterData>();
        }

        SaveBattleEncounterData[] result = new SaveBattleEncounterData[battleManager.DailyEncounters.Count];

        for (int i = 0; i < battleManager.DailyEncounters.Count; i++)
        {
            BattleEncounterPreview preview = battleManager.DailyEncounters[i];
            if (preview == null)
            {
                continue;
            }

            SaveBattleEncounterUnitData[] units = Array.Empty<SaveBattleEncounterUnitData>();
            if (preview.EnemyUnits != null && preview.EnemyUnits.Count > 0)
            {
                units = new SaveBattleEncounterUnitData[preview.EnemyUnits.Count];
                for (int j = 0; j < preview.EnemyUnits.Count; j++)
                {
                    BattleUnitSnapshot unit = preview.EnemyUnits[j];
                    if (unit == null)
                    {
                        continue;
                    }

                    units[j] = new SaveBattleEncounterUnitData
                    {
                        sourceRuntimeId = unit.SourceRuntimeId,
                        displayName = unit.DisplayName,
                        level = unit.Level,
                        loyalty = unit.Loyalty,
                        maxHealth = unit.MaxHealth,
                        currentHealth = unit.CurrentHealth,
                        attack = unit.Attack,
                        attackSpeed = unit.AttackSpeed,
                        moveSpeed = unit.MoveSpeed,
                        attackRange = unit.AttackRange,
                        gladiatorClassName = unit.GladiatorClass != null ? unit.GladiatorClass.className : string.Empty,
                        traitName = unit.Trait != null ? unit.Trait.traitName : string.Empty,
                        personalityName = unit.Personality != null ? unit.Personality.personalityName : string.Empty,
                        equippedPerkName =
                            unit.EquippedArtifact != null ? unit.EquippedArtifact.artifactName : string.Empty,
                        weaponType = (int)unit.WeaponType,
                        weaponSkillId = (int)unit.WeaponSkillId,
                        customizeIndicates = CloneIntArray(unit.CustomizeIndicates),
                        isRanged = unit.IsRanged,
                        useProjectile = unit.UseProjectile,
                    };
                }
            }

            result[i] = new SaveBattleEncounterData
            {
                encounterIndex = preview.EncounterIndex,
                difficulty = (int)preview.Difficulty,
                averageLevel = preview.AverageLevel,
                previewRewardGold = preview.PreviewRewardGold,
                enemyUnits = units,
            };
        }

        return result;
    }

    private static SaveOwnedWeaponData ToSaveOwnedWeaponData(OwnedWeaponData weapon)
    {
        return new SaveOwnedWeaponData
        {
            runtimeId = weapon.RuntimeId,
            displayName = weapon.DisplayName,
            level = weapon.Level,
            weaponName = weapon.Weapon != null ? weapon.Weapon.weaponName : string.Empty,
            weaponType = weapon.Weapon != null ? (int)weapon.Weapon.weaponType : (int)WeaponType.None,
            weaponSkillId = weapon.WeaponSkill != null ? (int)weapon.WeaponSkill.skillId : (int)WeaponSkillId.None,
            cachedAttackBonus = weapon.CachedAttackBonus,
            cachedHealthBonus = weapon.CachedHealthBonus,
            cachedAttackSpeedBonus = weapon.CachedAttackSpeedBonus,
            cachedMoveSpeedBonus = weapon.CachedMoveSpeedBonus,
            cachedAttackRangeBonus = weapon.CachedAttackRangeBonus,
            finalAttackBonusVariancePercent = weapon.FinalAttackBonusVariancePercent,
            finalHealthBonusVariancePercent = weapon.FinalHealthBonusVariancePercent,
        };
    }

    private static SaveOwnedGladiatorData ToSaveOwnedGladiatorData(OwnedGladiatorData gladiator)
    {
        return new SaveOwnedGladiatorData
        {
            runtimeId = gladiator.RuntimeId,
            displayName = gladiator.DisplayName,
            level = gladiator.Level,
            exp = gladiator.Exp,
            loyalty = gladiator.Loyalty,
            upkeep = gladiator.Upkeep,
            gladiatorClassName = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.className : string.Empty,
            traitName = gladiator.Trait != null ? gladiator.Trait.traitName : string.Empty,
            personalityName = gladiator.Personality != null ? gladiator.Personality.personalityName : string.Empty,
            equippedPerkName =
                gladiator.EquippedArtifact != null ? gladiator.EquippedArtifact.artifactName : string.Empty,
            equippedWeaponRuntimeId = gladiator.EquippedWeapon != null ? gladiator.EquippedWeapon.RuntimeId : 0,
            cachedMaxHealth = gladiator.CachedMaxHealth,
            currentHealth = gladiator.CurrentHealth,
            cachedAttack = gladiator.CachedAttack,
            cachedAttackSpeed = gladiator.CachedAttackSpeed,
            cachedMoveSpeed = gladiator.CachedMoveSpeed,
            cachedAttackRange = gladiator.CachedAttackRange,
            finalHealthVariancePercent = gladiator.FinalHealthVariancePercent,
            finalAttackVariancePercent = gladiator.FinalAttackVariancePercent,
            customizeIndicates = CloneIntArray(gladiator.CustomizeIndicates),
        };
    }

    private static List<OwnedWeaponData> BuildOwnedWeaponsFromSave(
        SaveOwnedWeaponData[] savedWeapons,
        ContentDatabaseProvider contentDatabaseProvider,
        Dictionary<int, OwnedWeaponData> weaponByRuntimeId,
        out int nextRuntimeId
    )
    {
        List<OwnedWeaponData> result = new List<OwnedWeaponData>();
        int maxRuntimeId = 0;

        if (savedWeapons != null)
        {
            for (int i = 0; i < savedWeapons.Length; i++)
            {
                SaveOwnedWeaponData savedWeapon = savedWeapons[i];
                OwnedWeaponData weapon = BuildOwnedWeaponFromSave(savedWeapon, contentDatabaseProvider);
                if (weapon == null)
                {
                    continue;
                }

                result.Add(weapon);

                if (weapon.RuntimeId > 0)
                {
                    weaponByRuntimeId[weapon.RuntimeId] = weapon;
                    if (weapon.RuntimeId > maxRuntimeId)
                    {
                        maxRuntimeId = weapon.RuntimeId;
                    }
                }
            }
        }

        nextRuntimeId = maxRuntimeId + 1;
        return result;
    }

    private static OwnedWeaponData BuildOwnedWeaponFromSave(
        SaveOwnedWeaponData savedWeapon,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        if (savedWeapon == null)
        {
            return null;
        }

        WeaponSO weaponSo = FindWeaponByNameOrType(
            contentDatabaseProvider,
            savedWeapon.weaponName,
            savedWeapon.weaponType
        );
        if (weaponSo == null)
        {
            return null;
        }

        int runtimeId = Mathf.Max(1, savedWeapon.runtimeId);
        OwnedWeaponData ownedWeapon = new OwnedWeaponData(
            runtimeId,
            savedWeapon.displayName,
            savedWeapon.level,
            weaponSo
        );

        ownedWeapon.WeaponSkill = FindWeaponSkillById(contentDatabaseProvider, savedWeapon.weaponSkillId);
        ownedWeapon.CachedAttackBonus = savedWeapon.cachedAttackBonus;
        ownedWeapon.CachedHealthBonus = savedWeapon.cachedHealthBonus;
        ownedWeapon.CachedAttackSpeedBonus = savedWeapon.cachedAttackSpeedBonus;
        ownedWeapon.CachedMoveSpeedBonus = savedWeapon.cachedMoveSpeedBonus;
        ownedWeapon.CachedAttackRangeBonus = savedWeapon.cachedAttackRangeBonus;
        ownedWeapon.FinalAttackBonusVariancePercent = savedWeapon.finalAttackBonusVariancePercent;
        ownedWeapon.FinalHealthBonusVariancePercent = savedWeapon.finalHealthBonusVariancePercent;

        return ownedWeapon;
    }

    private static List<OwnedGladiatorData> BuildOwnedGladiatorsFromSave(
        SaveOwnedGladiatorData[] savedGladiators,
        ContentDatabaseProvider contentDatabaseProvider,
        Dictionary<int, OwnedWeaponData> weaponByRuntimeId,
        out int nextRuntimeId
    )
    {
        List<OwnedGladiatorData> result = new List<OwnedGladiatorData>();
        int maxRuntimeId = 0;

        if (savedGladiators != null)
        {
            for (int i = 0; i < savedGladiators.Length; i++)
            {
                SaveOwnedGladiatorData savedGladiator = savedGladiators[i];
                OwnedGladiatorData gladiator = BuildOwnedGladiatorFromSave(
                    savedGladiator,
                    contentDatabaseProvider,
                    weaponByRuntimeId
                );

                if (gladiator == null)
                {
                    continue;
                }

                result.Add(gladiator);

                if (gladiator.RuntimeId > 0 && gladiator.RuntimeId > maxRuntimeId)
                {
                    maxRuntimeId = gladiator.RuntimeId;
                }
            }
        }

        nextRuntimeId = maxRuntimeId + 1;
        return result;
    }

    private static OwnedGladiatorData BuildOwnedGladiatorFromSave(
        SaveOwnedGladiatorData savedGladiator,
        ContentDatabaseProvider contentDatabaseProvider,
        Dictionary<int, OwnedWeaponData> weaponByRuntimeId
    )
    {
        if (savedGladiator == null)
        {
            return null;
        }

        GladiatorClassSO gladiatorClass = FindGladiatorClassByName(
            contentDatabaseProvider,
            savedGladiator.gladiatorClassName
        );
        if (gladiatorClass == null)
        {
            return null;
        }

        TraitSO trait = FindTraitByName(contentDatabaseProvider, savedGladiator.traitName);
        PersonalitySO personality = FindPersonalityByName(contentDatabaseProvider, savedGladiator.personalityName);
        ArtifactSO equippedArtifact = FindArtifactByName(contentDatabaseProvider, savedGladiator.equippedPerkName);

        OwnedWeaponData equippedWeapon = null;
        if (savedGladiator.equippedWeaponRuntimeId > 0)
        {
            weaponByRuntimeId.TryGetValue(savedGladiator.equippedWeaponRuntimeId, out equippedWeapon);
        }

        OwnedGladiatorData gladiator = new OwnedGladiatorData(
            Mathf.Max(1, savedGladiator.runtimeId),
            savedGladiator.displayName,
            savedGladiator.level,
            savedGladiator.exp,
            savedGladiator.loyalty,
            savedGladiator.upkeep,
            gladiatorClass,
            trait,
            personality,
            equippedArtifact,
            equippedWeapon,
            CloneIntArray(savedGladiator.customizeIndicates)
        );

        gladiator.CachedMaxHealth = Mathf.Max(0f, savedGladiator.cachedMaxHealth);
        gladiator.CurrentHealth = Mathf.Clamp(savedGladiator.currentHealth, 0f, gladiator.CachedMaxHealth);
        gladiator.CachedAttack = Mathf.Max(0f, savedGladiator.cachedAttack);
        gladiator.CachedAttackSpeed = Mathf.Max(0f, savedGladiator.cachedAttackSpeed);
        gladiator.CachedMoveSpeed = Mathf.Max(0f, savedGladiator.cachedMoveSpeed);
        gladiator.CachedAttackRange = Mathf.Max(0f, savedGladiator.cachedAttackRange);
        gladiator.FinalHealthVariancePercent = savedGladiator.finalHealthVariancePercent;
        gladiator.FinalAttackVariancePercent = savedGladiator.finalAttackVariancePercent;

        return gladiator;
    }

    private static List<ArtifactSO> BuildUnlockedArtifactsFromSave(
        string[] unlockedArtifactNames,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        List<ArtifactSO> result = new List<ArtifactSO>();

        if (unlockedArtifactNames == null || unlockedArtifactNames.Length == 0)
        {
            return result;
        }

        for (int i = 0; i < unlockedArtifactNames.Length; i++)
        {
            ArtifactSO artifact = FindArtifactByName(contentDatabaseProvider, unlockedArtifactNames[i]);
            if (artifact != null)
            {
                result.Add(artifact);
            }
        }

        return result;
    }

    private static List<MarketWeaponOffer> BuildMarketWeaponOffersFromSave(
        SaveMarketWeaponOfferData[] savedOffers,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        List<MarketWeaponOffer> result = new List<MarketWeaponOffer>();

        if (savedOffers == null)
        {
            return result;
        }

        for (int i = 0; i < savedOffers.Length; i++)
        {
            SaveMarketWeaponOfferData savedOffer = savedOffers[i];
            if (savedOffer == null)
            {
                continue;
            }

            OwnedWeaponData weapon = BuildOwnedWeaponFromSave(savedOffer.weapon, contentDatabaseProvider);
            MarketWeaponOffer offer = new MarketWeaponOffer(savedOffer.slotIndex, weapon, savedOffer.price);
            if (savedOffer.isSold)
            {
                offer.MarkSold();
            }

            result.Add(offer);
        }

        return result;
    }

    private static List<MarketGladiatorOffer> BuildMarketGladiatorOffersFromSave(
        SaveMarketGladiatorOfferData[] savedOffers,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        List<MarketGladiatorOffer> result = new List<MarketGladiatorOffer>();

        if (savedOffers == null)
        {
            return result;
        }

        for (int i = 0; i < savedOffers.Length; i++)
        {
            SaveMarketGladiatorOfferData savedOffer = savedOffers[i];
            if (savedOffer == null)
            {
                continue;
            }

            OwnedGladiatorData gladiator = BuildOwnedGladiatorFromSave(
                savedOffer.gladiator,
                contentDatabaseProvider,
                new Dictionary<int, OwnedWeaponData>()
            );

            MarketGladiatorOffer offer = new MarketGladiatorOffer(savedOffer.slotIndex, gladiator, savedOffer.price);
            if (savedOffer.isSold)
            {
                offer.MarkSold();
            }

            result.Add(offer);
        }

        return result;
    }

    private static List<MarketArtifactOffer> BuildMarketArtifactOffersFromSave(
        SaveMarketArtifactOfferData[] savedOffers,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        List<MarketArtifactOffer> result = new List<MarketArtifactOffer>();

        if (savedOffers == null)
        {
            return result;
        }

        for (int i = 0; i < savedOffers.Length; i++)
        {
            SaveMarketArtifactOfferData savedOffer = savedOffers[i];
            if (savedOffer == null)
            {
                continue;
            }

            ArtifactSO artifact = FindArtifactByName(contentDatabaseProvider, savedOffer.artifactName);
            MarketArtifactOffer offer = new MarketArtifactOffer(savedOffer.slotIndex, artifact, savedOffer.price);
            if (savedOffer.isSold)
            {
                offer.MarkSold();
            }

            result.Add(offer);
        }

        return result;
    }

    private static List<BattleEncounterPreview> BuildBattleEncountersFromSave(
        SaveBattleEncounterData[] savedEncounters,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        List<BattleEncounterPreview> result = new List<BattleEncounterPreview>();

        if (savedEncounters == null)
        {
            return result;
        }

        for (int i = 0; i < savedEncounters.Length; i++)
        {
            SaveBattleEncounterData savedEncounter = savedEncounters[i];
            if (savedEncounter == null)
            {
                continue;
            }

            List<BattleUnitSnapshot> units = new List<BattleUnitSnapshot>();
            if (savedEncounter.enemyUnits != null)
            {
                for (int j = 0; j < savedEncounter.enemyUnits.Length; j++)
                {
                    BattleUnitSnapshot unit = BuildBattleUnitSnapshotFromSave(
                        savedEncounter.enemyUnits[j],
                        contentDatabaseProvider
                    );
                    if (unit != null)
                    {
                        units.Add(unit);
                    }
                }
            }

            BattleEncounterPreview preview = new BattleEncounterPreview(
                savedEncounter.encounterIndex,
                units,
                savedEncounter.averageLevel,
                savedEncounter.previewRewardGold,
                (BattleEncounterDifficulty)savedEncounter.difficulty
            );

            result.Add(preview);
        }

        return result;
    }

    private static BattleUnitSnapshot BuildBattleUnitSnapshotFromSave(
        SaveBattleEncounterUnitData savedUnit,
        ContentDatabaseProvider contentDatabaseProvider
    )
    {
        if (savedUnit == null)
        {
            return null;
        }

        GladiatorClassSO gladiatorClass = FindGladiatorClassByName(
            contentDatabaseProvider,
            savedUnit.gladiatorClassName
        );
        TraitSO trait = FindTraitByName(contentDatabaseProvider, savedUnit.traitName);
        PersonalitySO personality = FindPersonalityByName(contentDatabaseProvider, savedUnit.personalityName);
        ArtifactSO equippedArtifact = FindArtifactByName(contentDatabaseProvider, savedUnit.equippedPerkName);

        WeaponType weaponType = Enum.IsDefined(typeof(WeaponType), savedUnit.weaponType)
            ? (WeaponType)savedUnit.weaponType
            : WeaponType.None;

        WeaponSkillId weaponSkillId = Enum.IsDefined(typeof(WeaponSkillId), savedUnit.weaponSkillId)
            ? (WeaponSkillId)savedUnit.weaponSkillId
            : WeaponSkillId.None;

        WeaponSO weapon = FindWeaponByNameOrType(contentDatabaseProvider, string.Empty, (int)weaponType);
        GameObject leftPrefab = weapon != null ? weapon.leftWeaponPrefab : null;
        GameObject rightPrefab = weapon != null ? weapon.rightWeaponPrefab : null;

        Sprite portrait = gladiatorClass != null ? gladiatorClass.icon : null;

        return new BattleUnitSnapshot(
            savedUnit.sourceRuntimeId,
            BattleTeamId.Enemy,
            savedUnit.displayName,
            savedUnit.level,
            savedUnit.loyalty,
            savedUnit.maxHealth,
            savedUnit.currentHealth,
            savedUnit.attack,
            savedUnit.attackSpeed,
            savedUnit.moveSpeed,
            savedUnit.attackRange,
            gladiatorClass,
            trait,
            personality,
            equippedArtifact,
            weaponType,
            leftPrefab,
            rightPrefab,
            weaponSkillId,
            CloneIntArray(savedUnit.customizeIndicates),
            savedUnit.isRanged,
            savedUnit.useProjectile,
            portrait
        );
    }

    private static GladiatorClassSO FindGladiatorClassByName(
        ContentDatabaseProvider contentDatabaseProvider,
        string className
    )
    {
        if (contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<GladiatorClassSO> classes = contentDatabaseProvider.GladiatorClasses;
        for (int i = 0; i < classes.Count; i++)
        {
            GladiatorClassSO value = classes[i];
            if (value != null && string.Equals(value.className, className, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return contentDatabaseProvider.GladiatorTemplate;
    }

    private static TraitSO FindTraitByName(ContentDatabaseProvider contentDatabaseProvider, string traitName)
    {
        if (contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<TraitSO> traits = contentDatabaseProvider.Traits;
        for (int i = 0; i < traits.Count; i++)
        {
            TraitSO value = traits[i];
            if (value != null && string.Equals(value.traitName, traitName, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }

    private static PersonalitySO FindPersonalityByName(
        ContentDatabaseProvider contentDatabaseProvider,
        string personalityName
    )
    {
        if (contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<PersonalitySO> personalities = contentDatabaseProvider.Personalities;
        for (int i = 0; i < personalities.Count; i++)
        {
            PersonalitySO value = personalities[i];
            if (value != null && string.Equals(value.personalityName, personalityName, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }

    private static ArtifactSO FindArtifactByName(ContentDatabaseProvider contentDatabaseProvider, string artifactName)
    {
        if (contentDatabaseProvider == null || string.IsNullOrWhiteSpace(artifactName))
        {
            return null;
        }

        IReadOnlyList<ArtifactSO> artifacts = contentDatabaseProvider.Artifacts;
        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactSO value = artifacts[i];
            if (value != null && string.Equals(value.artifactName, artifactName, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return null;
    }

    private static WeaponSkillSO FindWeaponSkillById(ContentDatabaseProvider contentDatabaseProvider, int weaponSkillId)
    {
        if (contentDatabaseProvider == null)
        {
            return null;
        }

        WeaponSkillId targetId = Enum.IsDefined(typeof(WeaponSkillId), weaponSkillId)
            ? (WeaponSkillId)weaponSkillId
            : WeaponSkillId.None;

        if (targetId == WeaponSkillId.None)
        {
            return null;
        }

        IReadOnlyList<WeaponSkillSO> skills = contentDatabaseProvider.WeaponSkills;
        for (int i = 0; i < skills.Count; i++)
        {
            WeaponSkillSO value = skills[i];
            if (value != null && value.skillId == targetId)
            {
                return value;
            }
        }

        return null;
    }

    private static WeaponSO FindWeaponByNameOrType(
        ContentDatabaseProvider contentDatabaseProvider,
        string weaponName,
        int weaponType
    )
    {
        if (contentDatabaseProvider == null)
        {
            return null;
        }

        IReadOnlyList<WeaponSO> weapons = contentDatabaseProvider.Weapons;
        WeaponType targetType = Enum.IsDefined(typeof(WeaponType), weaponType)
            ? (WeaponType)weaponType
            : WeaponType.None;

        WeaponSO fallbackByType = null;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSO value = weapons[i];
            if (value == null)
            {
                continue;
            }

            bool typeMatches = targetType == WeaponType.None || value.weaponType == targetType;
            if (fallbackByType == null && typeMatches)
            {
                fallbackByType = value;
            }

            if (
                !string.IsNullOrWhiteSpace(weaponName)
                && string.Equals(value.weaponName, weaponName, StringComparison.Ordinal)
            )
            {
                return value;
            }
        }

        return fallbackByType;
    }

    private static int[] CloneIntArray(int[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<int>();
        }

        int[] clone = new int[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    private static bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= MinSlotIndex && slotIndex <= MaxSlotIndex;
    }

    private static string GetSlotFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"slot{slotIndex}.json");
    }

    private static string GetPlayerPrefsSlotKey(int slotIndex)
    {
        return PlayerPrefsSlotJsonKeyPrefix + slotIndex;
    }

    [Serializable]
    public struct SaveSlotPreview
    {
        public int slotIndex;
        public bool hasData;
        public int day;
        public int gold;
        public string savedAtUtc;

        public static SaveSlotPreview CreateEmpty(int slotIndex)
        {
            SaveSlotPreview preview = new()
            {
                slotIndex = slotIndex,
                hasData = false,
                day = 0,
                gold = 0,
                savedAtUtc = string.Empty,
            };

            return preview;
        }
    }
}
