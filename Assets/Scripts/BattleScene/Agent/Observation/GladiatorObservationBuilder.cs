using System;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

public readonly struct GladiatorObservationContext
{
    private const float Epsilon = 1e-6f;

    public readonly BattleUnitCombatState Self;
    public readonly IReadOnlyList<BattleUnitCombatState> Teammates;
    public readonly IReadOnlyList<BattleUnitCombatState> Opponents;
    public readonly GladiatorObservationStats Stats;
    public readonly Vector3 ArenaCenter;
    public readonly Vector3 TeamCenter;
    public readonly float ArenaRadius;
    public readonly float BattleTimeoutRemainingRatio;
    public readonly Vector2 AgentSmoothedWorldMove;
    public readonly Vector2 AgentPreviousRawWorldMove;
    public readonly GladiatorAnchorKind AnchorKind;
    public readonly int AnchorSlot;
    public readonly GladiatorFightMode CurrentFightMode;
    public readonly GladiatorActionRole CurrentRole;
    public readonly int AnchorCommitmentSteps;
    public readonly int RoleCommitmentSteps;
    public readonly BattleUnitCombatState CurrentAnchor;

    public GladiatorObservationContext(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> teammates,
        IReadOnlyList<BattleUnitCombatState> opponents,
        GladiatorObservationStats stats,
        Vector3 arenaCenter,
        Vector3 teamCenter,
        float arenaRadius,
        float battleTimeoutRemainingRatio,
        Vector2 agentSmoothedWorldMove,
        Vector2 agentPreviousRawWorldMove,
        GladiatorAnchorKind anchorKind,
        int anchorSlot,
        GladiatorFightMode currentFightMode,
        GladiatorActionRole currentRole,
        int anchorCommitmentSteps,
        int roleCommitmentSteps,
        BattleUnitCombatState currentAnchor
    )
    {
        Self = self;
        Teammates = teammates ?? Array.Empty<BattleUnitCombatState>();
        Opponents = opponents ?? Array.Empty<BattleUnitCombatState>();
        Stats = stats;
        ArenaCenter = arenaCenter;
        TeamCenter = teamCenter;
        ArenaRadius = arenaRadius;
        BattleTimeoutRemainingRatio = Mathf.Clamp01(battleTimeoutRemainingRatio);
        AgentSmoothedWorldMove = Vector2.ClampMagnitude(agentSmoothedWorldMove, 1f);
        AgentPreviousRawWorldMove = Vector2.ClampMagnitude(agentPreviousRawWorldMove, 1f);
        AnchorKind = anchorKind;
        AnchorSlot = Mathf.Clamp(anchorSlot, 0, GladiatorActionSchema.AnchorSlotObservationSize - 1);
        CurrentFightMode = currentFightMode;
        CurrentRole = currentRole;
        AnchorCommitmentSteps = Mathf.Max(0, anchorCommitmentSteps);
        RoleCommitmentSteps = Mathf.Max(0, roleCommitmentSteps);
        CurrentAnchor = currentAnchor;
    }

    public Vector3 GetAnchorPosition()
    {
        if (AnchorKind == GladiatorAnchorKind.TeamCenter)
        {
            return TeamCenter;
        }

        if (CurrentAnchor != null && !CurrentAnchor.IsCombatDisabled)
        {
            return CurrentAnchor.Position;
        }

        return ArenaCenter;
    }

    // Observation과 tactical feature가 같은 anchor 기준 좌표계를 쓰도록 projection 규칙을 context에 둔다.
    public Vector2 WorldToObservationAxes(Vector3 worldDelta)
    {
        return WorldToObservationAxes(new Vector2(worldDelta.x, worldDelta.z));
    }

    public Vector2 WorldToObservationAxes(Vector2 worldDelta)
    {
        return ProjectWorldDeltaToAnchorAxes(GetAnchorPosition(), worldDelta);
    }

    public Vector2 ProjectWorldDeltaToAnchorAxes(Vector3 anchorPosition, Vector2 worldDelta)
    {
        if (Self == null)
        {
            return worldDelta;
        }

        Vector3 anchorDelta3 = anchorPosition - Self.Position;
        anchorDelta3.y = 0f;
        Vector2 anchorDelta = new Vector2(anchorDelta3.x, anchorDelta3.z);
        if (anchorDelta.sqrMagnitude <= Epsilon)
        {
            return worldDelta;
        }

        Vector2 forward = anchorDelta.normalized;
        Vector2 left = new Vector2(-forward.y, forward.x);
        return new Vector2(Vector2.Dot(worldDelta, left), Vector2.Dot(worldDelta, forward));
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

public static class GladiatorObservationBuilder
{
    private const float Epsilon = 1e-6f;
    private const float LogCompressDecadeWindow = 3f;

    public static void Write(VectorSensor sensor, GladiatorObservationContext context)
    {
        Build(context).WriteTo(sensor);
    }

    public static GladiatorObservation Build(GladiatorObservationContext context)
    {
        BattleUnitCombatState self = context.Self;
        if (self == null || self.IsCombatDisabled)
        {
            return GladiatorObservation.Zero;
        }

        Vector2 arenaDelta = context.WorldToObservationAxes(context.ArenaCenter - self.Position);
        IReadOnlyList<BattleUnitCombatState> teammates = context.Teammates;
        IReadOnlyList<BattleUnitCombatState> opponents = context.Opponents;
        float healthRatio = self.MaxHealth > 0f ? Mathf.Clamp01(self.CurrentHealth / self.MaxHealth) : 1f;
        GladiatorCombatSignalFeatures features = GladiatorCombatSignalFeatures.Builder.Build(context);
        float distanceFromCenter = Vector3.Distance(
            new Vector3(self.Position.x, 0f, self.Position.z),
            new Vector3(context.ArenaCenter.x, 0f, context.ArenaCenter.z)
        );
        float boundaryPressure = GladiatorObservationNormalization.NormalizeDistanceByArenaRadius(
            distanceFromCenter,
            context.ArenaRadius
        );

        GladiatorSelfObservation selfObservation = new GladiatorSelfObservation(
            GladiatorObservationNormalization.NormalizeSignedByArenaRadius(arenaDelta.x, context.ArenaRadius),
            GladiatorObservationNormalization.NormalizeSignedByArenaRadius(arenaDelta.y, context.ArenaRadius),
            healthRatio,
            LogCompress(self.MaxHealth, context.Stats.MedianMaxHealth),
            LogCompress(self.Attack, context.Stats.MedianAttack),
            GladiatorObservationNormalization.NormalizeByArenaRadius(self.AttackRange, context.ArenaRadius),
            GladiatorObservationNormalization.NormalizePositiveByReference(self.MoveSpeed, context.Stats.MaxMoveSpeed),
            NormalizeAttackCooldown(self),
            features.AnchorThreatToSelfRatio,
            features.SelfThreatToAnchorRatio,
            features.AnchorInSelfRange,
            features.SelfInAnchorRange,
            features.LeftLaneFreeRatio,
            features.RightLaneFreeRatio,
            features.EnemyClusterPressure,
            boundaryPressure,
            context.BattleTimeoutRemainingRatio,
            context.AgentSmoothedWorldMove.x,
            context.AgentSmoothedWorldMove.y,
            // TODO 임시 비활성화
            // context.AgentPreviousRawWorldMove.x,
            // context.AgentPreviousRawWorldMove.y,
            0,
            0,
            context.AnchorKind,
            context.AnchorSlot,
            context.CurrentFightMode,
            context.CurrentRole,
            NormalizeCommitment(context.AnchorCommitmentSteps),
            NormalizeCommitment(context.RoleCommitmentSteps),
            features.AnchorAllySupportPressure,
            features.AnchorEnemyFocusPressure,
            features.AnchorEnemyIsolation,
            features.AnchorEnemyRetreatSignal
        );

        return new GladiatorObservation(
            selfObservation,
            BuildTeammateSlotObservations(self, teammates, GladiatorObservationSchema.TeammateSlots, context),
            BuildOpponentSlotObservations(self, opponents, GladiatorObservationSchema.OpponentSlots, context)
        );
    }

    private static GladiatorUnitObservation[] BuildTeammateSlotObservations(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        int slots,
        GladiatorObservationContext context
    )
    {
        GladiatorUnitObservation[] observations = new GladiatorUnitObservation[slots];
        for (int i = 0; i < slots; i++)
        {
            BattleUnitCombatState unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            observations[i] = BuildUnitObservation(self, unit, context);
        }

        return observations;
    }

    private static GladiatorUnitObservation[] BuildOpponentSlotObservations(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> units,
        int slots,
        GladiatorObservationContext context
    )
    {
        GladiatorUnitObservation[] observations = new GladiatorUnitObservation[slots];
        for (int i = 0; i < slots; i++)
        {
            BattleUnitCombatState unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            bool isTargetingMeAggressively =
                unit.PlannedTargetEnemy == self && unit.AgentFightMode != GladiatorFightMode.KeepRange;
            observations[i] = BuildUnitObservation(self, unit, context, isTargetingMeAggressively ? 1f : 0f);
        }

        return observations;
    }

    private static GladiatorUnitObservation BuildUnitObservation(
        BattleUnitCombatState self,
        BattleUnitCombatState unit,
        GladiatorObservationContext context,
        float isTargetingMeAggressively = 0f
    )
    {
        Vector2 relativePos = context.WorldToObservationAxes(unit.Position - self.Position);
        return new GladiatorUnitObservation(
            GladiatorObservationNormalization.NormalizeSignedByArenaRadius(relativePos.x, context.ArenaRadius),
            GladiatorObservationNormalization.NormalizeSignedByArenaRadius(relativePos.y, context.ArenaRadius),
            NormalizeHorizontalDistance(self.Position, unit.Position, context.ArenaRadius),
            unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 1f,
            LogCompress(unit.MaxHealth, context.Stats.MedianMaxHealth),
            LogCompress(unit.Attack, context.Stats.MedianAttack),
            GladiatorObservationNormalization.NormalizeByArenaRadius(unit.AttackRange, context.ArenaRadius),
            GladiatorObservationNormalization.NormalizePositiveByReference(unit.MoveSpeed, context.Stats.MaxMoveSpeed),
            NormalizeAttackCooldown(unit),
            isTargetingMeAggressively
        );
    }

    private static float NormalizeHorizontalDistance(Vector3 a, Vector3 b, float arenaRadius)
    {
        Vector3 delta = a - b;
        delta.y = 0f;
        return GladiatorObservationNormalization.NormalizeByArenaRadius(delta.magnitude, arenaRadius);
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
        return GladiatorObservationNormalization.NormalizePositiveByReference(
            unit.AttackCooldownRemaining,
            expectedCooldown
        );
    }

    private static float NormalizeCommitment(int steps)
    {
        return Mathf.Clamp01(steps / 12f);
    }
}
