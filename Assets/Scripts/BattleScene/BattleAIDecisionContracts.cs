
/*
여기 추가 <- 검색해서 무기군 적절하게 추가하기


*/


using System;
using UnityEngine;

public enum BattleActionType
{
    None = 0,
    AssassinateIsolatedEnemy = 1,
    DiveEnemyBackline = 2,
    PeelForWeakAlly = 3,
    EscapeFromPressure = 4,
    RegroupToAllies = 5,
    CollapseOnCluster = 6,
    EngageNearest = 7
}


[Serializable]
public struct BattleParameterSet
{
    public float SelfHpLow;
    public float SelfSurroundedByEnemies;
    public float LowHealthAllyProximity;
    public float AllyUnderFocusPressure;
    public float AllyFrontlineGap;
    public float IsolatedEnemyVulnerability;
    public float EnemyClusterDensity;
    public float DistanceToTeamCenter;
    public float SelfCanAttackNow;

    public void Clamp01All()
    {
        SelfHpLow = Mathf.Clamp01(SelfHpLow);
        SelfSurroundedByEnemies = Mathf.Clamp01(SelfSurroundedByEnemies);
        LowHealthAllyProximity = Mathf.Clamp01(LowHealthAllyProximity);
        AllyUnderFocusPressure = Mathf.Clamp01(AllyUnderFocusPressure);
        AllyFrontlineGap = Mathf.Clamp01(AllyFrontlineGap);
        IsolatedEnemyVulnerability = Mathf.Clamp01(IsolatedEnemyVulnerability);
        EnemyClusterDensity = Mathf.Clamp01(EnemyClusterDensity);
        DistanceToTeamCenter = Mathf.Clamp01(DistanceToTeamCenter);
        SelfCanAttackNow = Mathf.Clamp01(SelfCanAttackNow);
    }
}

[Serializable]
public sealed class BattleParameterWeights
{
    public int selfHpLow;
    public int selfSurroundedByEnemies;
    public int lowHealthAllyProximity;
    public int allyUnderFocusPressure;
    public int allyFrontlineGap;
    public int isolatedEnemyVulnerability;
    public int enemyClusterDensity;
    public int distanceToTeamCenter;
    public int selfCanAttackNow;

    public float Evaluate(BattleParameterSet parameters)
    {
        return
            parameters.SelfHpLow * selfHpLow +
            parameters.SelfSurroundedByEnemies * selfSurroundedByEnemies +
            parameters.LowHealthAllyProximity * lowHealthAllyProximity +
            parameters.AllyUnderFocusPressure * allyUnderFocusPressure +
            parameters.AllyFrontlineGap * allyFrontlineGap +
            parameters.IsolatedEnemyVulnerability * isolatedEnemyVulnerability +
            parameters.EnemyClusterDensity * enemyClusterDensity +
            parameters.DistanceToTeamCenter * distanceToTeamCenter +
            parameters.SelfCanAttackNow * selfCanAttackNow;
    }

    public BattleParameterSet ApplyPercentModifiers(BattleParameterSet parameters)
    {
        parameters.SelfHpLow = Mathf.Clamp01(parameters.SelfHpLow * (selfHpLow / 100f));
        parameters.SelfSurroundedByEnemies = Mathf.Clamp01(parameters.SelfSurroundedByEnemies * (selfSurroundedByEnemies / 100f));
        parameters.LowHealthAllyProximity = Mathf.Clamp01(parameters.LowHealthAllyProximity * (lowHealthAllyProximity / 100f));
        parameters.AllyUnderFocusPressure = Mathf.Clamp01(parameters.AllyUnderFocusPressure * (allyUnderFocusPressure / 100f));
        parameters.AllyFrontlineGap = Mathf.Clamp01(parameters.AllyFrontlineGap * (allyFrontlineGap / 100f));
        parameters.IsolatedEnemyVulnerability = Mathf.Clamp01(parameters.IsolatedEnemyVulnerability * (isolatedEnemyVulnerability / 100f));
        parameters.EnemyClusterDensity = Mathf.Clamp01(parameters.EnemyClusterDensity * (enemyClusterDensity / 100f));
        parameters.DistanceToTeamCenter = Mathf.Clamp01(parameters.DistanceToTeamCenter * (distanceToTeamCenter / 100f));
        parameters.SelfCanAttackNow = Mathf.Clamp01(parameters.SelfCanAttackNow * (selfCanAttackNow / 100f));
        return parameters;
    }

    public static BattleParameterWeights CreateFilled(int value)
    {
        return new BattleParameterWeights
        {
            selfHpLow = value,
            selfSurroundedByEnemies = value,
            lowHealthAllyProximity = value,
            allyUnderFocusPressure = value,
            allyFrontlineGap = value,
            isolatedEnemyVulnerability = value,
            enemyClusterDensity = value,
            distanceToTeamCenter = value,
            selfCanAttackNow = value
        };
    }
}

[Serializable]
public sealed class BattleActionTuning
{
    public BattleActionType actionType = BattleActionType.EngageNearest;
    public string displayName = "EngageNearest";
    public int baseBias = 0;

    [Header("Score Weights (-10 ~ +10 권장)")]
    public BattleParameterWeights scoreWeights = new BattleParameterWeights();

    [Header("Current Action Param Modifiers (percent)")]
    public BattleParameterWeights currentActionParameterPercents = BattleParameterWeights.CreateFilled(100);

    [Header("Weapon Type Final Score Percents")]    //다섯개의 대표 무기군
    public int swordPercent = 100;
    public int crossbowPercent = 100;
    public int macePercent = 100;
    public int daggerPercent = 100;
    public int orbPercent = 100;

    private static WeaponType GetImplicitAffinityWeaponType(WeaponType weaponType) //여기 추가
    {
        switch (weaponType)
        {
            case WeaponType.Staff:
            case WeaponType.Orb:
                return WeaponType.Orb;          // 마법사가 쓸법한 무기군들

            case WeaponType.Dagger:
                return WeaponType.Dagger;       // 암살자가 쓸만한 무기군들

            case WeaponType.Sword:
                return WeaponType.Sword;        // 전사/근접 무기군들

            case WeaponType.Crossbow:
                return WeaponType.Crossbow;     //원딜러 무기군들

            case WeaponType.Mace:
                return WeaponType.Mace;         //탱커 무기군들 

            case WeaponType.None:
            default:
                return WeaponType.None;
        }
    }

    public int GetWeaponTypePercent(WeaponType weaponType)
    {
        switch (GetImplicitAffinityWeaponType(weaponType))
        {
            case WeaponType.Sword:
                return swordPercent;

            case WeaponType.Crossbow:
                return crossbowPercent;

            case WeaponType.Mace:
                return macePercent;

            case WeaponType.Orb:
                return orbPercent;

            case WeaponType.Dagger:
                return daggerPercent;

            case WeaponType.None:
            default:
                return 100;
        }
    }
}

[Serializable]
public struct BattleActionScoreSet
{
    public float AssassinateIsolatedEnemy;
    public float DiveEnemyBackline;
    public float PeelForWeakAlly;
    public float EscapeFromPressure;
    public float RegroupToAllies;
    public float CollapseOnCluster;
    public float EngageNearest;

    public float GetScore(BattleActionType actionType)
    {
        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
                return AssassinateIsolatedEnemy;
            case BattleActionType.DiveEnemyBackline:
                return DiveEnemyBackline;
            case BattleActionType.PeelForWeakAlly:
                return PeelForWeakAlly;
            case BattleActionType.EscapeFromPressure:
                return EscapeFromPressure;
            case BattleActionType.RegroupToAllies:
                return RegroupToAllies;
            case BattleActionType.CollapseOnCluster:
                return CollapseOnCluster;
            case BattleActionType.EngageNearest:
                return EngageNearest;
            default:
                return float.MinValue;
        }
    }

    public void SetScore(BattleActionType actionType, float score)
    {
        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
                AssassinateIsolatedEnemy = score;
                break;
            case BattleActionType.DiveEnemyBackline:
                DiveEnemyBackline = score;
                break;
            case BattleActionType.PeelForWeakAlly:
                PeelForWeakAlly = score;
                break;
            case BattleActionType.EscapeFromPressure:
                EscapeFromPressure = score;
                break;
            case BattleActionType.RegroupToAllies:
                RegroupToAllies = score;
                break;
            case BattleActionType.CollapseOnCluster:
                CollapseOnCluster = score;
                break;
            case BattleActionType.EngageNearest:
                EngageNearest = score;
                break;
        }
    }

    public void GetBestAction(out BattleActionType bestAction, out float bestScore)
    {
        bestAction = BattleActionType.EngageNearest;
        bestScore = EngageNearest;

        Check(ref bestAction, ref bestScore, BattleActionType.AssassinateIsolatedEnemy, AssassinateIsolatedEnemy);
        Check(ref bestAction, ref bestScore, BattleActionType.DiveEnemyBackline, DiveEnemyBackline);
        Check(ref bestAction, ref bestScore, BattleActionType.PeelForWeakAlly, PeelForWeakAlly);
        Check(ref bestAction, ref bestScore, BattleActionType.EscapeFromPressure, EscapeFromPressure);
        Check(ref bestAction, ref bestScore, BattleActionType.RegroupToAllies, RegroupToAllies);
        Check(ref bestAction, ref bestScore, BattleActionType.CollapseOnCluster, CollapseOnCluster);
        Check(ref bestAction, ref bestScore, BattleActionType.EngageNearest, EngageNearest);
    }

    public void GetBestActionExcluding(BattleActionType excludedAction, out BattleActionType bestAction, out float bestScore)
    {
        bestAction = BattleActionType.None;
        bestScore = float.MinValue;

        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.AssassinateIsolatedEnemy, AssassinateIsolatedEnemy);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.DiveEnemyBackline, DiveEnemyBackline);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.PeelForWeakAlly, PeelForWeakAlly);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.EscapeFromPressure, EscapeFromPressure);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.RegroupToAllies, RegroupToAllies);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.CollapseOnCluster, CollapseOnCluster);
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.EngageNearest, EngageNearest);

        if (bestAction == BattleActionType.None)
        {
            bestAction = BattleActionType.EngageNearest;
            bestScore = EngageNearest;
        }
    }

    private static void Check(ref BattleActionType bestAction, ref float bestScore, BattleActionType candidateAction, float candidateScore)
    {
        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }

    private static void TryCheckExcluded(
        ref BattleActionType bestAction,
        ref float bestScore,
        BattleActionType excludedAction,
        BattleActionType candidateAction,
        float candidateScore)
    {
        if (candidateAction == excludedAction)
        {
            return;
        }

        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }
}

[Serializable]
public struct BattleActionExecutionPlan
{
    public BattleActionType Action;
    public BattleRuntimeUnit TargetEnemy;
    public BattleRuntimeUnit TargetAlly;
    public Vector2 DesiredPosition;
    public bool HasDesiredPosition;
}