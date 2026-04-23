using System.Collections.Generic;
using UnityEngine;

// 유닛의 Modified 파라미터와 튜닝 데이터로부터 각 행동의 최종 점수를 계산하는 정적 클래스.
// 전역 상태에 의존하지 않으므로 Edit Mode 단위 테스트 가능.
public static class BattleActionScorer
{
    public static BattleActionScoreSet Evaluate(
        BattleParameterSet modifiedParams,
        WeaponType weaponType,
        IReadOnlyList<BattleActionTuning> tunings)
    {
        BattleActionScoreSet scores = default;

        for (int i = 0; i < tunings.Count; i++)
        {
            BattleActionTuning tuning = tunings[i];
            if (tuning == null || tuning.actionType == BattleActionType.None)
                continue;

            float rawScore = tuning.baseBias + tuning.scoreWeights.Evaluate(modifiedParams);
            int weaponTypePercent = tuning.GetWeaponTypePercent(weaponType);
            const int personalityPercent = 100; // 퍼스낼리티 시스템 미구현, 기본값 유지

            float finalScore = rawScore * (weaponTypePercent / 100f) * (personalityPercent / 100f);
            scores.SetScore(tuning.actionType, finalScore);
        }
        return scores;
    }

    // 도주 중이고 압박이 해소되면 재교전 행동 점수를 증폭시킨다.
    public static BattleActionScoreSet ApplyEscapeReengageBias(
        BattleActionType currentAction,
        BattleParameterSet rawParams,
        BattleActionScoreSet scores)
    {
        if (currentAction != BattleActionType.EscapeFromPressure)
            return scores;

        bool noEnemyInAttackRange = rawParams.SelfCanAttackNow <= 0f;
        bool pressureMostlyGone = rawParams.SelfSurroundedByEnemies <= 0.20f;

        if (!noEnemyInAttackRange || !pressureMostlyGone)
            return scores;

        scores.EscapeFromPressure *= 0.10f;
        scores.AssassinateIsolatedEnemy *= 1.50f;
        scores.DiveEnemyBackline *= 1.35f;
        scores.CollapseOnCluster *= 1.30f;

        return scores;
    }
}
