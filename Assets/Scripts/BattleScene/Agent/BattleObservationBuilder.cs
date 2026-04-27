using System;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public readonly struct GladiatorObservationContext
{
    public readonly BattleRuntimeUnit Self;
    public readonly GladiatorRosterView RosterView;
    public readonly Vector3 ArenaCenter;
    public readonly float ArenaRadius;

    public GladiatorObservationContext(
        BattleRuntimeUnit self,
        GladiatorRosterView rosterView,
        Vector3 arenaCenter,
        float arenaRadius
    )
    {
        Self = self;
        RosterView = rosterView;
        ArenaCenter = arenaCenter;
        ArenaRadius = arenaRadius;
    }
}

public static class BattleObservationBuilder
{
    public static void Write(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleRuntimeUnit self = context.Self;
        if (self == null || self.IsCombatDisabled)
        {
            AddZeroes(sensor, GladiatorObservationSchema.TotalSize);
            return;
        }

        Vector2 arenaLocal = WorldToLocal(self, context.ArenaCenter - self.Position);
        sensor.AddObservation(arenaLocal.x);
        sensor.AddObservation(arenaLocal.y);
        sensor.AddObservation(self.CurrentHealth);
        sensor.AddObservation(self.MaxHealth);
        sensor.AddObservation(self.Attack);
        sensor.AddObservation(self.AttackRange);
        sensor.AddObservation(self.MoveSpeed);
        sensor.AddObservation(self.AttackCooldownRemaining);

        IReadOnlyList<BattleRuntimeUnit> teammates = GetTeammatesSorted(context.RosterView, self);
        IReadOnlyList<BattleRuntimeUnit> opponents = GetOpponentsSorted(context.RosterView, self);
        BattleRuntimeUnit nearestOpponent = FindNearestLiving(self, opponents, out float nearestOpponentDistance);
        float healthRatio = self.MaxHealth > 0f ? Mathf.Clamp01(self.CurrentHealth / self.MaxHealth) : 1f;
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

        sensor.AddObservation(healthRatio);
        sensor.AddObservation(1f - healthRatio);
        sensor.AddObservation(nearestOpponentDistance < float.MaxValue ? nearestOpponentDistance : 0f);
        sensor.AddObservation(nearestOpponent != null && nearestOpponentDistance <= selfEffectiveRange ? 1f : 0f);
        sensor.AddObservation(nearestOpponent != null && nearestOpponentDistance <= opponentEffectiveRange ? 1f : 0f);
        sensor.AddObservation(
            CountLivingWithin(self, opponents, threatRadius) / (float)GladiatorObservationSchema.OpponentSlots
        );
        sensor.AddObservation(
            CountLivingWithin(self, teammates, threatRadius) / (float)GladiatorObservationSchema.TeammateSlots
        );
        sensor.AddObservation(boundaryPressure);

        AddUnitSlotObservations(sensor, self, teammates, GladiatorObservationSchema.TeammateSlots);
        AddUnitSlotObservations(sensor, self, opponents, GladiatorObservationSchema.OpponentSlots);
    }

    private static void AddUnitSlotObservations(
        VectorSensor sensor,
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> units,
        int slots
    )
    {
        for (int i = 0; i < slots; i++)
        {
            BattleRuntimeUnit unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                AddZeroes(sensor, GladiatorObservationSchema.UnitSlotSize);
                continue;
            }

            Vector2 localPos = WorldToLocal(self, unit.Position - self.Position);
            sensor.AddObservation(localPos.x);
            sensor.AddObservation(localPos.y);
            sensor.AddObservation(unit.CurrentHealth);
            sensor.AddObservation(unit.MaxHealth);
            sensor.AddObservation(unit.Attack);
            sensor.AddObservation(unit.AttackRange);
            sensor.AddObservation(unit.MoveSpeed);
        }
    }

    private static void AddZeroes(VectorSensor sensor, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    private static Vector2 WorldToLocal(BattleRuntimeUnit self, Vector3 worldDelta)
    {
        float x = Vector3.Dot(worldDelta, self.transform.right);
        float z = Vector3.Dot(worldDelta, self.transform.forward);
        return new Vector2(x, z);
    }

    private static BattleRuntimeUnit FindNearestLiving(
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> units,
        out float nearestDistance
    )
    {
        BattleRuntimeUnit nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
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

    private static float CountLivingWithin(BattleRuntimeUnit self, IReadOnlyList<BattleRuntimeUnit> units, float radius)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        float sqrRadius = radius * radius;
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
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

    private static float GetEffectiveRange(BattleRuntimeUnit attacker, BattleRuntimeUnit target, float attackRange)
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attackRange) + 0.05f;
    }

    private static IReadOnlyList<BattleRuntimeUnit> GetTeammatesSorted(
        GladiatorRosterView rosterView,
        BattleRuntimeUnit self
    ) => rosterView != null ? rosterView.GetSortedTeammates(self) : Array.Empty<BattleRuntimeUnit>();

    private static IReadOnlyList<BattleRuntimeUnit> GetOpponentsSorted(
        GladiatorRosterView rosterView,
        BattleRuntimeUnit self
    ) => rosterView != null ? rosterView.GetSortedHostiles(self) : Array.Empty<BattleRuntimeUnit>();
}
