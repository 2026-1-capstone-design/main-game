using System.Collections.Generic;

public sealed class BattleParameterSystem
{
    private readonly List<BattleUnitView> _allyViewBuffer = new List<BattleUnitView>(
        BattleTeamConstants.MaxUnitsPerTeam
    );
    private readonly List<BattleUnitView> _enemyViewBuffer = new List<BattleUnitView>(
        BattleTeamConstants.MaxUnitsPerTeam
    );

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
            teamCenterDistanceRadius = aiTuning.teamCenterDistanceRadius,
        };
    }

    public void Compute(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        BattleAITuningSO aiTuning,
        BattleFieldSnapshot snapshot,
        bool[] modifierOverflowFlags
    )
    {
        if (units == null || modifierOverflowFlags == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            modifierOverflowFlags[i] = false;
            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleUnitView self = BattleUnitView.From(unit.State);

            _allyViewBuffer.Clear();
            _enemyViewBuffer.Clear();

            snapshot.GetLivingAllyViews(unit.State, _allyViewBuffer);
            snapshot.GetLivingEnemyViews(unit.State, _enemyViewBuffer);

            for (int j = _allyViewBuffer.Count - 1; j >= 0; j--)
            {
                if (_allyViewBuffer[j].UnitNumber == self.UnitNumber)
                    _allyViewBuffer.RemoveAt(j);
            }

            BattleParameterSet raw = BattleParameterComputer.Compute(self, _allyViewBuffer, _enemyViewBuffer, radii);
            BattleParameterSet modified = ApplyCurrentActionParameterModifiers(
                unit,
                raw,
                aiTuning,
                out bool overflowed
            );

            raw.Clamp01All();
            modified.Clamp01All();

            unit.State.SetCurrentParameters(raw, modified);
            modifierOverflowFlags[i] = overflowed;
        }
    }

    private static BattleParameterSet ApplyCurrentActionParameterModifiers(
        BattleRuntimeUnit unit,
        BattleParameterSet rawParameters,
        BattleAITuningSO aiTuning,
        out bool overflowed
    )
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
        out bool overflowed
    )
    {
        overflowed = false;

        parameters.SelfHpLow = ApplyPercent(parameters.SelfHpLow, percents.selfHpLow, ref overflowed);
        parameters.SelfSurroundedByEnemies = ApplyPercent(
            parameters.SelfSurroundedByEnemies,
            percents.selfSurroundedByEnemies,
            ref overflowed
        );
        parameters.LowHealthAllyProximity = ApplyPercent(
            parameters.LowHealthAllyProximity,
            percents.lowHealthAllyProximity,
            ref overflowed
        );
        parameters.AllyUnderFocusPressure = ApplyPercent(
            parameters.AllyUnderFocusPressure,
            percents.allyUnderFocusPressure,
            ref overflowed
        );
        parameters.AllyFrontlineGap = ApplyPercent(
            parameters.AllyFrontlineGap,
            percents.allyFrontlineGap,
            ref overflowed
        );
        parameters.IsolatedEnemyVulnerability = ApplyPercent(
            parameters.IsolatedEnemyVulnerability,
            percents.isolatedEnemyVulnerability,
            ref overflowed
        );
        parameters.EnemyClusterDensity = ApplyPercent(
            parameters.EnemyClusterDensity,
            percents.enemyClusterDensity,
            ref overflowed
        );
        parameters.DistanceToTeamCenter = ApplyPercent(
            parameters.DistanceToTeamCenter,
            percents.distanceToTeamCenter,
            ref overflowed
        );
        parameters.SelfCanAttackNow = ApplyPercent(
            parameters.SelfCanAttackNow,
            percents.selfCanAttackNow,
            ref overflowed
        );

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
