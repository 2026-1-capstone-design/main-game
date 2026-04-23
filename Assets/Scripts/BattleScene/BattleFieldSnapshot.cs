using System.Collections.Generic;
using UnityEngine;

// BattlePlanningSystem 한 틱에서 공유하는 전장 스냅샷.
// 고비용 탐색 결과를 유닛별로 캐싱해 같은 틱 내 중복 계산을 줄인다.
public sealed class BattleFieldSnapshot
{
    private static readonly List<BattleUnitCombatState> _allyStateBuffer = new(6);
    private static readonly List<BattleUnitCombatState> _enemyStateBuffer = new(6);
    private static readonly List<BattleUnitView> _allyViewBuffer = new(6);
    private static readonly List<BattleUnitView> _enemyViewBuffer = new(6);

    private readonly BattleParameterRadii _radii;
    private readonly IReadOnlyList<BattleUnitCombatState> _livingAllies;
    private readonly IReadOnlyList<BattleUnitCombatState> _livingEnemies;
    private readonly IReadOnlyList<BattleUnitView> _livingAllyViews;
    private readonly IReadOnlyList<BattleUnitView> _livingEnemyViews;

    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestIsolatedEnemyCache = new(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _bestBacklineEnemyCache = new(12);
    private readonly Dictionary<BattleUnitCombatState, BattleUnitCombatState> _mostPressuredAllyCache = new(12);
    private readonly Dictionary<BattleUnitCombatState, Vector3> _enemyPressureCenterCache = new(12);

    public IReadOnlyList<BattleUnitCombatState> AllLiving { get; }
    public Vector3 AllyTeamCenter { get; }
    public Vector3 EnemyTeamCenter { get; }
    public float EscapeTowardTeamBlend { get; }

    private BattleFieldSnapshot(
        BattleParameterRadii radii,
        float escapeTowardTeamBlend,
        IReadOnlyList<BattleUnitCombatState> livingAllies,
        IReadOnlyList<BattleUnitCombatState> livingEnemies,
        IReadOnlyList<BattleUnitView> livingAllyViews,
        IReadOnlyList<BattleUnitView> livingEnemyViews
    )
    {
        _radii = radii;
        EscapeTowardTeamBlend = escapeTowardTeamBlend;
        _livingAllies = livingAllies;
        _livingEnemies = livingEnemies;
        _livingAllyViews = livingAllyViews;
        _livingEnemyViews = livingEnemyViews;

        AllyTeamCenter = BattleParameterComputer.ComputeTeamCenter(_livingAllyViews, Vector3.zero);
        EnemyTeamCenter = BattleParameterComputer.ComputeTeamCenter(_livingEnemyViews, Vector3.zero);
        AllLiving = MergeTeams(_livingAllies, _livingEnemies);
    }

    public static BattleFieldSnapshot Build(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleParameterRadii radii,
        float escapeTowardTeamBlend
    )
    {
        _allyStateBuffer.Clear();
        _enemyStateBuffer.Clear();
        _allyViewBuffer.Clear();
        _enemyViewBuffer.Clear();

        if (units != null)
        {
            for (int i = 0; i < units.Count; i++)
            {
                BattleRuntimeUnit unit = units[i];
                BattleUnitCombatState state = unit != null ? unit.State : null;
                if (!BattleFieldQueryHelper.IsLiving(state))
                    continue;

                if (state.IsEnemy)
                {
                    _enemyStateBuffer.Add(state);
                    _enemyViewBuffer.Add(BattleUnitView.From(state));
                }
                else
                {
                    _allyStateBuffer.Add(state);
                    _allyViewBuffer.Add(BattleUnitView.From(state));
                }
            }
        }

        return new BattleFieldSnapshot(
            radii,
            escapeTowardTeamBlend,
            _allyStateBuffer.ToArray(),
            _enemyStateBuffer.ToArray(),
            _allyViewBuffer.ToArray(),
            _enemyViewBuffer.ToArray()
        );
    }

    public IReadOnlyList<BattleUnitCombatState> GetLivingAllies(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitCombatState>();
        return requester.IsEnemy ? _livingEnemies : _livingAllies;
    }

    public IReadOnlyList<BattleUnitCombatState> GetLivingEnemies(BattleUnitCombatState requester)
    {
        if (requester == null)
            return System.Array.Empty<BattleUnitCombatState>();
        return requester.IsEnemy ? _livingAllies : _livingEnemies;
    }

    public Vector3 ComputeTeamCenter(bool isEnemyTeam) => isEnemyTeam ? EnemyTeamCenter : AllyTeamCenter;

    public BattleUnitCombatState FindNearestLivingEnemy(BattleUnitCombatState requester) =>
        BattleFieldQueryHelper.FindNearestLivingEnemy(requester, GetLivingEnemies(requester));

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

        Vector3 enemyCenter = ComputeTeamCenter(!self.IsEnemy);
        IReadOnlyList<BattleUnitCombatState> enemies = GetLivingEnemies(self);
        IReadOnlyList<BattleUnitView> enemyViews = GetLivingEnemyViews(self);
        BattleUnitView selfView = BattleUnitView.From(self);

        BattleUnitCombatState best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < enemies.Count; i++)
        {
            BattleUnitCombatState enemy = enemies[i];
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
            if (!BattleFieldQueryHelper.IsValidSameTeamAlly(self, ally))
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
            if (!BattleFieldQueryHelper.IsValidEnemyTarget(self, enemy))
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
            if (!BattleFieldQueryHelper.IsValidEnemyTarget(self, enemy))
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

        Vector3 center = weightSum <= 0.0001f ? ComputeTeamCenter(!self.IsEnemy) : weightedSum / weightSum;
        _enemyPressureCenterCache[self] = center;
        return center;
    }

    private IReadOnlyList<BattleUnitView> GetLivingEnemyViews(BattleUnitCombatState requester) =>
        requester != null && requester.IsEnemy ? _livingAllyViews : _livingEnemyViews;

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

    private static IReadOnlyList<BattleUnitCombatState> MergeTeams(
        IReadOnlyList<BattleUnitCombatState> allies,
        IReadOnlyList<BattleUnitCombatState> enemies
    )
    {
        var merged = new BattleUnitCombatState[allies.Count + enemies.Count];
        int index = 0;
        for (int i = 0; i < allies.Count; i++)
            merged[index++] = allies[i];
        for (int i = 0; i < enemies.Count; i++)
            merged[index++] = enemies[i];
        return merged;
    }
}
