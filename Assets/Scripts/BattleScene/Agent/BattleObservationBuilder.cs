using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public readonly struct GladiatorObservationContext
{
    public readonly BattleRuntimeUnit Self;
    public readonly GladiatorRosterView RosterView;
    public readonly Vector3 ArenaCenter;

    public GladiatorObservationContext(BattleRuntimeUnit self, GladiatorRosterView rosterView, Vector3 arenaCenter)
    {
        Self = self;
        RosterView = rosterView;
        ArenaCenter = arenaCenter;
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

        AddUnitSlotObservations(
            sensor,
            self,
            GetTeammatesSorted(context.RosterView, self),
            GladiatorObservationSchema.TeammateSlots
        );
        AddUnitSlotObservations(
            sensor,
            self,
            GetOpponentsSorted(context.RosterView, self),
            GladiatorObservationSchema.OpponentSlots
        );
    }

    private static void AddUnitSlotObservations(
        VectorSensor sensor,
        BattleRuntimeUnit self,
        List<BattleRuntimeUnit> units,
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

    private static List<BattleRuntimeUnit> GetTeammatesSorted(GladiatorRosterView rosterView, BattleRuntimeUnit self) =>
        rosterView != null ? rosterView.GetSortedTeammates(self) : new List<BattleRuntimeUnit>();

    private static List<BattleRuntimeUnit> GetOpponentsSorted(GladiatorRosterView rosterView, BattleRuntimeUnit self) =>
        rosterView != null ? rosterView.GetSortedHostiles(self) : new List<BattleRuntimeUnit>();
}
