using NUnit.Framework;
using UnityEngine;

public sealed class GladiatorStateRosterViewTests
{
    [Test]
    public void ResolveHostileSlot_UsesPayloadTeamLocalOrder()
    {
        BattleStartPayload payload = CreatePayload(teamSize: 3);
        BattleUnitCombatState ally1 = CreateState(payload, BattleTeamIds.Player, 0, Vector3.zero);
        BattleUnitCombatState ally2 = CreateState(payload, BattleTeamIds.Player, 1, Vector3.right);
        BattleUnitCombatState enemy1 = CreateState(payload, BattleTeamIds.Enemy, 0, new Vector3(10f, 0f, 0f));
        BattleUnitCombatState enemy2 = CreateState(payload, BattleTeamIds.Enemy, 1, new Vector3(20f, 0f, 0f));
        BattleUnitCombatState enemy3 = CreateState(payload, BattleTeamIds.Enemy, 2, new Vector3(30f, 0f, 0f));

        var view = new GladiatorStateRosterView(ally2, payload, new[] { enemy3, ally2, enemy1, ally1, enemy2 });

        Assert.That(view.Teammates, Is.EqualTo(new[] { ally1 }));
        Assert.That(view.ResolveHostileSlot(0), Is.SameAs(enemy1));
        Assert.That(view.ResolveHostileSlot(1), Is.SameAs(enemy2));
        Assert.That(view.ResolveHostileSlot(2), Is.SameAs(enemy3));
        Assert.That(view.ResolveHostileSlot(3), Is.Null);
    }

    [Test]
    public void GetDistanceToNearestHostile_IgnoresDisabledStates()
    {
        BattleStartPayload payload = CreatePayload(teamSize: 2);
        BattleUnitCombatState self = CreateState(payload, BattleTeamIds.Player, 0, Vector3.zero);
        BattleUnitCombatState disabledNearEnemy = CreateState(payload, BattleTeamIds.Enemy, 0, new Vector3(2f, 0f, 0f));
        BattleUnitCombatState livingFarEnemy = CreateState(payload, BattleTeamIds.Enemy, 1, new Vector3(5f, 0f, 0f));
        disabledNearEnemy.ApplyDamage(disabledNearEnemy.MaxHealth);

        var view = new GladiatorStateRosterView(self, payload, new[] { livingFarEnemy, disabledNearEnemy, self });

        Assert.That(view.GetDistanceToNearestHostile(self), Is.EqualTo(5f).Within(0.0001f));
    }

    private static BattleStartPayload CreatePayload(int teamSize)
    {
        var playerSnapshots = new BattleUnitSnapshot[teamSize];
        var enemySnapshots = new BattleUnitSnapshot[teamSize];
        for (int i = 0; i < teamSize; i++)
        {
            playerSnapshots[i] = CreateSnapshot(i + 1, BattleTeamIds.Player);
            enemySnapshots[i] = CreateSnapshot(i + 1, BattleTeamIds.Enemy);
        }

        return new BattleStartPayload(
            new[]
            {
                new BattleTeamEntry(BattleTeamIds.Player, isPlayerOwned: true, playerSnapshots),
                new BattleTeamEntry(BattleTeamIds.Enemy, isPlayerOwned: false, enemySnapshots),
            },
            BattleTeamIds.Player,
            selectedEncounterIndex: 0,
            enemyAverageLevel: 1f,
            previewRewardGold: 0,
            battleSeed: 1
        );
    }

    private static BattleUnitCombatState CreateState(
        BattleStartPayload payload,
        BattleTeamId teamId,
        int localIndex,
        Vector3 position
    )
    {
        int unitNumber = payload.AllocateUnitNumber(teamId, localIndex);
        var state = new BattleUnitCombatState(CreateSnapshot(unitNumber, teamId), unitNumber, teamId);
        state.SetBodyRadius(1f);
        state.SyncPosition(position);
        return state;
    }

    private static BattleUnitSnapshot CreateSnapshot(int sourceRuntimeId, BattleTeamId teamId)
    {
        return new BattleUnitSnapshot(
            sourceRuntimeId: sourceRuntimeId,
            teamId: teamId,
            displayName: $"Unit {sourceRuntimeId}",
            level: 1,
            loyalty: 0,
            maxHealth: 100f,
            currentHealth: 100f,
            attack: 10f,
            attackSpeed: 1f,
            moveSpeed: 5f,
            attackRange: 2f,
            gladiatorClass: null,
            trait: null,
            personality: null,
            equippedArtifact: null,
            weaponType: WeaponType.None,
            leftWeaponPrefab: null,
            rightWeaponPrefab: null,
            weaponSkillId: WeaponSkillId.None,
            customizeIndicates: null,
            isRanged: false,
            useProjectile: false,
            portraitSprite: null
        );
    }
}
