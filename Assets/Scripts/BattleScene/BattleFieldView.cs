using System.Collections.Generic;
using UnityEngine;

// 플랜 빌더와 스킬이 전장 상태를 조회하기 위한 컨텍스트 객체.
// BattleSimulationManager가 초기화 시 생성하고 매 틱 재사용한다.
// _runtimeUnits 리스트 참조를 유지하므로 항상 최신 상태를 반영한다.
public sealed class BattleFieldView
{
    private readonly IReadOnlyList<BattleRuntimeUnit> _units;
    private readonly BattleParameterRadii _radii;
    public readonly float EscapeTowardTeamBlend;

    public BattleFieldView(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        float escapeTowardTeamBlend)
    {
        _units = units;
        _radii = radii;
        EscapeTowardTeamBlend = escapeTowardTeamBlend;
    }

    // ── 유닛 필터링 ──────────────────────────────────────────────────────

    public List<BattleRuntimeUnit> GetLivingUnits(bool isEnemyTeam)
    {
        var result = new List<BattleRuntimeUnit>();
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit u = _units[i];
            if (u == null || u.IsCombatDisabled || u.IsEnemy != isEnemyTeam)
                continue;
            result.Add(u);
        }
        return result;
    }

    // ── 유효성 검사 ──────────────────────────────────────────────────────

    public bool IsValidEnemyTarget(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy == candidate.IsEnemy)
            return false;
        return !candidate.IsCombatDisabled;
    }

    public bool IsValidSameTeamAlly(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy != candidate.IsEnemy)
            return false;
        return !candidate.IsCombatDisabled;
    }

    public bool IsWithinEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
            return false;
        Vector3 delta = attacker.Position - target.Position;
        delta.y = 0f;
        return delta.magnitude <= (GetEffectiveAttackDistance(attacker, target) + 0.05f);
    }

    public float GetEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
            return 0f;
        return attacker.BodyRadius + target.BodyRadius + attacker.AttackRange;
    }

    // ── 타겟 탐색 ────────────────────────────────────────────────────────

    public BattleRuntimeUnit FindNearestLivingEnemy(BattleRuntimeUnit requester)
    {
        if (requester == null)
            return null;
        BattleRuntimeUnit nearest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit c = _units[i];
            if (!IsValidEnemyTarget(requester, c))
                continue;
            Vector3 delta = c.Position - requester.Position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr)
            { bestSqr = sqr; nearest = c; }
        }
        return nearest;
    }

    public BattleRuntimeUnit FindNearestLivingAlly(BattleRuntimeUnit requester)
    {
        if (requester == null)
            return null;
        BattleRuntimeUnit nearest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit c = _units[i];
            if (c == null || c == requester || c.IsCombatDisabled)
                continue;
            if (c.IsEnemy != requester.IsEnemy)
                continue;
            Vector3 delta = c.Position - requester.Position;
            delta.y = 0f;
            float sqr = delta.sqrMagnitude;
            if (sqr < bestSqr)
            { bestSqr = sqr; nearest = c; }
        }
        return nearest;
    }

    public BattleRuntimeUnit FindBestIsolatedEnemy(BattleRuntimeUnit self)
    {
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;
        var enemyViews = BuildEnemyViews(self);
        BattleUnitView selfView = BattleUnitView.From(self);

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            float score = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(
                selfView, BattleUnitView.From(enemy), enemyViews, _radii);
            if (score > bestScore)
            { bestScore = score; best = enemy; }
        }
        return best;
    }

    public BattleRuntimeUnit FindBestBacklineEnemy(BattleRuntimeUnit self)
    {
        Vector3 enemyCenter = ComputeTeamCenter(!self.IsEnemy);
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;
        var enemyViews = BuildEnemyViews(self);
        BattleUnitView selfView = BattleUnitView.From(self);

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            float hpLow = enemy.MaxHealth > 0f ? Mathf.Clamp01(1f - (enemy.CurrentHealth / enemy.MaxHealth)) : 0f;
            float isolation = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(
                selfView, BattleUnitView.From(enemy), enemyViews, _radii);
            float backlineFactor = Mathf.Clamp01(
                Vector3.Distance(enemy.Position, enemyCenter) / _radii.teamCenterDistanceRadius);
            float score = hpLow * 0.45f + isolation * 0.35f + backlineFactor * 0.20f;
            if (score > bestScore)
            { bestScore = score; best = enemy; }
        }
        return best;
    }

    public BattleRuntimeUnit FindMostPressuredAlly(BattleRuntimeUnit self)
    {
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit ally = _units[i];
            if (!IsValidSameTeamAlly(self, ally))
                continue;
            int focusCount = CountEnemiesTargeting(ally);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpLow = ally.MaxHealth > 0f ? Mathf.Clamp01(1f - (ally.CurrentHealth / ally.MaxHealth)) : 0f;
            float hpFactor = 0.5f + 0.5f * hpLow;
            float distWeight = BattleParameterComputer.LinearFalloff(
                Vector3.Distance(self.Position, ally.Position), _radii.peelRadius);
            float score = focusRatio * hpFactor * distWeight;
            if (score > bestScore)
            { bestScore = score; best = ally; }
        }
        return best;
    }

    public BattleRuntimeUnit FindBestPeelEnemy(BattleRuntimeUnit self, BattleRuntimeUnit protectedAlly)
    {
        if (protectedAlly == null)
            return FindNearestLivingEnemy(self);
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            if (enemy.CurrentTarget != protectedAlly && enemy.PlannedTargetEnemy != protectedAlly)
                continue;
            float dist = Vector3.Distance(enemy.Position, protectedAlly.Position);
            float score = 1f - Mathf.Clamp01(dist / _radii.peelRadius);
            if (score > bestScore)
            { bestScore = score; best = enemy; }
        }
        return best != null ? best : FindNearestLivingEnemy(self);
    }

    public BattleRuntimeUnit FindEnemyClosestToPoint(BattleRuntimeUnit self, Vector3 point)
    {
        BattleRuntimeUnit best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            float d = Vector3.Distance(enemy.Position, point);
            if (d < bestDist)
            { bestDist = d; best = enemy; }
        }
        return best;
    }

    // ── 공간 계산 ────────────────────────────────────────────────────────

    public Vector3 ComputeTeamCenter(bool isEnemyTeam)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit u = _units[i];
            if (u == null || u.IsCombatDisabled || u.IsEnemy != isEnemyTeam)
                continue;
            sum += u.Position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    public Vector3 ComputeEnemyPressureCenter(BattleRuntimeUnit self)
    {
        Vector3 weightedSum = Vector3.zero;
        float weightSum = 0f;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            float d = Vector3.Distance(self.Position, enemy.Position);
            float w = BattleParameterComputer.QuadraticCloseFalloff(d, _radii.surroundRadius);
            weightedSum += enemy.Position * w;
            weightSum += w;
        }
        if (weightSum <= 0.0001f)
            return ComputeTeamCenter(!self.IsEnemy);
        return weightedSum / weightSum;
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────

    private int CountEnemiesTargeting(BattleRuntimeUnit ally)
    {
        int count = 0;
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit enemy = _units[i];
            if (!IsValidEnemyTarget(ally, enemy))
                continue;
            if (enemy.CurrentTarget == ally || enemy.PlannedTargetEnemy == ally)
                count++;
        }
        return count;
    }

    private List<BattleUnitView> BuildEnemyViews(BattleRuntimeUnit self)
    {
        var result = new List<BattleUnitView>();
        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit u = _units[i];
            if (u == null || u.IsCombatDisabled || u.IsEnemy == self.IsEnemy)
                continue;
            result.Add(BattleUnitView.From(u));
        }
        return result;
    }
}
