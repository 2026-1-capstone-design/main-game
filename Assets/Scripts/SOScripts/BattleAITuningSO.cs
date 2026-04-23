using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleAITuning", menuName = "Capstone/Battle/AI Tuning")]
public class BattleAITuningSO : ScriptableObject
{
    [Header("AI / Action Switching")]
    [SerializeField]
    public float commitmentDecayPerSecond = 0.5f;

    [Header("AI / Parameter Radii")]
    public float surroundRadius = 350f; // self_surrounded_by_enemies 계산 기준 반경
    public float helpRadius = 450f; // low_health_ally_proximity 계산 기준 반경
    public float peelRadius = 500f; // ally_under_focus_pressure 거리 가중치 기준 반경
    public float frontlineGapRadius = 600f; // ally_frontline_gap 정규화 기준 반경
    public float isolationRadius = 450f; // isolated_enemy_vulnerability 고립 판정 기준 반경
    public float assassinReachRadius = 600f; // isolated_enemy_vulnerability 자기 도달 가능 거리 기준
    public float clusterRadius = 400f; // enemy_cluster_density 군집 판정 기준 반경
    public float teamCenterDistanceRadius = 800f; // distance_to_team_center 정규화 기준 반경

    [Header("AI / Action Tunings")]
    public List<BattleActionTuning> actionTunings = new List<BattleActionTuning>();

    private void OnValidate()
    {
        commitmentDecayPerSecond = Mathf.Max(0f, commitmentDecayPerSecond);
        surroundRadius = Mathf.Max(1f, surroundRadius);
        helpRadius = Mathf.Max(1f, helpRadius);
        peelRadius = Mathf.Max(1f, peelRadius);
        frontlineGapRadius = Mathf.Max(1f, frontlineGapRadius);
        isolationRadius = Mathf.Max(1f, isolationRadius);
        assassinReachRadius = Mathf.Max(1f, assassinReachRadius);
        clusterRadius = Mathf.Max(1f, clusterRadius);
        teamCenterDistanceRadius = Mathf.Max(1f, teamCenterDistanceRadius);

        EnsureDefaultActionTunings();
    }

    public void EnsureDefaultActionTunings()
    {
        if (actionTunings == null)
            actionTunings = new List<BattleActionTuning>();
        EnsureActionTuningExists(BattleActionType.AssassinateIsolatedEnemy);
        EnsureActionTuningExists(BattleActionType.DiveEnemyBackline);
        EnsureActionTuningExists(BattleActionType.PeelForWeakAlly);
        EnsureActionTuningExists(BattleActionType.EscapeFromPressure);
        EnsureActionTuningExists(BattleActionType.RegroupToAllies);
        EnsureActionTuningExists(BattleActionType.CollapseOnCluster);
        EnsureActionTuningExists(BattleActionType.EngageNearest);
    }

    private void EnsureActionTuningExists(BattleActionType actionType)
    {
        if (GetActionTuning(actionType) != null)
            return;
        actionTunings.Add(CreateDefaultTuning(actionType));
    }

    public BattleActionTuning GetActionTuning(BattleActionType actionType)
    {
        for (int i = 0; i < actionTunings.Count; i++)
        {
            BattleActionTuning tuning = actionTunings[i];
            if (tuning != null && tuning.actionType == actionType)
                return tuning;
        }
        return null;
    }

    private BattleActionTuning CreateDefaultTuning(BattleActionType actionType)
    {
        BattleActionTuning tuning = new BattleActionTuning
        {
            actionType = actionType,
            displayName = actionType.ToString(),
            currentActionParameterPercents = BattleParameterWeights.CreateFilled(100),
        };

        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
                tuning.baseBias = 1;
                tuning.scoreWeights.selfHpLow = -8;
                tuning.scoreWeights.selfSurroundedByEnemies = -7;
                tuning.scoreWeights.lowHealthAllyProximity = -3;
                tuning.scoreWeights.allyUnderFocusPressure = -4;
                tuning.scoreWeights.allyFrontlineGap = -2;
                tuning.scoreWeights.isolatedEnemyVulnerability = 10;
                tuning.scoreWeights.enemyClusterDensity = -8;
                tuning.scoreWeights.distanceToTeamCenter = -3;
                tuning.scoreWeights.selfCanAttackNow = 4;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 120;
                tuning.currentActionParameterPercents.enemyClusterDensity = 60;
                tuning.currentActionParameterPercents.selfCanAttackNow = 35;

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 110;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 180;
                tuning.handgunPercent = 60;
                tuning.dualgunPercent = 60;
                tuning.riflePercent = 60;
                tuning.staffPercent = 60;
                tuning.bowPercent = 60;

                break;
            case BattleActionType.DiveEnemyBackline:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = -9;
                tuning.scoreWeights.selfSurroundedByEnemies = -4;
                tuning.scoreWeights.lowHealthAllyProximity = -2;
                tuning.scoreWeights.allyUnderFocusPressure = -3;
                tuning.scoreWeights.allyFrontlineGap = -1;
                tuning.scoreWeights.isolatedEnemyVulnerability = 8;
                tuning.scoreWeights.enemyClusterDensity = -4;
                tuning.scoreWeights.distanceToTeamCenter = 2;
                tuning.scoreWeights.selfCanAttackNow = 5;
                tuning.currentActionParameterPercents.allyUnderFocusPressure = 75;
                tuning.currentActionParameterPercents.enemyClusterDensity = 75;
                tuning.currentActionParameterPercents.selfCanAttackNow = 45;

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 110;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 180;
                tuning.handgunPercent = 60;
                tuning.dualgunPercent = 60;
                tuning.riflePercent = 60;
                tuning.staffPercent = 60;
                tuning.bowPercent = 60;

                break;
            case BattleActionType.PeelForWeakAlly:
                tuning.baseBias = 2;
                tuning.scoreWeights.selfHpLow = -3;
                tuning.scoreWeights.selfSurroundedByEnemies = -1;
                tuning.scoreWeights.lowHealthAllyProximity = 9;
                tuning.scoreWeights.allyUnderFocusPressure = 10;
                tuning.scoreWeights.allyFrontlineGap = 5;
                tuning.scoreWeights.isolatedEnemyVulnerability = 1;
                tuning.scoreWeights.enemyClusterDensity = 1;
                tuning.scoreWeights.distanceToTeamCenter = -5;
                tuning.scoreWeights.selfCanAttackNow = 2;
                tuning.currentActionParameterPercents.allyUnderFocusPressure = 130;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 35;
                tuning.currentActionParameterPercents.enemyClusterDensity = 50;
                tuning.currentActionParameterPercents.selfCanAttackNow = 25;

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 70;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

                break;
            case BattleActionType.EscapeFromPressure:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = 6;
                tuning.scoreWeights.selfSurroundedByEnemies = 6;
                tuning.scoreWeights.lowHealthAllyProximity = -3;
                tuning.scoreWeights.allyUnderFocusPressure = -2;
                tuning.scoreWeights.allyFrontlineGap = 2;
                tuning.scoreWeights.isolatedEnemyVulnerability = -4;
                tuning.scoreWeights.enemyClusterDensity = 3;
                tuning.scoreWeights.distanceToTeamCenter = 3;
                tuning.scoreWeights.selfCanAttackNow = -6;
                tuning.currentActionParameterPercents.selfSurroundedByEnemies = 125;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 25;
                tuning.currentActionParameterPercents.enemyClusterDensity = 20;
                tuning.currentActionParameterPercents.selfCanAttackNow = 20;

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 70;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

                break;
            case BattleActionType.RegroupToAllies:
                tuning.baseBias = -7;
                tuning.scoreWeights.selfHpLow = 1;
                tuning.scoreWeights.selfSurroundedByEnemies = 2;
                tuning.scoreWeights.lowHealthAllyProximity = 1;
                tuning.scoreWeights.allyUnderFocusPressure = 2;
                tuning.scoreWeights.allyFrontlineGap = 3;
                tuning.scoreWeights.isolatedEnemyVulnerability = -3;
                tuning.scoreWeights.enemyClusterDensity = -1;
                tuning.scoreWeights.distanceToTeamCenter = 3;
                tuning.scoreWeights.selfCanAttackNow = -3;
                tuning.currentActionParameterPercents.allyFrontlineGap = 115;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 50;
                tuning.currentActionParameterPercents.distanceToTeamCenter = 125;
                tuning.currentActionParameterPercents.selfCanAttackNow = 25;

                tuning.oneHandPercent = 100;
                tuning.twoHandPercent = 100;
                tuning.dualHandPercent = 100;
                tuning.spearPercent = 100;
                tuning.shieldPercent = 120;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

                break;
            case BattleActionType.CollapseOnCluster:
                tuning.baseBias = 1;
                tuning.scoreWeights.selfHpLow = -6;
                tuning.scoreWeights.selfSurroundedByEnemies = -6;
                tuning.scoreWeights.lowHealthAllyProximity = 0;
                tuning.scoreWeights.allyUnderFocusPressure = 1;
                tuning.scoreWeights.allyFrontlineGap = 2;
                tuning.scoreWeights.isolatedEnemyVulnerability = -2;
                tuning.scoreWeights.enemyClusterDensity = 10;
                tuning.scoreWeights.distanceToTeamCenter = 0;
                tuning.scoreWeights.selfCanAttackNow = 6;
                tuning.currentActionParameterPercents.selfSurroundedByEnemies = 85;
                tuning.currentActionParameterPercents.enemyClusterDensity = 125;
                tuning.currentActionParameterPercents.selfCanAttackNow = 50;

                tuning.oneHandPercent = 120;
                tuning.twoHandPercent = 120;
                tuning.dualHandPercent = 120;
                tuning.spearPercent = 120;
                tuning.shieldPercent = 100;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 50;
                tuning.dualgunPercent = 50;
                tuning.riflePercent = 50;
                tuning.staffPercent = 50;
                tuning.bowPercent = 50;

                break;
            case BattleActionType.EngageNearest:
            default:
                tuning.baseBias = 100;
                tuning.scoreWeights.selfHpLow = -2;
                tuning.scoreWeights.selfSurroundedByEnemies = 2;
                tuning.scoreWeights.lowHealthAllyProximity = 1;
                tuning.scoreWeights.allyUnderFocusPressure = 1;
                tuning.scoreWeights.allyFrontlineGap = -1;
                tuning.scoreWeights.isolatedEnemyVulnerability = 3;
                tuning.scoreWeights.enemyClusterDensity = 2;
                tuning.scoreWeights.distanceToTeamCenter = -2;
                tuning.scoreWeights.selfCanAttackNow = 10;
                tuning.currentActionParameterPercents.selfCanAttackNow = 110;

                tuning.oneHandPercent = 100;
                tuning.twoHandPercent = 100;
                tuning.dualHandPercent = 100;
                tuning.spearPercent = 100;
                tuning.shieldPercent = 100;
                tuning.daggerPercent = 100;
                tuning.handgunPercent = 100;
                tuning.dualgunPercent = 100;
                tuning.riflePercent = 100;
                tuning.staffPercent = 100;
                tuning.bowPercent = 100;

                break;
        }
        return tuning;
    }
}
