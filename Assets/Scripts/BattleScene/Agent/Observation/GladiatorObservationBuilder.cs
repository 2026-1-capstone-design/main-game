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
    public readonly Vector2 RawLocalMove;
    public readonly Vector2 PreviousRawLocalMove;
    public readonly int AnchorSlot;
    public readonly GladiatorCommand CurrentCommand;
    public readonly GladiatorStrategy CurrentStrategy;
    public readonly int AnchorCommitmentSteps;
    public readonly int StrategyCommitmentSteps;
    public readonly float PersonalityCollectivism;
    public readonly float PersonalityPassiveness;
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
        Vector2 rawLocalMove,
        Vector2 PreviousRawLocalMove,
        int anchorSlot,
        GladiatorCommand currentCommand,
        GladiatorStrategy currentStrategy,
        int anchorCommitmentSteps,
        int strategyCommitmentSteps,
        float personalityCollectivism,
        float personalityPassiveness,
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
        RawLocalMove = Vector2.ClampMagnitude(rawLocalMove, 1f);
        this.PreviousRawLocalMove = Vector2.ClampMagnitude(PreviousRawLocalMove, 1f);
        AnchorSlot = Mathf.Clamp(anchorSlot, 0, GladiatorActionSchema.AnchorActionBranchSize - 1);
        CurrentCommand = currentCommand;
        CurrentStrategy = currentStrategy;
        AnchorCommitmentSteps = Mathf.Max(0, anchorCommitmentSteps);
        StrategyCommitmentSteps = Mathf.Max(0, strategyCommitmentSteps);
        PersonalityCollectivism = Mathf.Clamp01(personalityCollectivism);
        PersonalityPassiveness = Mathf.Clamp01(personalityPassiveness);
        CurrentAnchor = currentAnchor;
    }

    public Vector3 GetAnchorPosition()
    {
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

    // 에피소드 중 유닛 사망/버프에 따라 정규화 기준이 흔들리지 않도록 전투 시작 시점의 roster 최대 체력을 사용한다.
    public readonly float MaxRosterMaxHealth;
    public readonly float MaxMoveSpeed;

    public GladiatorObservationStats(float maxRosterMaxHealth, float maxMoveSpeed)
    {
        MaxRosterMaxHealth = Mathf.Max(Epsilon, maxRosterMaxHealth);
        MaxMoveSpeed = Mathf.Max(Epsilon, maxMoveSpeed);
    }
}

public static class GladiatorObservationBuilder
{
    private const float Epsilon = 1e-6f;

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
        float currentHealthRatio = NormalizeByRosterMaxHealth(self.CurrentHealth, context);
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
            currentHealthRatio,
            NormalizeByRosterMaxHealth(self.Attack, context),
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
            context.RawLocalMove.x,
            context.RawLocalMove.y,
            context.PreviousRawLocalMove.x,
            context.PreviousRawLocalMove.y,
            context.AnchorSlot,
            context.CurrentCommand,
            context.CurrentStrategy,
            NormalizeCommitment(context.AnchorCommitmentSteps),
            NormalizeCommitment(context.StrategyCommitmentSteps),
            features.AnchorAllySupportPressure,
            features.AnchorEnemyFocusPressure,
            features.AnchorEnemyIsolation,
            features.AnchorEnemyRetreatSignal,
            context.PersonalityCollectivism,
            context.PersonalityPassiveness
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
                unit.PlannedTargetEnemy == self && unit.AgentStrategy != GladiatorStrategy.KeepRange;
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
            NormalizeByRosterMaxHealth(unit.CurrentHealth, context),
            NormalizeByRosterMaxHealth(unit.Attack, context),
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

    private static float NormalizeByRosterMaxHealth(float value, GladiatorObservationContext context)
    {
        return GladiatorObservationNormalization.NormalizePositiveByReference(value, context.Stats.MaxRosterMaxHealth);
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
