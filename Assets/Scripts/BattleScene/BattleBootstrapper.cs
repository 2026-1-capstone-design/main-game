using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleBootstrapper
{
    public static SpawnResult SpawnUnits(
        BattleStartPayload payload,
        GameObject runtimeUnitRootPrefab,
        Transform runtimeUnitRoot,
        Vector3[] allyPositions,
        Vector3[] enemyPositions,
        BattleSceneContext context)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        if (runtimeUnitRootPrefab == null)
            throw new ArgumentNullException(nameof(runtimeUnitRootPrefab));
        if (allyPositions == null)
            throw new ArgumentNullException(nameof(allyPositions));
        if (enemyPositions == null)
            throw new ArgumentNullException(nameof(enemyPositions));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var spawnedUnits = new List<BattleRuntimeUnit>(12);
        Transform parent = runtimeUnitRoot != null ? runtimeUnitRoot : context.BattlefieldCollider.transform;

        bool allySpawned = SpawnTeam(
            payload.AllyUnits,
            allyPositions,
            isEnemy: false,
            unitNumberStart: 1,
            runtimeUnitRootPrefab,
            parent,
            context.BattlefieldCollider,
            spawnedUnits);

        bool enemySpawned = SpawnTeam(
            payload.EnemyUnits,
            enemyPositions,
            isEnemy: true,
            unitNumberStart: 7,
            runtimeUnitRootPrefab,
            parent,
            context.BattlefieldCollider,
            spawnedUnits);

        if (!allySpawned || !enemySpawned)
        {
            DestroySpawnedUnits(spawnedUnits);
            throw new InvalidOperationException("Team spawning failed.");
        }

        return new SpawnResult(spawnedUnits);
    }

    public static void InitializeSimulation(
        SpawnResult spawned,
        BattleSceneContext context,
        BattleStartPayload payload)
    {
        if (spawned == null)
            throw new ArgumentNullException(nameof(spawned));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        context.SimulationManager.Initialize(
            spawned.Units,
            context.BattlefieldCollider,
            payload: payload);
    }

    public static void InitializeUI(SpawnResult spawned, BattleSceneContext context)
    {
        if (spawned == null)
            throw new ArgumentNullException(nameof(spawned));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (context.SceneUI != null)
        {
            context.SceneUI.Initialize();
            context.SceneUI.HideAll();
        }

        if (context.StatusGridUI != null)
        {
            context.StatusGridUI.Initialize(context.SimulationManager, spawned.Units, context.SceneUI);
            context.StatusGridUI.BindUnits(spawned.Units);
            context.StatusGridUI.Refresh();
        }

        if (context.OrdersManager != null)
        {
            context.OrdersManager.Initialize(spawned.Units, context.BattlefieldCollider);
        }

        if (context.SceneUI != null)
            context.SceneUI.RefreshSpeedText();
    }

    public static SpawnResult Bootstrap(
        BattleStartPayload payload,
        GameObject runtimeUnitRootPrefab,
        Transform runtimeUnitRoot,
        Vector3[] allyPositions,
        Vector3[] enemyPositions,
        BattleSceneContext context)
    {
        SpawnResult spawned = SpawnUnits(
            payload,
            runtimeUnitRootPrefab,
            runtimeUnitRoot,
            allyPositions,
            enemyPositions,
            context);
        InitializeSimulation(spawned, context, payload);
        InitializeUI(spawned, context);
        return spawned;
    }

    private static bool SpawnTeam(
        IReadOnlyList<BattleUnitSnapshot> snapshots,
        Vector3[] positions,
        bool isEnemy,
        int unitNumberStart,
        GameObject runtimeUnitRootPrefab,
        Transform parent,
        BoxCollider battlefieldCollider,
        List<BattleRuntimeUnit> destination)
    {
        if (snapshots == null || positions == null)
            return false;

        int spawnCount = Mathf.Min(6, Mathf.Min(snapshots.Count, positions.Length));

        for (int i = 0; i < spawnCount; i++)
        {
            BattleUnitSnapshot snapshot = snapshots[i];
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
            runtimeUnit.Initialize(snapshot.Clone(), unitNumberStart + i, isEnemy);
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
