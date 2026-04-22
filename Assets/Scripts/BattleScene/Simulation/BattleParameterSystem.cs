using System.Collections.Generic;
using UnityEngine;

public readonly struct BattleParameterComputation
{
    public int UnitIndex { get; }
    public int UnitNumber { get; }
    public BattleParameterSet Raw { get; }
    public BattleParameterSet Modified { get; }
    public bool ModifierOverflowed { get; }

    public BattleParameterComputation(
        int unitIndex,
        int unitNumber,
        BattleParameterSet raw,
        BattleParameterSet modified,
        bool modifierOverflowed)
    {
        UnitIndex = unitIndex;
        UnitNumber = unitNumber;
        Raw = raw;
        Modified = modified;
        ModifierOverflowed = modifierOverflowed;
    }
}

public sealed class BattleParameterSystem
{
    public static BattleParameterRadii BuildRadii(BattleAITuningSO aiTuning)
    {
        if (aiTuning == null)
            return default;

        return new BattleParameterRadii
        {
            surroundRadius = aiTuning.surroundRadius,
            helpRadius = aiTuning.helpRadius,
            peelRadius = aiTuning.peelRadius,
            frontlineGapRadius = aiTuning.frontlineGapRadius,
            isolationRadius = aiTuning.isolationRadius,
            assassinReachRadius = aiTuning.assassinReachRadius,
            clusterRadius = aiTuning.clusterRadius,
            teamCenterDistanceRadius = aiTuning.teamCenterDistanceRadius
        };
    }

    public BattleParameterComputation[] Compute(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        BattleAITuningSO aiTuning)
    {
        if (units == null)
            return new BattleParameterComputation[0];

        var allViews = new List<BattleUnitView>(units.Count);
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            allViews.Add(BattleUnitView.From(unit.State));
        }

        var results = new List<BattleParameterComputation>(allViews.Count);
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleUnitView self = BattleUnitView.From(unit.State);
            var allies = new List<BattleUnitView>();
            var enemies = new List<BattleUnitView>();

            for (int j = 0; j < allViews.Count; j++)
            {
                BattleUnitView view = allViews[j];
                if (view.UnitNumber == self.UnitNumber)
                    continue;

                if (view.IsEnemy == self.IsEnemy)
                    allies.Add(view);
                else
                    enemies.Add(view);
            }

            BattleParameterSet raw = BattleParameterComputer.Compute(self, allies, enemies, radii);
            BattleParameterSet modified = ApplyCurrentActionParameterModifiers(
                unit,
                raw,
                aiTuning,
                out bool overflowed);

            raw.Clamp01All();
            modified.Clamp01All();

            unit.State.SetCurrentParameters(raw, modified);

            results.Add(new BattleParameterComputation(
                i,
                unit.UnitNumber,
                raw,
                modified,
                overflowed));
        }

        return results.ToArray();
    }

    private static BattleParameterSet ApplyCurrentActionParameterModifiers(
        BattleRuntimeUnit unit,
        BattleParameterSet rawParameters,
        BattleAITuningSO aiTuning,
        out bool overflowed)
    {
        overflowed = false;
        if (unit == null)
            return rawParameters;

        if (unit.CurrentActionType == BattleActionType.None || aiTuning == null)
            return rawParameters;

        BattleActionTuning tuning = aiTuning.GetActionTuning(unit.CurrentActionType);
        if (tuning == null)
            return rawParameters;

        BattleParameterWeights percents = tuning.currentActionParameterPercents;
        return ApplyPercentModifiersWithoutClamp(rawParameters, percents, out overflowed);
    }

    private static BattleParameterSet ApplyPercentModifiersWithoutClamp(
        BattleParameterSet parameters,
        BattleParameterWeights percents,
        out bool overflowed)
    {
        overflowed = false;

        parameters.SelfHpLow = ApplyPercent(parameters.SelfHpLow, percents.selfHpLow, ref overflowed);
        parameters.SelfSurroundedByEnemies = ApplyPercent(parameters.SelfSurroundedByEnemies, percents.selfSurroundedByEnemies, ref overflowed);
        parameters.LowHealthAllyProximity = ApplyPercent(parameters.LowHealthAllyProximity, percents.lowHealthAllyProximity, ref overflowed);
        parameters.AllyUnderFocusPressure = ApplyPercent(parameters.AllyUnderFocusPressure, percents.allyUnderFocusPressure, ref overflowed);
        parameters.AllyFrontlineGap = ApplyPercent(parameters.AllyFrontlineGap, percents.allyFrontlineGap, ref overflowed);
        parameters.IsolatedEnemyVulnerability = ApplyPercent(parameters.IsolatedEnemyVulnerability, percents.isolatedEnemyVulnerability, ref overflowed);
        parameters.EnemyClusterDensity = ApplyPercent(parameters.EnemyClusterDensity, percents.enemyClusterDensity, ref overflowed);
        parameters.DistanceToTeamCenter = ApplyPercent(parameters.DistanceToTeamCenter, percents.distanceToTeamCenter, ref overflowed);
        parameters.SelfCanAttackNow = ApplyPercent(parameters.SelfCanAttackNow, percents.selfCanAttackNow, ref overflowed);

        return parameters;
    }

    private static float ApplyPercent(float value, int percent, ref bool overflowed)
    {
        float modifiedValue = value * (percent / 100f);
        if (modifiedValue < 0f || modifiedValue > 1f)
            overflowed = true;

        return modifiedValue;
    }
}
