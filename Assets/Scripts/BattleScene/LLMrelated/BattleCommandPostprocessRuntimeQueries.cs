// 후처리 단계에서만 쓰는 전장 조회와 후보 선택을 모은다.
// 실행 plan을 만들지 않고, finalActionSequence 보정 후보만 고른다.
// BattleOrderRuntimeQueries의 SOT 입력 계산을 변경하지 않는다.
// 범용 조회가 되면 BattleOrderRuntimeQueries로 승격할 수 있다.

using System.Collections.Generic;
using UnityEngine;

public static class BattleCommandPostprocessRuntimeQueries
{
    private const float ClusterScoreRadius = 350f;

    public static bool IsEnemyTargetableForPostprocess(
        BattleRuntimeUnit enemy,
        BattleSimulationManager simulationManager
    )
    {
        return BattleOrderRuntimeQueries.IsAlive(enemy)
            && !BattleOrderRuntimeQueries.HasTargetingBlock(enemy, simulationManager);
    }

    public static bool IsLivingAlly(BattleRuntimeUnit unit)
    {
        return BattleOrderRuntimeQueries.IsAlive(unit);
    }

    public static bool IsValidOtherAllyTarget(BattleRuntimeUnit actor, BattleRuntimeUnit target)
    {
        return actor != null && target != null && target != actor && BattleOrderRuntimeQueries.IsAlive(target);
    }

    public static bool IsValidDeadAllyTarget(BattleRuntimeUnit actor, BattleRuntimeUnit target)
    {
        return actor != null && target != null && target != actor && target.IsCombatDisabled;
    }

    public static bool IsHoldFrontAnchorValid(
        BattleRuntimeUnit anchor,
        IReadOnlyDictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap,
        BattleSimulationManager simulationManager
    )
    {
        if (!BattleOrderRuntimeQueries.IsAlive(anchor))
            return false;

        if (BattleOrderRuntimeQueries.HasTargetingBlock(anchor, simulationManager))
            return false;

        if (formationMap != null && formationMap.TryGetValue(anchor, out BattleOrderFormationInfo formation))
        {
            return formation.HoldFrontAnchorEligible;
        }

        return true;
    }

    public static BattleRuntimeUnit FindClosestTargetableEnemy(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager
    )
    {
        if (actor == null || enemies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (!IsEnemyTargetableForPostprocess(enemy, simulationManager))
                continue;

            float distanceSqr = HorizontalDistanceSqr(actor.Position, enemy.Position);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = enemy;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindTargetableEnemyClosestToPosition(
        Vector3 position,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager
    )
    {
        if (enemies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (!IsEnemyTargetableForPostprocess(enemy, simulationManager))
                continue;

            float distanceSqr = HorizontalDistanceSqr(position, enemy.Position);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = enemy;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindLowestHpTargetableEnemy(
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager
    )
    {
        if (enemies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestHpRatio = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (!IsEnemyTargetableForPostprocess(enemy, simulationManager))
                continue;

            float hpRatio = BattleOrderRuntimeQueries.GetHpRatio(enemy);
            if (hpRatio < bestHpRatio)
            {
                bestHpRatio = hpRatio;
                best = enemy;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindEnemyAlreadyEngagedWithActor(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager
    )
    {
        if (actor == null || actor.State == null || enemies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (enemy == null || enemy.State == null || !IsEnemyTargetableForPostprocess(enemy, simulationManager))
            {
                continue;
            }

            bool enemyTargetsActor =
                enemy.State.CurrentTarget == actor.State || enemy.State.PlannedTargetEnemy == actor.State;

            bool actorTargetsEnemy =
                actor.State.CurrentTarget == enemy.State || actor.State.PlannedTargetEnemy == enemy.State;

            if (!enemyTargetsActor && !actorTargetsEnemy)
                continue;

            float distanceSqr = HorizontalDistanceSqr(actor.Position, enemy.Position);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = enemy;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindBestAoeCenterEnemy(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager
    )
    {
        if (enemies == null)
            return null;

        BattleRuntimeUnit best = null;
        int bestClusterCount = -1;
        float bestDistanceSqr = float.MaxValue;
        float radiusSqr = ClusterScoreRadius * ClusterScoreRadius;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit candidate = enemies[i];
            if (!IsEnemyTargetableForPostprocess(candidate, simulationManager))
                continue;

            int clusterCount = CountTargetableEnemiesAround(candidate, enemies, simulationManager, radiusSqr);
            float distanceSqr = actor != null ? HorizontalDistanceSqr(actor.Position, candidate.Position) : 0f;

            if (clusterCount > bestClusterCount || (clusterCount == bestClusterCount && distanceSqr < bestDistanceSqr))
            {
                bestClusterCount = clusterCount;
                bestDistanceSqr = distanceSqr;
                best = candidate;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindLowestHpLivingAlly(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allies
    )
    {
        if (allies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestHpRatio = float.MaxValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (!IsValidOtherAllyTarget(actor, ally))
                continue;

            float hpRatio = BattleOrderRuntimeQueries.GetHpRatio(ally);
            if (hpRatio < bestHpRatio)
            {
                bestHpRatio = hpRatio;
                best = ally;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindMostPressuredAlly(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyList<BattleRuntimeUnit> enemies
    )
    {
        if (actor == null || allies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (!IsValidOtherAllyTarget(actor, ally))
                continue;

            int engagedCount = BattleOrderRuntimeQueries.CountEngagedByOpponents(ally, enemies);
            float hpPressure = 1f - BattleOrderRuntimeQueries.GetHpRatio(ally);
            float distancePenalty = HorizontalDistanceSqr(actor.Position, ally.Position) * 0.00001f;
            float score = engagedCount * 100f + hpPressure * 50f - distancePenalty;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindEligibleBacklineAlly(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyDictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap
    )
    {
        if (actor == null || allies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (!IsValidOtherAllyTarget(actor, ally))
                continue;

            if (
                formationMap == null
                || !formationMap.TryGetValue(ally, out BattleOrderFormationInfo formation)
                || formation.Role != "backline"
                || !formation.HoldFrontAnchorEligible
            )
            {
                continue;
            }

            float distanceSqr = HorizontalDistanceSqr(actor.Position, ally.Position);
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = ally;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindFarthestLivingAlly(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allies
    )
    {
        if (actor == null || allies == null)
            return null;

        BattleRuntimeUnit best = null;
        float bestDistanceSqr = float.MinValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (!IsValidOtherAllyTarget(actor, ally))
                continue;

            float distanceSqr = HorizontalDistanceSqr(actor.Position, ally.Position);
            if (distanceSqr > bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                best = ally;
            }
        }

        return best;
    }

    public static BattleRuntimeUnit FindDeadAllyTarget(BattleRuntimeUnit actor, IReadOnlyList<BattleRuntimeUnit> allies)
    {
        if (actor == null || allies == null)
            return null;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (IsValidDeadAllyTarget(actor, ally))
                return ally;
        }

        return null;
    }

    public static BattleRuntimeUnit FindBestHoldFrontAnchor(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        IReadOnlyDictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap,
        BattleSimulationManager simulationManager
    )
    {
        BattleRuntimeUnit enemyAnchor = FindClosestTargetableEnemy(actor, enemies, simulationManager);
        if (IsHoldFrontAnchorValid(enemyAnchor, formationMap, simulationManager))
            return enemyAnchor;

        if (allies != null)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                BattleRuntimeUnit ally = allies[i];
                if (ally == actor)
                    continue;

                if (IsHoldFrontAnchorValid(ally, formationMap, simulationManager))
                    return ally;
            }
        }

        return null;
    }

    private static int CountTargetableEnemiesAround(
        BattleRuntimeUnit center,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        BattleSimulationManager simulationManager,
        float radiusSqr
    )
    {
        int count = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (!IsEnemyTargetableForPostprocess(enemy, simulationManager))
                continue;

            if (HorizontalDistanceSqr(center.Position, enemy.Position) <= radiusSqr)
                count++;
        }

        return count;
    }

    private static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
    {
        Vector3 diff = a - b;
        diff.y = 0f;
        return diff.sqrMagnitude;
    }
}
