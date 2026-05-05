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
    public readonly int CurrentStance;

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
        int currentStance
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
        CurrentStance = currentStance;
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

    public static void Write(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleUnitCombatState self = context.Self;
        if (self == null || self.IsCombatDisabled)
        {
            AddZeroes(sensor, GladiatorObservationSchema.TotalSize);
            return;
        }

        Vector2 arenaDelta = WorldToObservationAxes(context.ArenaCenter - self.Position);
        IReadOnlyList<BattleUnitCombatState> teammates = context.Teammates;
        IReadOnlyList<BattleUnitCombatState> opponents = context.Opponents;
        BattleUnitCombatState nearestOpponent = FindNearestLiving(self, opponents, out float nearestOpponentDistance);
        float healthRatio = self.MaxHealth > 0f ? Mathf.Clamp01(self.CurrentHealth / self.MaxHealth) : 1f;
        float selfDamageToNearestEnemyMaxHp = GetDamageToMaxHealthRatio(self, nearestOpponent);
        float nearestEnemyDamageToSelfMaxHp = GetDamageToMaxHealthRatio(nearestOpponent, self);
        float selfEffectiveRange = GetEffectiveRange(self, nearestOpponent, self.AttackRange);
        float opponentEffectiveRange = GetEffectiveRange(nearestOpponent, self, nearestOpponent?.AttackRange ?? 0f);
        float threatRadius = Mathf.Max(selfEffectiveRange, opponentEffectiveRange) * 1.25f;
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
        sensor.AddObservation(selfDamageToNearestEnemyMaxHp);
        sensor.AddObservation(nearestEnemyDamageToSelfMaxHp);
        sensor.AddObservation(NormalizeDistance(nearestOpponentDistance, context.ArenaRadius));
        sensor.AddObservation(nearestOpponent != null && nearestOpponentDistance <= selfEffectiveRange ? 1f : 0f);
        sensor.AddObservation(nearestOpponent != null && nearestOpponentDistance <= opponentEffectiveRange ? 1f : 0f);
        sensor.AddObservation(
            CountLivingWithin(self, opponents, threatRadius) / (float)GladiatorObservationSchema.OpponentSlots
        );
        sensor.AddObservation(
            CountLivingWithin(self, teammates, threatRadius) / (float)GladiatorObservationSchema.TeammateSlots
        );
        sensor.AddObservation(boundaryPressure);

        sensor.AddObservation(context.BattleTimeoutRemainingRatio);
        sensor.AddObservation(context.AgentSmoothedWorldMove.x);
        sensor.AddObservation(context.AgentSmoothedWorldMove.y);
        sensor.AddObservation(context.AgentPreviousRawWorldMove.x);
        sensor.AddObservation(context.AgentPreviousRawWorldMove.y);
        sensor.AddObservation(self.CurrentTarget != null && !self.CurrentTarget.IsCombatDisabled ? 1f : 0f);
        AddCurrentTargetSlotOneHot(sensor, self.CurrentTarget, opponents);
        AddCurrentStanceOneHot(sensor, context.CurrentStance);

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

            Vector2 relativePos = WorldToObservationAxes(unit.Position - self.Position);
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

            Vector2 relativePos = WorldToObservationAxes(unit.Position - self.Position);
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.x, context.ArenaRadius));
            sensor.AddObservation(NormalizeSignedByArenaRadius(relativePos.y, context.ArenaRadius));
            sensor.AddObservation(unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 1f);
            sensor.AddObservation(LogCompress(unit.MaxHealth, context.Stats.MedianMaxHealth));
            sensor.AddObservation(LogCompress(unit.Attack, context.Stats.MedianAttack));
            sensor.AddObservation(NormalizeByArenaRadius(unit.AttackRange, context.ArenaRadius));
            sensor.AddObservation(NormalizePositiveByReference(unit.MoveSpeed, context.Stats.MaxMoveSpeed));
            sensor.AddObservation(NormalizeAttackCooldown(unit));

            bool isTargetingMeAggressively =
                unit.PlannedTargetEnemy == self
                && unit.AgentStance != GladiatorActionSchema.StanceKeepRange;
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

    private static void AddCurrentTargetSlotOneHot(
        VectorSensor sensor,
        BattleUnitCombatState currentTarget,
        IReadOnlyList<BattleUnitCombatState> opponents
    )
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState opponent = i < opponents.Count ? opponents[i] : null;
            bool selected = currentTarget != null && !currentTarget.IsCombatDisabled && opponent == currentTarget;
            sensor.AddObservation(selected ? 1f : 0f);
        }
    }

    private static void AddCurrentStanceOneHot(VectorSensor sensor, int currentStance)
    {
        for (int i = 0; i < GladiatorObservationSchema.CurrentStanceObservationSize; i++)
        {
            sensor.AddObservation(currentStance == i ? 1f : 0f);
        }
    }

    private static Vector2 WorldToObservationAxes(Vector3 worldDelta)
    {
        return new Vector2(worldDelta.x, worldDelta.z);
    }

    private static BattleUnitCombatState FindNearestLiving(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        out float nearestDistance
    )
    {
        BattleUnitCombatState nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitCombatState unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = unit.Position - self.Position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = unit;
            }
        }

        nearestDistance = nearest != null ? Mathf.Sqrt(nearestSqrDistance) : float.MaxValue;
        return nearest;
    }

    private static float CountLivingWithin(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        float radius
    )
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        float sqrRadius = radius * radius;
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleUnitCombatState unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = unit.Position - self.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
            {
                count++;
            }
        }

        return count;
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
