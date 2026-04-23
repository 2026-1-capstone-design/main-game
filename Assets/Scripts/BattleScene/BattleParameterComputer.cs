using System.Collections.Generic;
using UnityEngine;

// 유닛의 RAW 파라미터 9개를 순수 데이터(BattleUnitView)로부터 계산하는 정적 클래스.
// 전역 상태(MonoBehaviour 등)에 의존하지 않으므로 Edit Mode 단위 테스트 가능.
public static class BattleParameterComputer
{
    // allies: self와 같은 팀, self 제외 / enemies: 상대 팀
    public static BattleParameterSet Compute(
        BattleUnitView self,
        IReadOnlyList<BattleUnitView> allies,
        IReadOnlyList<BattleUnitView> enemies,
        BattleParameterRadii radii)
    {
        BattleParameterSet p = default;
        p.SelfHpLow = ComputeSelfHpLow(self);
        p.SelfSurroundedByEnemies = ComputeSelfSurroundedByEnemies(self, enemies, radii.surroundRadius);
        p.LowHealthAllyProximity = ComputeLowHealthAllyProximity(self, allies, radii.helpRadius);
        p.AllyUnderFocusPressure = ComputeAllyUnderFocusPressure(self, allies, enemies, radii.peelRadius);
        p.AllyFrontlineGap = ComputeAllyFrontlineGap(allies, radii.frontlineGapRadius);
        p.IsolatedEnemyVulnerability = ComputeIsolatedEnemyVulnerability(self, enemies, radii);
        p.EnemyClusterDensity = ComputeEnemyClusterDensity(enemies, radii.clusterRadius);
        p.DistanceToTeamCenter = ComputeDistanceToTeamCenter(self, allies, radii.teamCenterDistanceRadius);
        p.SelfCanAttackNow = ComputeSelfCanAttackNow(self, enemies);
        p.Clamp01All();
        return p;
    }

    // ── 파라미터 계산 ──────────────────────────────────────────────────

    private static float ComputeSelfHpLow(BattleUnitView self)
    {
        if (self.MaxHealth <= 0f)
            return 0f;
        return Mathf.Clamp01(1f - (self.CurrentHealth / self.MaxHealth));
    }

    private static float ComputeSelfSurroundedByEnemies(BattleUnitView self, IReadOnlyList<BattleUnitView> enemies, float surroundRadius)
    {
        float sum = 0f;
        for (int i = 0; i < enemies.Count; i++)
            sum += QuadraticCloseFalloff(Vector3.Distance(self.Position, enemies[i].Position), surroundRadius);
        return Mathf.Clamp01(sum / 3f);
    }

    private static float ComputeLowHealthAllyProximity(BattleUnitView self, IReadOnlyList<BattleUnitView> allies, float helpRadius)
    {
        float sum = 0f;
        for (int i = 0; i < allies.Count; i++)
        {
            float hpLow = ComputeSelfHpLow(allies[i]);
            float distWeight = LinearFalloff(Vector3.Distance(self.Position, allies[i].Position), helpRadius);
            sum += hpLow * distWeight;
        }
        return Mathf.Clamp01(sum / 2f);
    }

    private static float ComputeAllyUnderFocusPressure(BattleUnitView self, IReadOnlyList<BattleUnitView> allies, IReadOnlyList<BattleUnitView> enemies, float peelRadius)
    {
        float best = 0f;
        for (int i = 0; i < allies.Count; i++)
        {
            BattleUnitView ally = allies[i];
            int focusCount = CountUnitsTargeting(ally.UnitNumber, enemies);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpFactor = 0.5f + 0.5f * ComputeSelfHpLow(ally);
            float distWeight = LinearFalloff(Vector3.Distance(self.Position, ally.Position), peelRadius);
            float value = focusRatio * hpFactor * distWeight;
            if (value > best)
                best = value;
        }
        return Mathf.Clamp01(best);
    }

    private static float ComputeAllyFrontlineGap(IReadOnlyList<BattleUnitView> allies, float frontlineGapRadius)
    {
        if (allies.Count <= 1)
            return 0f;

        float sumNearest = 0f;
        int count = 0;
        for (int i = 0; i < allies.Count; i++)
        {
            float nearest = float.MaxValue;
            for (int j = 0; j < allies.Count; j++)
            {
                if (i == j)
                    continue;
                float d = Vector3.Distance(allies[i].Position, allies[j].Position);
                if (d < nearest)
                    nearest = d;
            }
            if (nearest < float.MaxValue)
            { sumNearest += nearest; count++; }
        }
        if (count == 0)
            return 0f;
        return Mathf.Clamp01((sumNearest / count) / frontlineGapRadius);
    }

    private static float ComputeIsolatedEnemyVulnerability(BattleUnitView self, IReadOnlyList<BattleUnitView> enemies, BattleParameterRadii radii)
    {
        float best = 0f;
        for (int i = 0; i < enemies.Count; i++)
        {
            float score = ComputeIsolatedEnemyTargetScore(self, enemies[i], enemies, radii);
            if (score > best)
                best = score;
        }
        return Mathf.Clamp01(best);
    }

    private static float ComputeEnemyClusterDensity(IReadOnlyList<BattleUnitView> enemies, float clusterRadius)
    {
        if (enemies.Count <= 1)
            return 0f;
        float sum = 0f;
        int pairCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            for (int j = i + 1; j < enemies.Count; j++)
            {
                sum += LinearFalloff(Vector3.Distance(enemies[i].Position, enemies[j].Position), clusterRadius);
                pairCount++;
            }
        }
        if (pairCount == 0)
            return 0f;
        return Mathf.Clamp01(sum / pairCount);
    }

    private static float ComputeDistanceToTeamCenter(BattleUnitView self, IReadOnlyList<BattleUnitView> allies, float teamCenterDistanceRadius)
    {
        Vector3 teamCenter = ComputeTeamCenter(allies, self.Position);
        return Mathf.Clamp01(Vector3.Distance(self.Position, teamCenter) / teamCenterDistanceRadius);
    }

    private static float ComputeSelfCanAttackNow(BattleUnitView self, IReadOnlyList<BattleUnitView> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            if (IsWithinEffectiveAttackDistance(self, enemies[i]))
                return 1f;
        }
        return 0f;
    }

    // ── 공개 헬퍼 (BattleFieldView에서도 사용) ──────────────────────────

    // isolated enemy target score - FindBestIsolatedEnemy, FindBestBacklineEnemy에서도 사용
    public static float ComputeIsolatedEnemyTargetScore(
        BattleUnitView self,
        BattleUnitView enemy,
        IReadOnlyList<BattleUnitView> allEnemies,
        BattleParameterRadii radii)
    {
        float nearestSupportDistance = float.MaxValue;
        for (int i = 0; i < allEnemies.Count; i++)
        {
            BattleUnitView other = allEnemies[i];
            if (other.UnitNumber == enemy.UnitNumber || other.IsCombatDisabled)
                continue;
            float d = Vector3.Distance(enemy.Position, other.Position);
            if (d < nearestSupportDistance)
                nearestSupportDistance = d;
        }
        if (nearestSupportDistance == float.MaxValue)
            nearestSupportDistance = radii.isolationRadius;

        float isolation = Mathf.Clamp01(nearestSupportDistance / radii.isolationRadius);
        float hpLow = Mathf.Clamp01(1f - (enemy.CurrentHealth / Mathf.Max(1f, enemy.MaxHealth)));
        float reachFactor = 0.35f + 0.65f * LinearFalloff(Vector3.Distance(self.Position, enemy.Position), radii.assassinReachRadius);
        return isolation * (0.6f + 0.4f * hpLow) * reachFactor;
    }

    public static bool IsWithinEffectiveAttackDistance(BattleUnitView attacker, BattleUnitView target)
    {
        float effectiveRange = attacker.BodyRadius + target.BodyRadius + attacker.AttackRange;
        return Vector3.Distance(attacker.Position, target.Position) <= effectiveRange;
    }

    public static Vector3 ComputeTeamCenter(IReadOnlyList<BattleUnitView> teamUnits, Vector3 fallback)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < teamUnits.Count; i++)
        {
            sum += teamUnits[i].Position;
            count++;
        }
        return count > 0 ? sum / count : fallback;
    }

    // ── 수학 헬퍼 ──────────────────────────────────────────────────────

    public static float LinearFalloff(float distance, float radius)
    {
        if (radius <= 0f)
            return 0f;
        return Mathf.Max(0f, 1f - distance / radius);
    }

    public static float QuadraticCloseFalloff(float distance, float radius)
    {
        float linear = LinearFalloff(distance, radius);
        return linear * linear;
    }

    // ── 내부 헬퍼 ──────────────────────────────────────────────────────

    private static int CountUnitsTargeting(int targetUnitNumber, IReadOnlyList<BattleUnitView> units)
    {
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitView u = units[i];
            if (u.PlannedEnemyTargetNumber == targetUnitNumber || u.CurrentTargetNumber == targetUnitNumber)
                count++;
        }
        return count;
    }
}
