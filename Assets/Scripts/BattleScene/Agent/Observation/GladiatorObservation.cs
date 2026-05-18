using System;
using Unity.MLAgents.Sensors;

// GladiatorAgent의 vector observation을 시스템 내부에서 공유하는 값 타입이다.
// GladiatorObservationSchema의 순서 contract를 사람이 읽기 쉬운 필드 구조로 드러낸 뒤 sensor에 직렬화한다.
public readonly struct GladiatorObservation
{
    public readonly GladiatorSelfObservation Self;
    public readonly GladiatorUnitObservation[] Teammates;
    public readonly GladiatorUnitObservation[] Opponents;

    public GladiatorObservation(
        GladiatorSelfObservation self,
        GladiatorUnitObservation[] teammates,
        GladiatorUnitObservation[] opponents
    )
    {
        Self = self;
        Teammates = NormalizeSlots(teammates, GladiatorObservationSchema.TeammateSlots);
        Opponents = NormalizeSlots(opponents, GladiatorObservationSchema.OpponentSlots);
    }

    public static GladiatorObservation Zero =>
        new GladiatorObservation(
            default,
            new GladiatorUnitObservation[GladiatorObservationSchema.TeammateSlots],
            new GladiatorUnitObservation[GladiatorObservationSchema.OpponentSlots]
        );

    public void WriteTo(VectorSensor sensor)
    {
        Self.WriteTo(sensor);
        WriteTeammates(sensor);
        WriteOpponents(sensor);
    }

    private void WriteTeammates(VectorSensor sensor)
    {
        for (int i = 0; i < GladiatorObservationSchema.TeammateSlots; i++)
        {
            Teammates[i].WriteTeammateTo(sensor);
        }
    }

    private void WriteOpponents(VectorSensor sensor)
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            Opponents[i].WriteOpponentTo(sensor);
        }
    }

    private static GladiatorUnitObservation[] NormalizeSlots(GladiatorUnitObservation[] source, int size)
    {
        if (source == null)
        {
            return new GladiatorUnitObservation[size];
        }

        if (source.Length == size)
        {
            return source;
        }

        GladiatorUnitObservation[] normalized = new GladiatorUnitObservation[size];
        Array.Copy(source, normalized, Math.Min(source.Length, size));
        return normalized;
    }
}

// GladiatorObservationSchema.SelfSize 구간의 이름 있는 observation 필드 묶음이다.
public readonly struct GladiatorSelfObservation
{
    public readonly float ArenaCenterAnchorRelativeX;
    public readonly float ArenaCenterAnchorRelativeZ;
    public readonly float HealthRatio;
    public readonly float MaxHealthLogRatio;
    public readonly float AttackLogRatio;
    public readonly float AttackRangeRatio;
    public readonly float MoveSpeedRatio;
    public readonly float AttackCooldownRatio;
    public readonly float AnchorThreatToSelfRatio;
    public readonly float SelfThreatToAnchorRatio;
    public readonly float AnchorInSelfRange;
    public readonly float SelfInAnchorRange;
    public readonly float LeftLaneFreeRatio;
    public readonly float RightLaneFreeRatio;
    public readonly float EnemyClusterPressure;
    public readonly float BoundaryPressure;
    public readonly float BattleTimeoutRemainingRatio;
    public readonly float RawLocalMoveX;
    public readonly float RawLocalMoveZ;
    public readonly float PreviousRawLocalMoveX;
    public readonly float AgentPreviousRawMoveZ;
    public readonly int AnchorSlot;
    public readonly GladiatorCommand Command;
    public readonly GladiatorStrategy Strategy;
    public readonly float AnchorCommitmentRatio;
    public readonly float StrategyCommitmentRatio;
    public readonly float AnchorAllySupportPressure;
    public readonly float AnchorEnemyFocusPressure;
    public readonly float AnchorEnemyIsolation;
    public readonly float AnchorEnemyRetreatSignal;
    public readonly float PersonalityCollectivism;
    public readonly float PersonalityPassiveness;

    public GladiatorSelfObservation(
        float arenaCenterAnchorRelativeX,
        float arenaCenterAnchorRelativeZ,
        float healthRatio,
        float maxHealthLogRatio,
        float attackLogRatio,
        float attackRangeRatio,
        float moveSpeedRatio,
        float attackCooldownRatio,
        float anchorThreatToSelfRatio,
        float selfThreatToAnchorRatio,
        float anchorInSelfRange,
        float selfInAnchorRange,
        float leftLaneFreeRatio,
        float rightLaneFreeRatio,
        float enemyClusterPressure,
        float boundaryPressure,
        float battleTimeoutRemainingRatio,
        float rawLocalMoveX,
        float rawLocalMoveZ,
        float previousRawLocalMoveX,
        float previousRawLocalMoveZ,
        int anchorSlot,
        GladiatorCommand command,
        GladiatorStrategy strategy,
        float anchorCommitmentRatio,
        float strategyCommitmentRatio,
        float anchorAllySupportPressure,
        float anchorEnemyFocusPressure,
        float anchorEnemyIsolation,
        float anchorEnemyRetreatSignal,
        float personalityCollectivism,
        float personalityPassiveness
    )
    {
        ArenaCenterAnchorRelativeX = arenaCenterAnchorRelativeX;
        ArenaCenterAnchorRelativeZ = arenaCenterAnchorRelativeZ;
        HealthRatio = healthRatio;
        MaxHealthLogRatio = maxHealthLogRatio;
        AttackLogRatio = attackLogRatio;
        AttackRangeRatio = attackRangeRatio;
        MoveSpeedRatio = moveSpeedRatio;
        AttackCooldownRatio = attackCooldownRatio;
        AnchorThreatToSelfRatio = anchorThreatToSelfRatio;
        SelfThreatToAnchorRatio = selfThreatToAnchorRatio;
        AnchorInSelfRange = anchorInSelfRange;
        SelfInAnchorRange = selfInAnchorRange;
        LeftLaneFreeRatio = leftLaneFreeRatio;
        RightLaneFreeRatio = rightLaneFreeRatio;
        EnemyClusterPressure = enemyClusterPressure;
        BoundaryPressure = boundaryPressure;
        BattleTimeoutRemainingRatio = battleTimeoutRemainingRatio;
        RawLocalMoveX = rawLocalMoveX;
        RawLocalMoveZ = rawLocalMoveZ;
        PreviousRawLocalMoveX = previousRawLocalMoveX;
        AgentPreviousRawMoveZ = previousRawLocalMoveZ;
        AnchorSlot = anchorSlot;
        Command = command;
        Strategy = strategy;
        AnchorCommitmentRatio = anchorCommitmentRatio;
        StrategyCommitmentRatio = strategyCommitmentRatio;
        AnchorAllySupportPressure = anchorAllySupportPressure;
        AnchorEnemyFocusPressure = anchorEnemyFocusPressure;
        AnchorEnemyIsolation = anchorEnemyIsolation;
        AnchorEnemyRetreatSignal = anchorEnemyRetreatSignal;
        PersonalityCollectivism = personalityCollectivism;
        PersonalityPassiveness = personalityPassiveness;
    }

    public void WriteTo(VectorSensor sensor)
    {
        sensor.AddObservation(ArenaCenterAnchorRelativeX);
        sensor.AddObservation(ArenaCenterAnchorRelativeZ);
        sensor.AddObservation(HealthRatio);
        sensor.AddObservation(MaxHealthLogRatio);
        sensor.AddObservation(AttackLogRatio);
        sensor.AddObservation(AttackRangeRatio);
        sensor.AddObservation(MoveSpeedRatio);
        sensor.AddObservation(AttackCooldownRatio);
        sensor.AddObservation(AnchorThreatToSelfRatio);
        sensor.AddObservation(SelfThreatToAnchorRatio);
        sensor.AddObservation(AnchorInSelfRange);
        sensor.AddObservation(SelfInAnchorRange);
        sensor.AddObservation(LeftLaneFreeRatio);
        sensor.AddObservation(RightLaneFreeRatio);
        sensor.AddObservation(EnemyClusterPressure);
        sensor.AddObservation(BoundaryPressure);
        sensor.AddObservation(BattleTimeoutRemainingRatio);
        sensor.AddObservation(RawLocalMoveX);
        sensor.AddObservation(RawLocalMoveZ);
        sensor.AddObservation(PreviousRawLocalMoveX);
        sensor.AddObservation(AgentPreviousRawMoveZ);
        AddOneHot(sensor, (int)Command, GladiatorActionSchema.CommandBranchSize);
        AddOneHot(sensor, AnchorSlot, GladiatorActionSchema.AnchorActionBranchSize);
        AddOneHot(sensor, (int)Strategy, GladiatorActionSchema.StrategyBranchSize);
        sensor.AddObservation(AnchorCommitmentRatio);
        sensor.AddObservation(StrategyCommitmentRatio);
        sensor.AddObservation(AnchorAllySupportPressure);
        sensor.AddObservation(AnchorEnemyFocusPressure);
        sensor.AddObservation(AnchorEnemyIsolation);
        sensor.AddObservation(AnchorEnemyRetreatSignal);
        sensor.AddObservation(PersonalityCollectivism);
        sensor.AddObservation(PersonalityPassiveness);
    }

    private static void AddOneHot(VectorSensor sensor, int value, int size)
    {
        for (int i = 0; i < size; i++)
        {
            sensor.AddObservation(value == i ? 1f : 0f);
        }
    }
}

// 팀원/적 슬롯 observation의 공통 필드다. 적 슬롯만 IsTargetingMeAggressively를 추가로 직렬화한다.
public readonly struct GladiatorUnitObservation
{
    public readonly float AnchorRelativePositionX;
    public readonly float AnchorRelativePositionZ;
    public readonly float DistanceToSelfRatio;
    public readonly float HealthRatio;
    public readonly float MaxHealthLogRatio;
    public readonly float AttackLogRatio;
    public readonly float AttackRangeRatio;
    public readonly float MoveSpeedRatio;
    public readonly float AttackCooldownRatio;
    public readonly float IsTargetingMeAggressively;

    public GladiatorUnitObservation(
        float anchorRelativePositionX,
        float anchorRelativePositionZ,
        float distanceToSelfRatio,
        float healthRatio,
        float maxHealthLogRatio,
        float attackLogRatio,
        float attackRangeRatio,
        float moveSpeedRatio,
        float attackCooldownRatio,
        float isTargetingMeAggressively = 0f
    )
    {
        AnchorRelativePositionX = anchorRelativePositionX;
        AnchorRelativePositionZ = anchorRelativePositionZ;
        DistanceToSelfRatio = distanceToSelfRatio;
        HealthRatio = healthRatio;
        MaxHealthLogRatio = maxHealthLogRatio;
        AttackLogRatio = attackLogRatio;
        AttackRangeRatio = attackRangeRatio;
        MoveSpeedRatio = moveSpeedRatio;
        AttackCooldownRatio = attackCooldownRatio;
        IsTargetingMeAggressively = isTargetingMeAggressively;
    }

    public void WriteTeammateTo(VectorSensor sensor)
    {
        WriteCommonTo(sensor);
    }

    public void WriteOpponentTo(VectorSensor sensor)
    {
        WriteCommonTo(sensor);
        sensor.AddObservation(IsTargetingMeAggressively);
    }

    private void WriteCommonTo(VectorSensor sensor)
    {
        sensor.AddObservation(AnchorRelativePositionX);
        sensor.AddObservation(AnchorRelativePositionZ);
        sensor.AddObservation(DistanceToSelfRatio);
        sensor.AddObservation(HealthRatio);
        sensor.AddObservation(MaxHealthLogRatio);
        sensor.AddObservation(AttackLogRatio);
        sensor.AddObservation(AttackRangeRatio);
        sensor.AddObservation(MoveSpeedRatio);
        sensor.AddObservation(AttackCooldownRatio);
    }
}
