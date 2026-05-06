using System.Collections.Generic;
using UnityEngine;

public static class GladiatorLessonScenarioBuilder
{
    public static BattleStartPayload Build(GladiatorLessonMode lessonMode, BattleStartPayload payload)
    {
        if (payload == null)
        {
            return null;
        }

        BattleTeamEntry playerTeam = payload.GetPlayerTeam();
        BattleTeamEntry hostileTeam = payload.GetHostileTeam();
        var playerUnits = CloneUnits(playerTeam?.Units);
        var hostileUnits = CloneUnits(hostileTeam?.Units);

        switch (lessonMode)
        {
            case GladiatorLessonMode.FinishLowHp:
                ApplyLowHpFinishScenario(hostileUnits);
                break;
            case GladiatorLessonMode.PeelFocusedAlly:
                ApplyPeelScenario(playerUnits, hostileUnits);
                break;
            case GladiatorLessonMode.SplitPressure3v3:
                ApplySplitPressureScenario(playerUnits, hostileUnits);
                break;
        }

        return new BattleStartPayload(
            new[]
            {
                new BattleTeamEntry(playerTeam.TeamId, playerTeam.IsPlayerOwned, playerUnits),
                new BattleTeamEntry(hostileTeam.TeamId, hostileTeam.IsPlayerOwned, hostileUnits),
            },
            payload.PlayerTeamId,
            payload.SelectedEncounterIndex,
            payload.EnemyAverageLevel,
            payload.PreviewRewardGold,
            payload.BattleSeed
        );
    }

    private static List<BattleUnitSnapshot> CloneUnits(IReadOnlyList<BattleUnitSnapshot> source)
    {
        var units = new List<BattleUnitSnapshot>();
        if (source == null)
        {
            return units;
        }

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
            {
                units.Add(source[i].Clone());
            }
        }

        return units;
    }

    private static void ApplyLowHpFinishScenario(List<BattleUnitSnapshot> hostileUnits)
    {
        if (hostileUnits == null || hostileUnits.Count == 0)
        {
            return;
        }

        BattleUnitSnapshot target = hostileUnits[0];
        hostileUnits[0] = CloneWithHealthRatio(target, 0.35f);
    }

    private static void ApplyPeelScenario(List<BattleUnitSnapshot> playerUnits, List<BattleUnitSnapshot> hostileUnits)
    {
        if (playerUnits == null || playerUnits.Count == 0)
        {
            return;
        }

        int weakIndex = Mathf.Min(1, playerUnits.Count - 1);
        playerUnits[weakIndex] = CloneWithHealthRatio(playerUnits[weakIndex], 0.45f);
    }

    private static void ApplySplitPressureScenario(
        List<BattleUnitSnapshot> playerUnits,
        List<BattleUnitSnapshot> hostileUnits
    )
    {
        for (int i = 0; i < hostileUnits.Count; i++)
        {
            hostileUnits[i] = CloneWithHealthRatio(hostileUnits[i], i == 0 ? 0.5f : 1f);
        }
    }

    private static BattleUnitSnapshot CloneWithHealthRatio(BattleUnitSnapshot unit, float healthRatio)
    {
        if (unit == null)
        {
            return null;
        }

        float clampedRatio = Mathf.Clamp01(healthRatio);
        return new BattleUnitSnapshot(
            unit.SourceRuntimeId,
            unit.TeamId,
            unit.DisplayName,
            unit.Level,
            unit.Loyalty,
            unit.MaxHealth,
            unit.MaxHealth * clampedRatio,
            unit.Attack,
            unit.AttackSpeed,
            unit.MoveSpeed,
            unit.AttackRange,
            unit.GladiatorClass,
            unit.Trait,
            unit.Personality,
            unit.EquippedPerk,
            unit.WeaponType,
            unit.LeftWeaponPrefab,
            unit.RightWeaponPrefab,
            unit.WeaponSkillId,
            unit.CustomizeIndicates,
            unit.IsRanged,
            unit.UseProjectile,
            unit.PortraitSprite
        );
    }
}
