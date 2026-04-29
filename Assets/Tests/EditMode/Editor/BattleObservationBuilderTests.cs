using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using NUnit.Framework;
using Unity.MLAgents.Sensors;
using UnityEngine;

public sealed class BattleObservationBuilderTests
{
    [Test]
    public void Write_NormalizesSelfAndFirstUnitSlotObservations()
    {
        BattleUnitCombatState self = CreateState(
            unitNumber: 1,
            teamId: BattleTeamIds.Player,
            maxHealth: 100f,
            currentHealth: 50f,
            attack: 10f,
            attackSpeed: 2f,
            moveSpeed: 5f,
            attackRange: 2f,
            position: new Vector3(10f, 0f, 0f),
            bodyRadius: 1f
        );
        BattleUnitCombatState teammate = CreateState(
            unitNumber: 2,
            teamId: BattleTeamIds.Player,
            maxHealth: 50f,
            currentHealth: 25f,
            attack: 5f,
            attackSpeed: 1f,
            moveSpeed: 2.5f,
            attackRange: 1f,
            position: new Vector3(10f, 0f, -4f),
            bodyRadius: 1f
        );
        BattleUnitCombatState opponent = CreateState(
            unitNumber: 3,
            teamId: BattleTeamIds.Enemy,
            maxHealth: 200f,
            currentHealth: 100f,
            attack: 20f,
            attackSpeed: 1f,
            moveSpeed: 10f,
            attackRange: 3f,
            position: new Vector3(10f, 0f, 5f),
            bodyRadius: 1f
        );
        self.ResetAttackCooldown();
        self.SetPlannedTargets(opponent, null);

        var sensor = new VectorSensor(GladiatorObservationSchema.TotalSize);
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(
                self,
                new[] { teammate },
                new[] { opponent },
                new GladiatorObservationStats(100f, 10f, 10f),
                BattleUnitPose.Default,
                Vector3.zero,
                20f,
                0.75f,
                new Vector2(0.25f, -0.5f),
                0.4f,
                new Vector2(-0.2f, 0.1f),
                -0.3f
            )
        );

        ReadOnlyCollection<float> observations = GetObservations(sensor);

        Assert.That(observations.Count, Is.EqualTo(GladiatorObservationSchema.TotalSize));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.ArenaCenterLocalX), Is.EqualTo(-0.5f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.ArenaCenterLocalZ), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.HealthRatio), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.MaxHealthLogRatio), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AttackLogRatio), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AttackRangeRatio), Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.MoveSpeedRatio), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AttackCooldownRatio), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.DamageToNearestEnemyMaxHealthRatio), Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.NearestEnemyDamageToSelfMaxHealthRatio), Is.EqualTo(0.2f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.NearestOpponentDistanceRatio), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.CanHitNearestOpponent), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.InNearestOpponentRange), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.NearbyOpponentRatio), Is.EqualTo(1f / GladiatorObservationSchema.OpponentSlots).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.NearbyTeammateRatio), Is.EqualTo(1f / GladiatorObservationSchema.TeammateSlots).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.BoundaryPressure), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.BattleTimeoutRemainingRatio), Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentSmoothedLocalMoveX), Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentSmoothedLocalMoveZ), Is.EqualTo(-0.5f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentSmoothedTurn), Is.EqualTo(0.4f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentPreviousRawLocalMoveX), Is.EqualTo(-0.2f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentPreviousRawLocalMoveZ), Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.AgentPreviousRawTurn), Is.EqualTo(-0.3f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.HasReadySkill), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(SelfObservation(observations, GladiatorSelfObservationIndex.HasTarget), Is.EqualTo(1f).Within(0.0001f));

        int teammateSlotStart = GladiatorObservationSchema.SelfSize;
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.LocalPositionX), Is.EqualTo(0f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.LocalPositionZ), Is.EqualTo(-0.2f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.HealthRatio), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.MaxHealthLogRatio), Is.EqualTo(Mathf.Log10(0.5f) / 3f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.AttackLogRatio), Is.EqualTo(Mathf.Log10(0.5f) / 3f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.AttackRangeRatio), Is.EqualTo(0.05f).Within(0.0001f));
        Assert.That(UnitObservation(observations, teammateSlotStart, GladiatorUnitObservationIndex.MoveSpeedRatio), Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void Write_DisabledSelfWritesAllZeroes()
    {
        BattleUnitCombatState self = CreateState(
            unitNumber: 1,
            teamId: BattleTeamIds.Player,
            maxHealth: 100f,
            currentHealth: 100f,
            attack: 10f,
            attackSpeed: 1f,
            moveSpeed: 5f,
            attackRange: 2f,
            position: Vector3.zero,
            bodyRadius: 1f
        );
        self.ApplyDamage(100f);

        var sensor = new VectorSensor(GladiatorObservationSchema.TotalSize);
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(
                self,
                null,
                null,
                new GladiatorObservationStats(100f, 10f, 5f),
                BattleUnitPose.Default,
                Vector3.zero,
                20f,
                1f,
                Vector2.zero,
                0f,
                Vector2.zero,
                0f
            )
        );

        ReadOnlyCollection<float> observations = GetObservations(sensor);

        Assert.That(observations.Count, Is.EqualTo(GladiatorObservationSchema.TotalSize));
        for (int i = 0; i < observations.Count; i++)
        {
            Assert.That(observations[i], Is.EqualTo(0f).Within(0.0001f));
        }
    }

    private static BattleUnitCombatState CreateState(
        int unitNumber,
        BattleTeamId teamId,
        float maxHealth,
        float currentHealth,
        float attack,
        float attackSpeed,
        float moveSpeed,
        float attackRange,
        Vector3 position,
        float bodyRadius
    )
    {
        var snapshot = new BattleUnitSnapshot(
            sourceRuntimeId: unitNumber,
            teamId: teamId,
            displayName: $"Unit {unitNumber}",
            level: 1,
            loyalty: 0,
            maxHealth: maxHealth,
            currentHealth: currentHealth,
            attack: attack,
            attackSpeed: attackSpeed,
            moveSpeed: moveSpeed,
            attackRange: attackRange,
            gladiatorClass: null,
            trait: null,
            personality: null,
            equippedPerk: null,
            weaponType: WeaponType.None,
            leftWeaponPrefab: null,
            rightWeaponPrefab: null,
            weaponSkillId: WeaponSkillId.None,
            customizeIndicates: null,
            isRanged: false,
            useProjectile: false,
            portraitSprite: null
        );
        var state = new BattleUnitCombatState(snapshot, unitNumber, teamId);
        state.SetBodyRadius(bodyRadius);
        state.SyncPosition(position);
        return state;
    }

    private static ReadOnlyCollection<float> GetObservations(VectorSensor sensor)
    {
        MethodInfo method = typeof(VectorSensor).GetMethod(
            "GetObservations",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        return (ReadOnlyCollection<float>)method.Invoke(sensor, null);
    }

    private static float SelfObservation(
        ReadOnlyCollection<float> observations,
        GladiatorSelfObservationIndex index
    ) => observations[(int)index];

    private static float UnitObservation(
        ReadOnlyCollection<float> observations,
        int slotStartIndex,
        GladiatorUnitObservationIndex index
    ) => observations[slotStartIndex + (int)index];
}
