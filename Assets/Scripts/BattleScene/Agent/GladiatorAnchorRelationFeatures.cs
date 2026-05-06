using System.Collections.Generic;
using UnityEngine;

public readonly struct GladiatorAnchorRelationFeatures
{
    public readonly float AllySupportPressure;
    public readonly float EnemyFocusPressure;
    public readonly float EnemyIsolation;
    public readonly float EnemyRetreatSignal;

    public GladiatorAnchorRelationFeatures(
        float allySupportPressure,
        float enemyFocusPressure,
        float enemyIsolation,
        float enemyRetreatSignal
    )
    {
        AllySupportPressure = allySupportPressure;
        EnemyFocusPressure = enemyFocusPressure;
        EnemyIsolation = enemyIsolation;
        EnemyRetreatSignal = enemyRetreatSignal;
    }
}

public static class GladiatorAnchorRelationFeatureBuilder
{
    private const float Epsilon = 1e-6f;

    public static GladiatorAnchorRelationFeatures Build(
        BattleUnitCombatState self,
        BattleUnitCombatState anchor,
        IReadOnlyList<BattleUnitCombatState> teammates,
        IReadOnlyList<BattleUnitCombatState> opponents,
        float arenaRadius
    )
    {
        if (
            self == null
            || anchor == null
            || anchor.IsCombatDisabled
            || arenaRadius <= Epsilon
            || arenaRadius >= float.MaxValue
        )
        {
            return new GladiatorAnchorRelationFeatures(0f, 0f, 0f, 0f);
        }

        bool isEnemyAnchor = anchor.TeamId != self.TeamId;
        int allySupportCount = 0;
        int allyCount = 0;
        int enemySupportCount = 0;
        int enemyCount = 0;

        if (teammates != null)
        {
            for (int i = 0; i < teammates.Count; i++)
            {
                BattleUnitCombatState teammate = teammates[i];
                if (teammate == null || teammate.IsCombatDisabled)
                {
                    continue;
                }

                allyCount++;
                if (IsSupportingAnchor(teammate, anchor, arenaRadius))
                {
                    allySupportCount++;
                }
            }
        }

        if (opponents != null)
        {
            for (int i = 0; i < opponents.Count; i++)
            {
                BattleUnitCombatState opponent = opponents[i];
                if (opponent == null || opponent.IsCombatDisabled || opponent == anchor)
                {
                    continue;
                }

                enemyCount++;
                if (Distance2D(opponent.Position, anchor.Position) <= arenaRadius * 0.35f)
                {
                    enemySupportCount++;
                }
            }
        }

        float allySupportPressure = allyCount > 0 ? Mathf.Clamp01(allySupportCount / (float)allyCount) : 0f;
        float enemyFocusPressure = 0f;
        float enemyIsolation = 0f;
        float enemyRetreatSignal = 0f;

        if (isEnemyAnchor)
        {
            int alliedFocusCount = 0;
            int alliedLivingCount = 0;
            if (teammates != null)
            {
                for (int i = 0; i < teammates.Count; i++)
                {
                    BattleUnitCombatState teammate = teammates[i];
                    if (teammate == null || teammate.IsCombatDisabled)
                    {
                        continue;
                    }

                    alliedLivingCount++;
                    if (teammate.PlannedTargetEnemy == anchor)
                    {
                        alliedFocusCount++;
                    }
                }
            }

            alliedLivingCount++; // self
            if (self.PlannedTargetEnemy == anchor)
            {
                alliedFocusCount++;
            }

            enemyFocusPressure =
                alliedLivingCount > 0 ? Mathf.Clamp01(alliedFocusCount / (float)alliedLivingCount) : 0f;
            enemyIsolation = enemyCount > 0 ? Mathf.Clamp01(1f - (enemySupportCount / (float)enemyCount)) : 1f;

            Vector3 toSelf = self.Position - anchor.Position;
            toSelf.y = 0f;
            Vector3 anchorMove = anchor.HasPlannedDesiredPosition
                ? anchor.PlannedDesiredPosition - anchor.Position
                : Vector3.zero;
            anchorMove.y = 0f;
            if (anchorMove.sqrMagnitude > Epsilon && toSelf.sqrMagnitude > Epsilon)
            {
                enemyRetreatSignal = Mathf.Clamp01(Vector3.Dot(anchorMove.normalized, -toSelf.normalized));
            }
        }
        else
        {
            int enemyFocusCount = 0;
            int livingOpponentCount = 0;
            if (opponents != null)
            {
                for (int i = 0; i < opponents.Count; i++)
                {
                    BattleUnitCombatState opponent = opponents[i];
                    if (opponent == null || opponent.IsCombatDisabled)
                    {
                        continue;
                    }

                    livingOpponentCount++;
                    if (opponent.PlannedTargetEnemy == anchor)
                    {
                        enemyFocusCount++;
                    }
                }
            }

            enemyFocusPressure =
                livingOpponentCount > 0 ? Mathf.Clamp01(enemyFocusCount / (float)livingOpponentCount) : 0f;
        }

        return new GladiatorAnchorRelationFeatures(
            allySupportPressure,
            enemyFocusPressure,
            enemyIsolation,
            enemyRetreatSignal
        );
    }

    private static bool IsSupportingAnchor(BattleUnitCombatState unit, BattleUnitCombatState anchor, float arenaRadius)
    {
        if (unit == null || anchor == null)
        {
            return false;
        }

        if (unit.PlannedTargetEnemy == anchor)
        {
            return true;
        }

        return Distance2D(unit.Position, anchor.Position) <= arenaRadius * 0.3f;
    }

    private static float Distance2D(Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        delta.y = 0f;
        return delta.magnitude;
    }
}
