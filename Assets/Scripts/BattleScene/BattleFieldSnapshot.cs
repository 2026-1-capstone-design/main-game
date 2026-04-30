using System.Collections.Generic;
using UnityEngine;

// 전장 상태에 대한 다양한 쿼리를 제공한다.
// 각 BattleSimulationManager가 자기 Snapshot 인스턴스를 재사용한다.
public sealed class BattleFieldSnapshot
{
    private BattleParameterRadii _radii;
    private readonly List<BattleUnitCombatState> _allLivingStates = new List<BattleUnitCombatState>(
        BattleTeamConstants.MaxUnitsInBattle
    );
    private readonly List<BattleUnitView> _allLivingViews = new List<BattleUnitView>(
        BattleTeamConstants.MaxUnitsInBattle
    );

    private readonly Dictionary<BattleTeamId, List<BattleUnitView>> _livingViewsByTeam =
        new Dictionary<BattleTeamId, List<BattleUnitView>>();
    private readonly Dictionary<BattleTeamId, List<BattleUnitView>> _hostileViewsByTeam =
        new Dictionary<BattleTeamId, List<BattleUnitView>>();
    private readonly Dictionary<BattleTeamId, Vector3> _teamCenterByTeam = new Dictionary<BattleTeamId, Vector3>();
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestIsolatedEnemyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(BattleTeamConstants.MaxUnitsInBattle);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestBacklineEnemyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(BattleTeamConstants.MaxUnitsInBattle);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _mostPressuredAllyCache = new Dictionary<
        BattleUnitCombatState,
        BattleUnitCombatState
    >(BattleTeamConstants.MaxUnitsInBattle);
    private readonly Dictionary<BattleUnitCombatState, Vector3> _enemyPressureCenterCache = new Dictionary<
        BattleUnitCombatState,
        Vector3
    >(BattleTeamConstants.MaxUnitsInBattle);
    private readonly Dictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeByState = new Dictionary<
        BattleUnitCombatState,
        BattleRuntimeUnit
    >(BattleTeamConstants.MaxUnitsInBattle);
    private IBattleTargetingPolicy _targetingPolicy = DefaultBattleTargetingPolicy.Instance;

    public IReadOnlyList<BattleUnitCombatState> AllLiving => _allLivingStates;
    public float EscapeTowardTeamBlend { get; private set; }

    private BattleFieldSnapshot() { }

    public static BattleFieldSnapshot Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        float escapeTowardTeamBlend,
        BattleFieldSnapshot reusableSnapshot = null,
        IBattleTargetingPolicy targetingPolicy = null
    )
    {
        BattleFieldSnapshot snapshot = reusableSnapshot ?? new BattleFieldSnapshot();
        snapshot.Rebuild(units, radii, escapeTowardTeamBlend, targetingPolicy);
        return snapshot;
    }

    public void Reset()
    {
        ResetForReuse();
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
        float range = GetEffectiveAttackDistance(attacker, target) + 0.05f;
        return delta.sqrMagnitude <= range * range;
    }

    public bool CanTarget(
        BattleUnitCombatState requester,
        BattleUnitCombatState candidate,
        BattleTargetingReason reason = BattleTargetingReason.Planner
    )
    {
        if (!IsValidEnemyTarget(requester, candidate))
            return false;

        return _targetingPolicy.CanTarget(ResolveRuntime(requester), ResolveRuntime(candidate), reason);
    }

    public float ModifyTargetScore(
        BattleUnitCombatState requester,
        BattleUnitCombatState candidate,
        float baseScore,
        BattleTargetingReason reason = BattleTargetingReason.Planner
    )
    {
        if (!IsValidEnemyTarget(requester, candidate))
            return baseScore;

        return _targetingPolicy.ModifyTargetScore(
            ResolveRuntime(requester),
            ResolveRuntime(candidate),
            baseScore,
            reason
        );
    }

    public void GetLivingAllies(BattleUnitCombatState requester, List<BattleUnitCombatState> result)
    {
        if (result == null)
            return;

        FilterStates(requester, allies: true, result);
    }

    public void GetLivingEnemies(BattleUnitCombatState requester, List<BattleUnitCombatState> result)
    {
        if (result == null)
            return;

        FilterStates(requester, allies: false, result);
    }

    public void GetLivingAllyViews(BattleUnitCombatState requester, List<BattleUnitView> result)
    {
        if (result == null)
            return;

        FilterViews(requester, allies: true, result);
    }

    public void GetLivingEnemyViews(BattleUnitCombatState requester, List<BattleUnitView> result)
    {
        if (result == null)
            return;

        FilterViews(requester, allies: false, result);
    }

    public Vector3 ComputeTeamCenter(BattleTeamId teamId) =>
        _teamCenterByTeam.TryGetValue(teamId, out Vector3 center) ? center : Vector3.zero;

    public BattleUnitCombatState FindNearestLivingEnemy(BattleUnitCombatState requester)
    {
        if (requester == null || requester.IsCombatDisabled)
            return null;

        BattleUnitCombatState nearest = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState candidate = _allLivingStates[i];
            if (!CanTarget(requester, candidate, BattleTargetingReason.Planner))
                continue;

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float score = ModifyTargetScore(requester, candidate, -delta.sqrMagnitude, BattleTargetingReason.Planner);
            if (score > bestScore)
            {
                bestScore = score;
                nearest = candidate;
            }
        }

        return nearest;
    }

    public BattleUnitCombatState FindNearestLivingAlly(BattleUnitCombatState requester)
    {
        if (requester == null || requester.IsCombatDisabled)
            return null;

        BattleUnitCombatState nearest = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState candidate = _allLivingStates[i];
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

    public BattleUnitCombatState FindBestIsolatedEnemy(BattleUnitCombatState self)
    {
        if (self == null || self.IsCombatDisabled)
            return null;

        if (_bestIsolatedEnemyCache.TryGetValue(self, out BattleUnitCombatState cached))
            return cached;

        BattleUnitView selfView = BattleUnitView.From(self);
        IReadOnlyList<BattleUnitView> enemyViews = GetLivingViews(self.TeamId, allies: false);

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(self, enemy, BattleTargetingReason.Planner))
                continue;

            float score = BattleParameterComputer.ComputeIsolatedEnemyTargetScore(
                selfView,
                BattleUnitView.From(enemy),
                enemyViews,
                _radii
            );
            score = ModifyTargetScore(self, enemy, score, BattleTargetingReason.Planner);
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

        BattleUnitView selfView = BattleUnitView.From(self);
        IReadOnlyList<BattleUnitView> enemyViews = GetLivingViews(self.TeamId, allies: false);

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(self, enemy, BattleTargetingReason.Planner))
                continue;

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
            score = ModifyTargetScore(self, enemy, score, BattleTargetingReason.Planner);

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

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState ally = _allLivingStates[i];
            if (!IsValidSameTeamAlly(self, ally))
                continue;

            int focusCount = CountEnemiesTargeting(self, ally);
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

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(self, enemy, BattleTargetingReason.Planner))
                continue;
            if (enemy.CurrentTarget != protectedAlly && enemy.PlannedTargetEnemy != protectedAlly)
                continue;

            float dist = Vector3.Distance(enemy.Position, protectedAlly.Position);
            float score = 1f - Mathf.Clamp01(dist / _radii.peelRadius);
            score = ModifyTargetScore(self, enemy, score, BattleTargetingReason.Planner);
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

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(self, enemy, BattleTargetingReason.Planner))
                continue;
            float distance = Vector3.Distance(enemy.Position, point);
            float score = ModifyTargetScore(self, enemy, -distance, BattleTargetingReason.Planner);
            if (score > bestScore)
            {
                bestScore = score;
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

        Vector3 weightedSum = Vector3.zero;
        float weightSum = 0f;

        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(self, enemy, BattleTargetingReason.Planner))
                continue;

            float distance = Vector3.Distance(self.Position, enemy.Position);
            float weight = BattleParameterComputer.QuadraticCloseFalloff(distance, _radii.surroundRadius);
            weightedSum += enemy.Position * weight;
            weightSum += weight;
        }

        Vector3 center = weightSum <= 0.0001f ? ComputeHostileCenter(self) : weightedSum / weightSum;
        _enemyPressureCenterCache[self] = center;
        return center;
    }

    private void Rebuild(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        float escapeTowardTeamBlend,
        IBattleTargetingPolicy targetingPolicy
    )
    {
        ResetForReuse();

        _radii = radii;
        EscapeTowardTeamBlend = escapeTowardTeamBlend;
        _targetingPolicy = targetingPolicy ?? DefaultBattleTargetingPolicy.Instance;

        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            BattleUnitCombatState state = unit != null ? unit.State : null;
            if (!IsLiving(state))
                continue;

            _allLivingStates.Add(state);
            _runtimeByState[state] = unit;

            BattleUnitView view = BattleUnitView.From(state);
            _allLivingViews.Add(view);
            GetOrCreateViewList(_livingViewsByTeam, state.TeamId, BattleTeamConstants.MaxUnitsPerTeam).Add(view);
        }

        BuildHostileViews();
        RecomputeTeamCenters();
    }

    private void ResetForReuse()
    {
        _radii = default;
        EscapeTowardTeamBlend = 0f;
        _allLivingStates.Clear();
        _allLivingViews.Clear();
        ClearViewDictionary(_livingViewsByTeam);
        ClearViewDictionary(_hostileViewsByTeam);
        _teamCenterByTeam.Clear();
        _bestIsolatedEnemyCache.Clear();
        _bestBacklineEnemyCache.Clear();
        _mostPressuredAllyCache.Clear();
        _enemyPressureCenterCache.Clear();
        _runtimeByState.Clear();
        _targetingPolicy = DefaultBattleTargetingPolicy.Instance;
    }

    private static bool IsLiving(BattleUnitCombatState unit) => unit != null && !unit.IsCombatDisabled;

    private void FilterStates(BattleUnitCombatState requester, bool allies, List<BattleUnitCombatState> result)
    {
        result.Clear();
        if (requester == null)
            return;

        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState candidate = _allLivingStates[i];
            bool matches = allies ? requester.TeamId == candidate.TeamId : requester.TeamId != candidate.TeamId;
            if (!allies && !CanTarget(requester, candidate, BattleTargetingReason.Planner))
                matches = false;
            if (matches)
                result.Add(candidate);
        }
    }

    private void FilterViews(BattleUnitCombatState requester, bool allies, List<BattleUnitView> result)
    {
        result.Clear();
        if (requester == null)
            return;

        IReadOnlyList<BattleUnitView> source = GetLivingViews(requester.TeamId, allies);
        for (int i = 0; i < source.Count; i++)
        {
            result.Add(source[i]);
        }
    }

    private Vector3 ComputeHostileCenter(BattleUnitCombatState requester) =>
        BattleParameterComputer.ComputeTeamCenter(GetLivingViews(requester.TeamId, allies: false), Vector3.zero);

    private IReadOnlyList<BattleUnitView> GetLivingViews(BattleTeamId requesterTeamId, bool allies)
    {
        Dictionary<BattleTeamId, List<BattleUnitView>> source = allies ? _livingViewsByTeam : _hostileViewsByTeam;
        if (source.TryGetValue(requesterTeamId, out List<BattleUnitView> views))
            return views;

        return System.Array.Empty<BattleUnitView>();
    }

    private int CountEnemiesTargeting(BattleUnitCombatState requester, BattleUnitCombatState ally)
    {
        int count = 0;
        for (int i = 0; i < _allLivingStates.Count; i++)
        {
            BattleUnitCombatState enemy = _allLivingStates[i];
            if (!CanTarget(requester, enemy, BattleTargetingReason.Planner))
                continue;

            if (enemy.CurrentTarget == ally || enemy.PlannedTargetEnemy == ally)
                count++;
        }
        return count;
    }

    private void BuildHostileViews()
    {
        foreach (KeyValuePair<BattleTeamId, List<BattleUnitView>> pair in _livingViewsByTeam)
        {
            if (pair.Value.Count == 0)
                continue;

            List<BattleUnitView> hostileViews = GetOrCreateViewList(
                _hostileViewsByTeam,
                pair.Key,
                _allLivingViews.Count
            );
            hostileViews.Clear();

            foreach (KeyValuePair<BattleTeamId, List<BattleUnitView>> otherPair in _livingViewsByTeam)
            {
                if (otherPair.Key.Equals(pair.Key) || otherPair.Value.Count == 0)
                    continue;

                hostileViews.AddRange(otherPair.Value);
            }
        }
    }

    private void RecomputeTeamCenters()
    {
        foreach (KeyValuePair<BattleTeamId, List<BattleUnitView>> pair in _livingViewsByTeam)
        {
            if (pair.Value.Count == 0)
                continue;

            _teamCenterByTeam[pair.Key] = BattleParameterComputer.ComputeTeamCenter(pair.Value, Vector3.zero);
        }
    }

    private static List<BattleUnitView> GetOrCreateViewList(
        Dictionary<BattleTeamId, List<BattleUnitView>> source,
        BattleTeamId teamId,
        int capacity
    )
    {
        if (source.TryGetValue(teamId, out List<BattleUnitView> views))
            return views;

        views = new List<BattleUnitView>(capacity);
        source[teamId] = views;
        return views;
    }

    private static void ClearViewDictionary(Dictionary<BattleTeamId, List<BattleUnitView>> source)
    {
        foreach (KeyValuePair<BattleTeamId, List<BattleUnitView>> pair in source)
        {
            pair.Value.Clear();
        }
    }

    private BattleRuntimeUnit ResolveRuntime(BattleUnitCombatState state)
    {
        if (state == null)
            return null;

        return _runtimeByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
