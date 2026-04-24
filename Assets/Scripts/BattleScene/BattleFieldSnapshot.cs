using System.Collections.Generic;
using UnityEngine;

// 전장 상태에 대한 다양한 쿼리를 제공한다.
public sealed class BattleFieldSnapshot
{
    private readonly BattleParameterRadii _radii;
    private readonly BattleUnitCombatState[] _allLivingStates;
    private readonly BattleUnitView[] _allLivingViews;

    private readonly Dictionary<BattleTeamId, BattleUnitView[]> _livingViewsByTeam;
    private readonly Dictionary<BattleTeamId, Vector3> _teamCenterByTeam = new Dictionary<BattleTeamId, Vector3>();
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState[]> _livingAlliesCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState[]
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState[]> _livingEnemiesCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState[]
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitView[]> _livingAllyViewsCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitView[]
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitView[]> _livingEnemyViewsCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitView[]
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestIsolatedEnemyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestBacklineEnemyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _mostPressuredAllyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(12);
    private readonly Dictionary<BattleUnitCombatState, Vector3> _enemyPressureCenterCache = new Dictionary<
        BattleUnitCombatState,
        Vector3
    >(12);

    public IReadOnlyList<BattleUnitCombatState> AllLiving => _allLivingStates;
    public float EscapeTowardTeamBlend { get; }

    private BattleFieldSnapshot(
        BattleParameterRadii radii,
        float escapeTowardTeamBlend,
        BattleUnitCombatState[] allLivingStates,
        BattleUnitView[] allLivingViews,
        Dictionary<BattleTeamId, BattleUnitView[]> livingViewsByTeam
    )
    {
        _radii = radii;
        EscapeTowardTeamBlend = escapeTowardTeamBlend;
        _allLivingStates = allLivingStates ?? System.Array.Empty<BattleUnitCombatState>();
        _allLivingViews = allLivingViews ?? System.Array.Empty<BattleUnitView>();
        _livingViewsByTeam = livingViewsByTeam ?? new Dictionary<BattleTeamId, BattleUnitView[]>();

        foreach (KeyValuePair<BattleTeamId, BattleUnitView[]> pair in _livingViewsByTeam)
        {
            _teamCenterByTeam[pair.Key] = BattleParameterComputer.ComputeTeamCenter(pair.Value, Vector3.zero);
        }
    }

    public static BattleFieldSnapshot Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        float escapeTowardTeamBlend
    )
    {
        List<BattleUnitCombatState> livingStates = new List<BattleUnitCombatState>(12);
        List<BattleUnitView> livingViews = new List<BattleUnitView>(12);
        Dictionary<BattleTeamId, List<BattleUnitView>> viewsByTeam =
            new Dictionary<BattleTeamId, List<BattleUnitView>>();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                BattleRuntimeUnit unit = units[i];
                BattleUnitCombatState state = unit != null ? unit.State : null;
                if (!IsLiving(state))
                {
                    continue;
                }

                livingStates.Add(state);

                BattleUnitView view = BattleUnitView.From(state);
                livingViews.Add(view);

                if (!viewsByTeam.TryGetValue(state.TeamId, out List<BattleUnitView> teamViews))
                {
                    teamViews = new List<BattleUnitView>(6);
                    viewsByTeam[state.TeamId] = teamViews;
                }

                teamViews.Add(view);
            }
        }

        Dictionary<BattleTeamId, BattleUnitView[]> frozenViewsByTeam = new Dictionary<BattleTeamId, BattleUnitView[]>();
        foreach (KeyValuePair<BattleTeamId, List<BattleUnitView>> pair in viewsByTeam)
        {
            frozenViewsByTeam[pair.Key] = pair.Value.ToArray();
        }

        return new BattleFieldSnapshot(
            radii,
            escapeTowardTeamBlend,
            livingStates.ToArray(),
            livingViews.ToArray(),
            frozenViewsByTeam
        );
    }

    public static bool IsValidEnemyTarget(BattleUnitCombatState requester, BattleUnitCombatState candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.TeamId == candidate.TeamId)
            return false;
        return !candidate.IsCombatDisabled;
    }

    public static bool IsValidSameTeamAlly(BattleUnitCombatState requester, BattleUnitCombatState candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.TeamId != candidate.TeamId)
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

    public IReadOnlyList<BattleUnitCombatState> GetLivingAllies(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitCombatState>();

        if (_livingAlliesCache.TryGetValue(requester, out BattleUnitCombatState[] cached))
        {
            return cached;
        }

        BattleUnitCombatState[] result = FilterStates(requester, allies: true);
        _livingAlliesCache[requester] = result;
        return result;
    }

    public IReadOnlyList<BattleUnitCombatState> GetLivingEnemies(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitCombatState>();

        if (_livingEnemiesCache.TryGetValue(requester, out BattleUnitCombatState[] cached))
        {
            return cached;
        }

        BattleUnitCombatState[] result = FilterStates(requester, allies: false);
        _livingEnemiesCache[requester] = result;
        return result;
    }

    public IReadOnlyList<BattleUnitView> GetLivingAllyViews(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitView>();

        if (_livingAllyViewsCache.TryGetValue(requester, out BattleUnitView[] cached))
        {
            return cached;
        }

        BattleUnitView[] result = FilterViews(requester, allies: true);
        _livingAllyViewsCache[requester] = result;
        return result;
    }

    public IReadOnlyList<BattleUnitView> GetLivingEnemyViews(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitView>();

        if (_livingEnemyViewsCache.TryGetValue(requester, out BattleUnitView[] cached))
        {
            return cached;
        }

        BattleUnitView[] result = FilterViews(requester, allies: false);
        _livingEnemyViewsCache[requester] = result;
        return result;
    }

    public Vector3 ComputeTeamCenter(BattleTeamId teamId) =>
        _teamCenterByTeam.TryGetValue(teamId, out Vector3 center) ? center : Vector3.zero;

    public BattleUnitCombatState FindNearestLivingEnemy(BattleUnitCombatState requester) =>
        FindNearestEnemy(requester, GetLivingEnemies(requester));

    public BattleUnitCombatState FindNearestLivingAlly(BattleUnitCombatState requester) =>
        FindNearestAllyInList(requester, GetLivingAllies(requester));

    public BattleUnitCombatState FindBestIsolatedEnemy(BattleUnitCombatState self)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        if (_bestIsolatedEnemyCache.TryGetValue(self, out BattleUnitCombatState cached))
            return cached;

        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        IReadOnlyList<BattleUnitView> enemyViews = GetLivingEnemyViews(self);
        BattleUnitView selfView = BattleUnitView.From(self);

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            float score = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(
                selfView,
                BattleUnitView.From(enemy),
                enemyViews,
                _radii
            );
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        _bestIsolatedEnemyCache[self] = best;
        return best;
    }

    public BattleUnitCombatState FindBestBacklineEnemy(BattleUnitCombatState self)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        if (_bestBacklineEnemyCache.TryGetValue(self, out BattleUnitCombatState cached))
            return cached;

        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        IReadOnlyList<BattleUnitView> enemyViews = GetLivingEnemyViews(self);
        BattleUnitView selfView = BattleUnitView.From(self);

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            Vector3 enemyCenter = ComputeTeamCenter(enemy.TeamId);
            float hpLow = enemy.MaxHealth > 0f ? Mathf.Clamp01(1f - (enemy.CurrentHealth / enemy.MaxHealth)) : 0f;
            float isolation = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(
                selfView,
                BattleUnitView.From(enemy),
                enemyViews,
                _radii
            );
            float backlineFactor = Mathf.Clamp01(
                Vector3.Distance(enemy.Position, enemyCenter) / _radii.teamCenterDistanceRadius
            );
            float score = hpLow * 0.45f + isolation * 0.35f + backlineFactor * 0.20f;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        _bestBacklineEnemyCache[self] = best;
        return best;
    }

    public BattleUnitCombatState FindMostPressuredAlly(BattleUnitCombatState self)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        if (_mostPressuredAllyCache.TryGetValue(self, out BattleUnitCombatState cached))
            return cached;

        IReadOnlyList<BattleUnitCombatState> allies = GetLivingAllies(self);
        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleUnitCombatState ally = allies[i];
            if (!IsValidSameTeamAlly(self, ally))
                continue;

            int focusCount = CountEnemiesTargeting(ally, enemies);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpLow = ally.MaxHealth > 0f ? Mathf.Clamp01(1f - (ally.CurrentHealth / ally.MaxHealth)) : 0f;
            float hpFactor = 0.5f + 0.5f * hpLow;
            float distWeight = BattleParameterComputer.LinearFalloff(
                Vector3.Distance(self.Position, ally.Position),
                _radii.peelRadius
            );
            float score = focusRatio * hpFactor * distWeight;

            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        _mostPressuredAllyCache[self] = best;
        return best;
    }

    public BattleUnitCombatState FindBestPeelEnemy(BattleUnitCombatState self, BattleUnitCombatState protectedAlly)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        if (protectedAlly == null)
            return FindNearestLivingEnemy(self);

        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            if (enemy.CurrentTarget != protectedAlly && enemy.PlannedTargetEnemy != protectedAlly)
                continue;

            float dist = Vector3.Distance(enemy.Position, protectedAlly.Position);
            float score = 1f - Mathf.Clamp01(dist / _radii.peelRadius);
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best ?? FindNearestLivingEnemy(self);
    }

    public BattleUnitCombatState FindEnemyClosestToPoint(BattleUnitCombatState self, Vector3 point)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        BattleUnitCombatState best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            float distance = Vector3.Distance(enemy.Position, point);
            if (distance < bestDist)
            {
                bestDist = distance;
                best = enemy;
            }
        }

        return best;
    }

    public Vector3 ComputeEnemyPressureCenter(BattleUnitCombatState self)
    {
        if (self == null || self.IsCombatDisabled)
            return Vector3.zero;

        if (_enemyPressureCenterCache.TryGetValue(self, out Vector3 cached))
            return cached;

        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        Vector3 weightedSum = Vector3.zero;
        float weightSum = 0f;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            float distance = Vector3.Distance(self.Position, enemy.Position);
            float weight = BattleParameterComputer.QuadraticCloseFalloff(distance, _radii.surroundRadius);
            weightedSum += enemy.Position * weight;
            weightSum += weight;
        }

        Vector3 center = weightSum <= 0.0001f ? ComputeHostileCenter(self) : weightedSum / weightSum;
        _enemyPressureCenterCache[self] = center;
        return center;
    }

    private static bool IsLiving(BattleUnitCombatState unit) => unit != null && !unit.IsCombatDisabled;

    private BattleUnitCombatState[] FilterStates(BattleUnitCombatState requester, bool allies)
    {
        List<BattleUnitCombatState> result = new List<BattleUnitCombatState>(_allLivingStates.Length);

        for (int i = 0; i < _allLivingStates.Length; i++)
        {
            BattleUnitCombatState candidate = _allLivingStates[i];
            if (!IsLiving(candidate))
            {
                continue;
            }

            bool matches = allies
                ? requester.TeamId == candidate.TeamId
                : requester.TeamId != candidate.TeamId;

            if (matches)
            {
                result.Add(candidate);
            }
        }

        return result.ToArray();
    }

    private BattleUnitView[] FilterViews(BattleUnitCombatState requester, bool allies)
    {
        List<BattleUnitView> result = new List<BattleUnitView>(_allLivingViews.Length);

        for (int i = 0; i < _allLivingViews.Length; i++)
        {
            BattleUnitView candidate = _allLivingViews[i];
            bool matches = allies
                ? requester.TeamId == candidate.TeamId
                : requester.TeamId != candidate.TeamId;

            if (matches)
            {
                result.Add(candidate);
            }
        }

        return result.ToArray();
    }

    private Vector3 ComputeHostileCenter(BattleUnitCombatState requester)
    {
        IReadOnlyList<BattleUnitView> hostileViews = GetLivingEnemyViews(requester);
        return BattleParameterComputer.ComputeTeamCenter(hostileViews, Vector3.zero);
    }

    private static BattleUnitCombatState FindNearestEnemy(
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

    private static BattleUnitCombatState FindNearestAllyInList(
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

    private static int CountEnemiesTargeting(BattleUnitCombatState ally, IReadOnlyList<BattleUnitCombatState> enemies)
    {
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
            if (enemy.CurrentTarget == ally || enemy.PlannedTargetEnemy == ally)
                count++;
        }
        return count;
    }
}
