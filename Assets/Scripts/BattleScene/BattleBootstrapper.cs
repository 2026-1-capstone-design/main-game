using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleBootstrapper
{
    public static SpawnResult SpawnUnits(
        BattleStartPayload payload,
        GameObject runtimeUnitRootPrefab,
        Transform runtimeUnitRoot,
        IReadOnlyDictionary<BattleTeamId, Vector3[]> spawnPositionsByTeam,
        BattleSceneContext context
    )
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (runtimeUnitRootPrefab == null)
            throw new ArgumentNullException(nameof(runtimeUnitRootPrefab));
        if (spawnPositionsByTeam == null)
            throw new ArgumentNullException(nameof(spawnPositionsByTeam));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var spawnedUnits = new List<BattleRuntimeUnit>(12);
        Transform parent = runtimeUnitRoot != null ? runtimeUnitRoot : context.BattlefieldCollider.transform;

        for (int i = 0; i < payload.Teams.Count; i++)
        {
            BattleTeamEntry team = payload.Teams[i];
            if (team == null)
            {
                continue;
            }

            if (!spawnPositionsByTeam.TryGetValue(team.TeamId, out Vector3[] positions) || positions == null)
            {
                DestroySpawnedUnits(spawnedUnits);
                throw new InvalidOperationException($"Missing spawn positions for team {team.TeamId.Value}.");
            }

            bool teamSpawned = SpawnTeam(
                payload,
                team,
                positions,
                runtimeUnitRootPrefab,
                parent,
                context.BattlefieldCollider,
                spawnedUnits
            );

            if (!teamSpawned)
            {
                DestroySpawnedUnits(spawnedUnits);
                throw new InvalidOperationException($"Team spawning failed. TeamId={team.TeamId.Value}");
            }
        }

        return new SpawnResult(spawnedUnits);
    }

    public static void InitializeSimulation(SpawnResult spawned, BattleSceneContext context, BattleStartPayload payload)
    {
        if (spawned == null)
            throw new ArgumentNullException(nameof(spawned));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.SimulationManager.Initialize(spawned.Units, context.BattlefieldCollider, payload: payload);
    }

    public static void InitializeUI(SpawnResult spawned, BattleSceneContext context, BattleStartPayload payload)
    {
        if (spawned == null)
            throw new ArgumentNullException(nameof(spawned));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        BattleRosterProjection rosterProjection = new BattleRosterProjection(payload);

        if (context.SceneUI != null)
        {
            context.SceneUI.Initialize();
            context.SceneUI.HideAll();
        }

        if (context.StatusGridUI != null)
        {
            context.StatusGridUI.Initialize(
                context.SimulationManager,
                spawned.Units,
                rosterProjection,
                context.SceneUI
            );
            context.StatusGridUI.BindUnits(spawned.Units, rosterProjection);
            context.StatusGridUI.Refresh();
        }

        if (context.OrdersManager != null)
        {
            context.OrdersManager.Initialize(spawned.Units, rosterProjection, context.BattlefieldCollider);
        }

        if (context.SceneUI != null)
            context.SceneUI.RefreshSpeedText();
    }

    public static SpawnResult Bootstrap(
        BattleStartPayload payload,
        GameObject runtimeUnitRootPrefab,
        Transform runtimeUnitRoot,
        IReadOnlyDictionary<BattleTeamId, Vector3[]> spawnPositionsByTeam,
        BattleSceneContext context
    )
    {
        SpawnResult spawned = SpawnUnits(
            payload,
            runtimeUnitRootPrefab,
            runtimeUnitRoot,
            spawnPositionsByTeam,
            context
        );
        InitializeSimulation(spawned, context, payload);
        InitializeUI(spawned, context, payload);
        return spawned;
    }

    private static bool SpawnTeam(
        BattleStartPayload payload,
        BattleTeamEntry teamEntry,
        Vector3[] positions,
        GameObject runtimeUnitRootPrefab,
        Transform parent,
        SphereCollider battlefieldCollider,
        List<BattleRuntimeUnit> destination
    )
    {
        if (payload == null || teamEntry == null || teamEntry.Units == null || positions == null)
            return false;

        int spawnCount = Mathf.Min(teamEntry.Units.Count, positions.Length);

        for (int i = 0; i < spawnCount; i++)
        {
            BattleUnitSnapshot snapshot = teamEntry.Units[i];
            if (snapshot == null)
                continue;

            GameObject runtimeRoot = UnityEngine.Object.Instantiate(runtimeUnitRootPrefab, parent);
            BattleRuntimeUnit runtimeUnit = runtimeRoot.GetComponentInChildren<BattleRuntimeUnit>(true);

            if (runtimeUnit == null)
            {
                UnityEngine.Object.Destroy(runtimeRoot);
                return false;
            }

            runtimeUnit.SetRuntimeRootObject(runtimeRoot);
            runtimeUnit.Initialize(
                snapshot.Clone(),
                payload.AllocateUnitNumber(teamEntry.TeamId, i),
                teamEntry.TeamId,
                teamEntry.IsPlayerOwned
            );
            runtimeUnit.PlaceAt(positions[i], battlefieldCollider.transform);
            destination.Add(runtimeUnit);
        }

        return true;
    }

    private static void DestroySpawnedUnits(List<BattleRuntimeUnit> spawnedUnits)
    {
        if (spawnedUnits == null)
            return;

        for (int i = 0; i < spawnedUnits.Count; i++)
        {
            BattleRuntimeUnit unit = spawnedUnits[i];
            if (unit == null)
                continue;

            GameObject rootObject = unit.RuntimeRootObject;
            if (rootObject != null)
                UnityEngine.Object.Destroy(rootObject);
        }
    }
}
