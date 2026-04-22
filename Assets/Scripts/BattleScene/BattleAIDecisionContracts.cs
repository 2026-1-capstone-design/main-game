/*
여기 추가 <- 검색해서 무기군 적절하게 추가하기
*/

using System;
using System.Collections.Generic;
using UnityEngine;

// 전투 AI 행동 목록. "가장 가까운 살아있는 적만 추적"은 fallback일 뿐, 실제 판단은 행동 점수 시스템이 먼저 결정한다.
public enum BattleActionType
{
    None = 0,
    AssassinateIsolatedEnemy = 1, // 고립된 적 암살: 혼자 떨어진 적 추적
    DiveEnemyBackline = 2, // 적 후방 다이브: 후방 취약 적 추적
    PeelForWeakAlly = 3, // 아군 보호: 압박받는 아군 주변 적을 우선
    EscapeFromPressure = 4, // 도주: 압박 중심 반대 방향 + 팀 방향 혼합
    RegroupToAllies = 5, // 집결: 팀 중심으로 이동
    CollapseOnCluster = 6, // 군집 압박: 적 군집 중심으로 압박
    EngageNearest = 7, // 최근접 교전: 가장 가까운 적 교전 (fallback)
}

// 매 tick마다 각 유닛에 대해 계산되는 RAW 파라미터 9개. 모두 마지막에 Clamp01 된다.
[Serializable]
public struct BattleParameterSet
{
    public float SelfHpLow; // 1 - CurrentHealth/MaxHealth. 체력이 낮을수록 큼
    public float SelfSurroundedByEnemies; // closeFalloff 합산 / 3. 가까운 적이 많을수록 큼
    public float LowHealthAllyProximity; // 저체력 아군 * 거리 falloff 합산 / 2. 저체력 아군이 가까울수록 큼
    public float AllyUnderFocusPressure; // 집중 공격받는 아군이 가까이 있고 체력도 낮을수록 큼 (최대값 사용)
    public float AllyFrontlineGap; // 아군 최근접 거리 평균 / frontlineGapRadius. 아군 진형이 퍼질수록 큼
    public float IsolatedEnemyVulnerability; // 혼자 떨어져 있고 체력이 낮고 내가 너무 멀지 않은 적일수록 큼 (최대값 사용)
    public float EnemyClusterDensity; // 적 pair 간 falloff 평균. 적들이 서로 뭉쳐 있을수록 큼
    public float DistanceToTeamCenter; // 자기와 팀 중심 거리 / teamCenterDistanceRadius. 멀리 떨어질수록 큼
    public float SelfCanAttackNow; // 사거리 안에 적이 하나라도 있으면 1, 아니면 0 (이진값)

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
        return parameters.SelfHpLow * selfHpLow
            + parameters.SelfSurroundedByEnemies * selfSurroundedByEnemies
            + parameters.LowHealthAllyProximity * lowHealthAllyProximity
            + parameters.AllyUnderFocusPressure * allyUnderFocusPressure
            + parameters.AllyFrontlineGap * allyFrontlineGap
            + parameters.IsolatedEnemyVulnerability * isolatedEnemyVulnerability
            + parameters.EnemyClusterDensity * enemyClusterDensity
            + parameters.DistanceToTeamCenter * distanceToTeamCenter
            + parameters.SelfCanAttackNow * selfCanAttackNow;
    }

    public BattleParameterSet ApplyPercentModifiers(BattleParameterSet parameters)
    {
        parameters.SelfHpLow = Mathf.Clamp01(parameters.SelfHpLow * (selfHpLow / 100f));
        parameters.SelfSurroundedByEnemies = Mathf.Clamp01(
            parameters.SelfSurroundedByEnemies * (selfSurroundedByEnemies / 100f)
        );
        parameters.LowHealthAllyProximity = Mathf.Clamp01(
            parameters.LowHealthAllyProximity * (lowHealthAllyProximity / 100f)
        );
        parameters.AllyUnderFocusPressure = Mathf.Clamp01(
            parameters.AllyUnderFocusPressure * (allyUnderFocusPressure / 100f)
        );
        parameters.AllyFrontlineGap = Mathf.Clamp01(parameters.AllyFrontlineGap * (allyFrontlineGap / 100f));
        parameters.IsolatedEnemyVulnerability = Mathf.Clamp01(
            parameters.IsolatedEnemyVulnerability * (isolatedEnemyVulnerability / 100f)
        );
        parameters.EnemyClusterDensity = Mathf.Clamp01(parameters.EnemyClusterDensity * (enemyClusterDensity / 100f));
        parameters.DistanceToTeamCenter = Mathf.Clamp01(
            parameters.DistanceToTeamCenter * (distanceToTeamCenter / 100f)
        );
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
            selfCanAttackNow = value,
        };
    }
}

// 각 행동(BattleActionType)의 AI 튜닝 데이터.
// 점수 계산식: RawScore = baseBias + Σ(modifiedParam_i * weight_i)
//              FinalScore = RawScore * (weaponTypePercent / 100f)
// currentActionParameterPercents: 현재 이 행동을 수행 중일 때 RAW→MOD 파라미터 변환에 사용.
//   ex) Regroup 중이면 allyFrontlineGap, distanceToTeamCenter를 키우고 selfCanAttackNow를 줄여서 regroup 유지를 유도.
// inspector에서 행동별 가중치, 보정치, 무기별 최종값 보정, 반경값, threshold 등을 바로 조정 가능.
[Serializable]
public sealed class BattleActionTuning
{
    public BattleActionType actionType = BattleActionType.EngageNearest;
    public string displayName = "EngageNearest";
    public int baseBias = 0; // 행동 점수의 기본 편향값

    [Header("Score Weights (-10 ~ +10 권장)")]
    public BattleParameterWeights scoreWeights = new BattleParameterWeights();

    // 현재 이 행동 수행 중일 때 RAW 파라미터에 곱해지는 퍼센트 보정치. 행동 선택 이전 단계의 입력값 자체를 왜곡한다.
    [Header("Current Action Param Modifiers (percent)")]
    public BattleParameterWeights currentActionParameterPercents = BattleParameterWeights.CreateFilled(100);

    // 무기 타입별 행동 최종 점수 배율. 클래스 성향 대신 무기군으로 판정한다.
    [Header("Weapon Type Final Score Percents")]
    public int oneHandPercent = 100;
    public int twoHandPercent = 100;
    public int dualHandPercent = 100;
    public int spearPercent = 100;
    public int shieldPercent = 100;
    public int daggerPercent = 100;
    public int handgunPercent = 100;
    public int dualgunPercent = 100;
    public int riflePercent = 100;
    public int staffPercent = 100;
    public int bowPercent = 100;

    private static WeaponType GetImplicitAffinityWeaponType(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.oneHand:
                return WeaponType.oneHand;
            case WeaponType.twoHand:
                return WeaponType.twoHand;
            case WeaponType.dualHand:
                return WeaponType.dualHand;
            case WeaponType.spear:
                return WeaponType.spear;
            case WeaponType.shield:
                return WeaponType.shield;
            case WeaponType.dagger:
                return WeaponType.dagger;
            case WeaponType.handGun:
                return WeaponType.handGun;
            case WeaponType.dualGun:
                return WeaponType.dualGun;
            case WeaponType.rifle:
                return WeaponType.rifle;
            case WeaponType.staff:
                return WeaponType.staff;
            case WeaponType.bow:
                return WeaponType.bow;

            case WeaponType.None:
            default:
                return WeaponType.None;
        }
    }

    public int GetWeaponTypePercent(WeaponType weaponType)
    {
        switch (GetImplicitAffinityWeaponType(weaponType))
        {
            case WeaponType.oneHand:
                return oneHandPercent;
            case WeaponType.twoHand:
                return twoHandPercent;
            case WeaponType.dualHand:
                return dualHandPercent;
            case WeaponType.spear:
                return spearPercent;
            case WeaponType.shield:
                return shieldPercent;
            case WeaponType.dagger:
                return daggerPercent;
            case WeaponType.handGun:
                return handgunPercent;
            case WeaponType.dualGun:
                return dualgunPercent;
            case WeaponType.rifle:
                return riflePercent;
            case WeaponType.staff:
                return staffPercent;
            case WeaponType.bow:
                return bowPercent;
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

    public void GetBestActionExcluding(
        BattleActionType excludedAction,
        out BattleActionType bestAction,
        out float bestScore
    )
    {
        bestAction = BattleActionType.None;
        bestScore = float.MinValue;

        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.AssassinateIsolatedEnemy,
            AssassinateIsolatedEnemy
        );
        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.DiveEnemyBackline,
            DiveEnemyBackline
        );
        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.PeelForWeakAlly,
            PeelForWeakAlly
        );
        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.EscapeFromPressure,
            EscapeFromPressure
        );
        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.RegroupToAllies,
            RegroupToAllies
        );
        TryCheckExcluded(
            ref bestAction,
            ref bestScore,
            excludedAction,
            BattleActionType.CollapseOnCluster,
            CollapseOnCluster
        );
        TryCheckExcluded(ref bestAction, ref bestScore, excludedAction, BattleActionType.EngageNearest, EngageNearest);

        if (bestAction == BattleActionType.None)
        {
            bestAction = BattleActionType.EngageNearest;
            bestScore = EngageNearest;
        }
    }

    private static void Check(
        ref BattleActionType bestAction,
        ref float bestScore,
        BattleActionType candidateAction,
        float candidateScore
    )
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
        float candidateScore
    )
    {
        if (candidateAction == excludedAction)
            return;

        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }
}

// 파라미터 계산에 사용되는 반경/임계값 모음. BattleSimulationManager Inspector 필드에서 채워진다.
[Serializable]
public struct BattleParameterRadii
{
    public float surroundRadius; // self_surrounded_by_enemies 계산 기준 반경
    public float helpRadius; // low_health_ally_proximity 계산 기준 반경
    public float peelRadius; // ally_under_focus_pressure 거리 가중치 기준 반경
    public float frontlineGapRadius; // ally_frontline_gap 정규화 기준 반경
    public float isolationRadius; // isolated_enemy_vulnerability 고립 판정 기준 반경
    public float assassinReachRadius; // isolated_enemy_vulnerability 자기 도달 가능 거리 기준
    public float clusterRadius; // enemy_cluster_density 군집 판정 기준 반경
    public float teamCenterDistanceRadius; // distance_to_team_center 정규화 기준 반경
}

// 파라미터 계산을 위한 유닛의 런타임 스냅샷. MonoBehaviour 없이 순수 C# 테스트 가능.
public readonly struct BattleUnitView
{
    public readonly int UnitNumber;
    public readonly bool IsEnemy;
    public readonly Vector3 Position;
    public readonly float CurrentHealth;
    public readonly float MaxHealth;
    public readonly float BodyRadius;
    public readonly float AttackRange;
    public readonly bool IsCombatDisabled;
    public readonly int PlannedEnemyTargetNumber; // -1 if none
    public readonly int CurrentTargetNumber; // -1 if none

    public BattleUnitView(
        int unitNumber,
        bool isEnemy,
        Vector3 position,
        float currentHealth,
        float maxHealth,
        float bodyRadius,
        float attackRange,
        bool isCombatDisabled,
        int plannedEnemyTargetNumber,
        int currentTargetNumber
    )
    {
        UnitNumber = unitNumber;
        IsEnemy = isEnemy;
        Position = position;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        BodyRadius = bodyRadius;
        AttackRange = attackRange;
        IsCombatDisabled = isCombatDisabled;
        PlannedEnemyTargetNumber = plannedEnemyTargetNumber;
        CurrentTargetNumber = currentTargetNumber;
    }

    public static BattleUnitView From(BattleRuntimeUnit u)
    {
        return From(u != null ? u.State : null);
    }

    public static BattleUnitView From(BattleUnitCombatState u)
    {
        if (u == null)
            return default;

        return new BattleUnitView(
            u.UnitNumber,
            u.IsEnemy,
            u.Position,
            u.CurrentHealth,
            u.MaxHealth,
            u.BodyRadius,
            u.AttackRange,
            u.IsCombatDisabled,
            u.PlannedTargetEnemy != null ? u.PlannedTargetEnemy.UnitNumber : -1,
            u.CurrentTarget != null ? u.CurrentTarget.UnitNumber : -1
        );
    }
}

[Serializable]
public struct BattleActionExecutionPlan
{
    public BattleActionType Action;
    public BattleUnitCombatState TargetEnemy;
    public BattleUnitCombatState TargetAlly;
    public Vector3 DesiredPosition;
    public bool HasDesiredPosition;
}
