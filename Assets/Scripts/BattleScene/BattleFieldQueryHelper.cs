using System.Collections.Generic;
using UnityEngine;

// 전장 조회/검증 규칙을 모아둔 stateless 유틸리티.
public static class BattleFieldQueryHelper
{
    public static List<BattleUnitCombatState> GetLivingUnits(
        IReadOnlyList<BattleUnitCombatState> units,
        bool isEnemyTeam
    )
    {
        var result = new List<BattleUnitCombatState>();
        if (units == null)
            return result;

        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitCombatState unit = units[i];
            if (!IsLivingAndOnTeam(unit, isEnemyTeam))
                continue;

            result.Add(unit);
        }

        return result;
    }

    public static bool IsLiving(BattleUnitCombatState unit) => unit != null && !unit.IsCombatDisabled;

    public static bool IsLivingAndOnTeam(BattleUnitCombatState unit, bool isEnemyTeam) =>
        IsLiving(unit) && unit.IsEnemy == isEnemyTeam;

    public static bool IsValidEnemyTarget(BattleUnitCombatState requester, BattleUnitCombatState candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy == candidate.IsEnemy)
            return false;
        return !candidate.IsCombatDisabled;
    }

    public static bool IsValidSameTeamAlly(BattleUnitCombatState requester, BattleUnitCombatState candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy != candidate.IsEnemy)
            return false;
        return !candidate.IsCombatDisabled;
    }

    public static float GetEffectiveAttackDistance(BattleUnitCombatState attacker, BattleUnitCombatState target)
    {
        if (attacker == null || target == null)
            return 0f;
        return attacker.BodyRadius + target.BodyRadius + attacker.AttackRange;
    }

    public static bool IsWithinEffectiveAttackDistance(BattleUnitCombatState attacker, BattleUnitCombatState target)
    {
        if (attacker == null || target == null)
            return false;
        Vector3 delta = attacker.Position - target.Position;
        delta.y = 0f;
        return delta.magnitude <= (GetEffectiveAttackDistance(attacker, target) + 0.05f);
    }

    public static BattleUnitCombatState FindNearestLivingEnemy(
        BattleUnitCombatState requester,
        IReadOnlyList<BattleUnitCombatState> candidates
    )
    {
        if (requester == null || requester.IsCombatDisabled || candidates == null)
            return null;

        BattleUnitCombatState nearest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            BattleUnitCombatState candidate = candidates[i];
            if (!IsValidEnemyTarget(requester, candidate))
                continue;

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    public static BattleUnitCombatState FindNearestLivingAlly(
        BattleUnitCombatState requester,
        IReadOnlyList<BattleUnitCombatState> candidates
    )
    {
        if (requester == null || requester.IsCombatDisabled || candidates == null)
            return null;

        BattleUnitCombatState nearest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            BattleUnitCombatState candidate = candidates[i];
            if (!IsValidSameTeamAlly(requester, candidate))
                continue;

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
