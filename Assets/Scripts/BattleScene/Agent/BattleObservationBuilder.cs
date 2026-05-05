using System;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public readonly struct GladiatorObservationContext
{
    public readonly BattleUnitCombatState Self;
    public readonly IReadOnlyList<BattleUnitCombatState> Teammates;
    public readonly IReadOnlyList<BattleUnitCombatState> Opponents;
    public readonly GladiatorObservationStats Stats;
    public readonly Vector3 ArenaCenter;
    public readonly float ArenaRadius;
    public readonly float BattleTimeoutRemainingRatio;
    public readonly Vector2 AgentSmoothedWorldMove;
    public readonly Vector2 AgentPreviousRawWorldMove;
    public readonly int AnchorKind;
    public readonly int PathMode;
    public readonly BattleUnitCombatState CurrentAnchor;

    public GladiatorObservationContext(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> teammates,
        IReadOnlyList<BattleUnitCombatState> opponents,
        GladiatorObservationStats stats,
        Vector3 arenaCenter,
        float arenaRadius,
        float battleTimeoutRemainingRatio,
        Vector2 agentSmoothedWorldMove,
        Vector2 agentPreviousRawWorldMove,
        int anchorKind,
        int pathMode,
        BattleUnitCombatState currentAnchor
    )
    {
        Self = self;
        Teammates = teammates ?? Array.Empty<BattleUnitCombatState>();
        Opponents = opponents ?? Array.Empty<BattleUnitCombatState>();
        Stats = stats;
        ArenaCenter = arenaCenter;
        ArenaRadius = arenaRadius;
        BattleTimeoutRemainingRatio = Mathf.Clamp01(battleTimeoutRemainingRatio);
        AgentSmoothedWorldMove = Vector2.ClampMagnitude(agentSmoothedWorldMove, 1f);
        AgentPreviousRawWorldMove = Vector2.ClampMagnitude(agentPreviousRawWorldMove, 1f);
        AnchorKind = anchorKind;
        PathMode = pathMode;
        CurrentAnchor = currentAnchor;
    }
}

public readonly struct GladiatorObservationStats
{
    private const float Epsilon = 1e-6f;

    public readonly float MedianMaxHealth;
    public readonly float MedianAttack;
    public readonly float MaxMoveSpeed;

    public GladiatorObservationStats(float medianMaxHealth, float medianAttack, float maxMoveSpeed)
    {
        MedianMaxHealth = Mathf.Max(Epsilon, medianMaxHealth);
        MedianAttack = Mathf.Max(Epsilon, medianAttack);
        MaxMoveSpeed = Mathf.Max(Epsilon, maxMoveSpeed);
    }
}

public static class BattleObservationBuilder
{
    private const float Epsilon = 1e-6f;
    private const float LogCompressDecadeWindow = 3f;

    public static GladiatorTacticalFeatures BuildTacticalFeatures(GladiatorObservationContext context)
    {
        BattleUnitCombatState self = context.Self;
        BattleUnitCombatState anchor = context.CurrentAnchor;
        if (self == null || anchor == null || anchor.IsCombatDisabled)
        {
            return new GladiatorTacticalFeatures(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
        }

        float distance = Distance2D(self.Position, anchor.Position);
        float selfRange = GetEffectiveRange(self, anchor, self.AttackRange);
        float anchorRange = GetEffectiveRange(anchor, self, anchor.AttackRange);
        Vector2 toAnchor = WorldToObservationAxes(self.TeamId, anchor.Position - self.Position);
        Vector2 forward = toAnchor.sqrMagnitude > Epsilon ? toAnchor.normalized : Vector2.up;

        return new GladiatorTacticalFeatures(
            NormalizeDistance(distance, context.ArenaRadius),
            1f,
            GetDamageToMaxHealthRatio(anchor, self),
            GetDamageToMaxHealthRatio(self, anchor),
            distance <= selfRange ? 1f : 0f,
            distance <= anchorRange ? 1f : 0f,
            SampleLaneFreeRatio(self, context.Opponents, forward, -1f, context.ArenaRadius),
            SampleLaneFreeRatio(self, context.Opponents, forward, 1f, context.ArenaRadius),
            ComputeAllyUnderFocusRatio(context.Teammates, context.Opponents),
            ComputeEnemyClusterPressure(self, context.Opponents, context.ArenaRadius)
        );
    }

    public static void Write(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleUnitCombatState self = context.Self;
        if (self == null || self.IsCombatDisabled)
        {
            AddZeroes(sensor, GladiatorObservationSchema.TotalSize);
            return;
        }

        Vector2 arenaDelta = WorldToObservationAxes(self.TeamId, context.ArenaCenter - self.Position);
        IReadOnlyList<BattleUnitCombatState> teammates = context.Teammates;
        IReadOnlyList<BattleUnitCombatState> opponents = context.Opponents;
        float healthRatio = self.MaxHealth > 0f ? Mathf.Clamp01(self.CurrentHealth / self.MaxHealth) : 1f;
        GladiatorTacticalFeatures features = BuildTacticalFeatures(context);
        float distanceFromCenter = Vector3.Distance(
            new Vector3(self.Position.x, 0f, self.Position.z),
            new Vector3(context.ArenaCenter.x, 0f, context.ArenaCenter.z)
        );
        float boundaryPressure =
            context.ArenaRadius > 0f && context.ArenaRadius < float.MaxValue
                ? Mathf.Clamp01(distanceFromCenter / context.ArenaRadius)
                : 0f;

        sensor.AddObservation(NormalizeSignedByArenaRadius(arenaDelta.x, context.ArenaRadius));
        sensor.AddObservation(NormalizeSignedByArenaRadius(arenaDelta.y, context.ArenaRadius));
        sensor.AddObservation(healthRatio);
        sensor.AddObservation(LogCompress(self.MaxHealth, context.Stats.MedianMaxHealth));
        sensor.AddObservation(LogCompress(self.Attack, context.Stats.MedianAttack));
        sensor.AddObservation(NormalizeByArenaRadius(self.AttackRange, context.ArenaRadius));
        sensor.AddObservation(NormalizePositiveByReference(self.MoveSpeed, context.Stats.MaxMoveSpeed));
        sensor.AddObservation(NormalizeAttackCooldown(self));
        sensor.AddObservation(features.AnchorDistanceRatio);
        sensor.AddObservation(features.AnchorVisibility);
        sensor.AddObservation(features.AnchorThreatToSelfRatio);
        sensor.AddObservation(features.SelfThreatToAnchorRatio);
        sensor.AddObservation(features.AnchorInSelfRange);
        sensor.AddObservation(features.SelfInAnchorRange);
        sensor.AddObservation(features.LeftLaneFreeRatio);
        sensor.AddObservation(features.RightLaneFreeRatio);
        sensor.AddObservation(features.AllyUnderFocusRatio);
        sensor.AddObservation(features.EnemyClusterPressure);
        sensor.AddObservation(boundaryPressure);

        sensor.AddObservation(context.BattleTimeoutRemainingRatio);
        Vector2 canonicalSmoothedMove = BattleCanonicalFrame.ToCanonical(self.TeamId, context.AgentSmoothedWorldMove);
        Vector2 canonicalPreviousMove = BattleCanonicalFrame.ToCanonical(
            self.TeamId,
            context.AgentPreviousRawWorldMove
        );
        sensor.AddObservation(canonicalSmoothedMove.x);
        sensor.AddObservation(canonicalSmoothedMove.y);
        sensor.AddObservation(canonicalPreviousMove.x);
        sensor.AddObservation(canonicalPreviousMove.y);
        AddAnchorKindOneHot(sensor, context.AnchorKind);
        AddPathModeOneHot(sensor, context.PathMode);

        AddTeammateSlotObservations(sensor, self, teammates, GladiatorObservationSchema.TeammateSlots, context);
        AddOpponentSlotObservations(sensor, self, opponents, GladiatorObservationSchema.OpponentSlots, context);
    }

    private static void AddTeammateSlotObservations(
        VectorSensor sensor,
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        int slots,
        GladiatorObservationContext context
    )
    {
        for (int i = 0; i < slots; i++)
        {
            BattleUnitCombatState unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                AddZeroes(sensor, GladiatorObservationSchema.TeammateSlotSize);
                continue;
            }

            Vector2 relativePos = WorldToObservationAxes(self.TeamId, unit.Position - self.Position);
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.x, context.ArenaRadius));
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.y, context.ArenaRadius));
            sensor.AddObservation(unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 1f);
            sensor.AddObservation(LogCompress(unit.MaxHealth, context.Stats.MedianMaxHealth));
            sensor.AddObservation(LogCompress(unit.Attack, context.Stats.MedianAttack));
            sensor.AddObservation(NormalizeByArenaRadius(unit.AttackRange, context.ArenaRadius));
            sensor.AddObservation(NormalizePositiveByReference(unit.MoveSpeed, context.Stats.MaxMoveSpeed));
            sensor.AddObservation(NormalizeAttackCooldown(unit));
        }
    }

    private static void AddOpponentSlotObservations(
        VectorSensor sensor,
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        int slots,
        GladiatorObservationContext context
    )
    {
        for (int i = 0; i < slots; i++)
        {
            BattleUnitCombatState unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                AddZeroes(sensor, GladiatorObservationSchema.OpponentSlotSize);
                continue;
            }

            Vector2 relativePos = WorldToObservationAxes(self.TeamId, unit.Position - self.Position);
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.x, context.ArenaRadius));
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.y, context.ArenaRadius));
            sensor.AddObservation(unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 1f);
            sensor.AddObservation(LogCompress(unit.MaxHealth, context.Stats.MedianMaxHealth));
            sensor.AddObservation(LogCompress(unit.Attack, context.Stats.MedianAttack));
            sensor.AddObservation(NormalizeByArenaRadius(unit.AttackRange, context.ArenaRadius));
            sensor.AddObservation(NormalizePositiveByReference(unit.MoveSpeed, context.Stats.MaxMoveSpeed));
            sensor.AddObservation(NormalizeAttackCooldown(unit));

            bool isTargetingMeAggressively =
                unit.PlannedTargetEnemy == self && unit.AgentStance != GladiatorActionSchema.StanceKeepRange;
            sensor.AddObservation(isTargetingMeAggressively ? 1f : 0f);
        }
    }

    private static void AddZeroes(VectorSensor sensor, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    private static void AddAnchorKindOneHot(VectorSensor sensor, int anchorKind)
    {
        for (int i = 0; i < GladiatorActionSchema.AnchorKindBranchSize; i++)
        {
            sensor.AddObservation(anchorKind == i ? 1f : 0f);
        }
    }

    private static void AddPathModeOneHot(VectorSensor sensor, int pathMode)
    {
        for (int i = 0; i < GladiatorActionSchema.PathModeBranchSize; i++)
        {
            sensor.AddObservation(pathMode == i ? 1f : 0f);
        }
    }

    private static Vector2 WorldToObservationAxes(BattleTeamId selfTeamId, Vector3 worldDelta)
    {
        return BattleCanonicalFrame.ToCanonical(selfTeamId, new Vector2(worldDelta.x, worldDelta.z));
    }

    private static float SampleLaneFreeRatio(BattleUnitCombatState self, IReadOnlyList<BattleUnitCombatState> opponents, Vector2 forward, float laneSign, float arenaRadius)
    {
        if (arenaRadius <= Epsilon)
        {
            return 1f;
        }

        Vector2 side = new Vector2(-forward.y, forward.x) * laneSign;
        int blockers = 0;
        int samples = 0;
        for (int i = 0; i < opponents.Count; i++)
        {
            BattleUnitCombatState unit = opponents[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            Vector2 relative = WorldToObservationAxes(self.TeamId, unit.Position - self.Position);
            if (relative.sqrMagnitude <= Epsilon)
            {
                continue;
            }

            if (Vector2.Dot(relative.normalized, side) > 0.35f)
            {
                blockers++;
            }

            samples++;
        }

        return samples > 0 ? Mathf.Clamp01(1f - blockers / (float)samples) : 1f;
    }

    private static float ComputeAllyUnderFocusRatio(IReadOnlyList<BattleUnitCombatState> teammates, IReadOnlyList<BattleUnitCombatState> opponents)
    {
        int allyCount = 0;
        int focusedCount = 0;
        for (int i = 0; i < teammates.Count; i++)
        {
            BattleUnitCombatState teammate = teammates[i];
            if (teammate == null || teammate.IsCombatDisabled)
            {
                continue;
            }

            allyCount++;
            for (int j = 0; j < opponents.Count; j++)
            {
                BattleUnitCombatState opponent = opponents[j];
                if (opponent != null && opponent.PlannedTargetEnemy == teammate)
                {
                    focusedCount++;
                    break;
                }
            }
        }

        return allyCount > 0 ? Mathf.Clamp01(focusedCount / (float)allyCount) : 0f;
    }

    private static float ComputeEnemyClusterPressure(BattleUnitCombatState self, IReadOnlyList<BattleUnitCombatState> opponents, float arenaRadius)
    {
        float total = 0f;
        int count = 0;
        for (int i = 0; i < opponents.Count; i++)
        {
            BattleUnitCombatState opponent = opponents[i];
            if (opponent == null || opponent.IsCombatDisabled)
            {
                continue;
            }

            total += 1f - NormalizeDistance(Distance2D(self.Position, opponent.Position), arenaRadius);
            count++;
        }

        return count > 0 ? Mathf.Clamp01(total / count) : 0f;
    }

    private static float Distance2D(Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        delta.y = 0f;
        return delta.magnitude;
    }

    private static float GetEffectiveRange(
        BattleUnitCombatState attacker,
        BattleUnitCombatState target,
        float attackRange
    )
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attackRange) + 0.05f;
    }

    private static float GetDamageToMaxHealthRatio(BattleUnitCombatState attacker, BattleUnitCombatState target)
    {
        if (attacker == null || target == null || target.MaxHealth <= Epsilon)
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.Max(0f, attacker.Attack) / target.MaxHealth);
    }

    private static float LogCompress(float value, float reference)
    {
        float safeValue = Mathf.Max(Epsilon, value);
        float safeReference = Mathf.Max(Epsilon, reference);
        float ratio = safeValue / safeReference;
        return Mathf.Clamp(Mathf.Log10(ratio) / LogCompressDecadeWindow, -1f, 1f);
    }

    private static float NormalizeAttackCooldown(BattleUnitCombatState unit)
    {
        if (unit == null)
        {
            return 0f;
        }

        float expectedCooldown = unit.AttackSpeed > Epsilon ? 1f / unit.AttackSpeed : 1f;
        return NormalizePositiveByReference(unit.AttackCooldownRemaining, expectedCooldown);
    }

    private static float NormalizeSignedByArenaRadius(float value, float arenaRadius)
    {
        if (!IsValidReference(arenaRadius))
        {
            return 0f;
        }

        return Mathf.Clamp(value / arenaRadius, -1f, 1f);
    }

    private static float NormalizeByArenaRadius(float value, float arenaRadius)
    {
        return IsValidReference(arenaRadius) ? Mathf.Clamp01(Mathf.Max(0f, value) / arenaRadius) : 0f;
    }

    private static float NormalizeDistance(float distance, float arenaRadius)
    {
        if (distance >= float.MaxValue || !IsValidReference(arenaRadius))
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.Max(0f, distance) / arenaRadius);
    }

    private static float NormalizePositiveByReference(float value, float reference)
    {
        return IsValidReference(reference) ? Mathf.Clamp01(Mathf.Max(0f, value) / reference) : 0f;
    }

    private static bool IsValidReference(float value)
    {
        return value > Epsilon && value < float.MaxValue;
    }
}
