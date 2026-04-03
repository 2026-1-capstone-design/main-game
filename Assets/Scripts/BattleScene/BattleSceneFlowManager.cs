using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSceneFlowManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private RectTransform battlefieldRect;
    [SerializeField] private GameObject runtimeUnitRootPrefab;
    [SerializeField] private RectTransform runtimeUnitRoot;
    [SerializeField] private RectTransform[] allyPlaceholders = new RectTransform[6];
    [SerializeField] private RectTransform[] enemyPlaceholders = new RectTransform[6];

    [Header("Battle")]
    [SerializeField] private BattleSimulationManager battleSimulationManager;
    [SerializeField] private BattleStatusGridUIManager battleStatusGridUIManager;
    [SerializeField] private BattleSceneUIManager battleSceneUIManager;
    [SerializeField] private BattleOrdersManager battleOrdersManager;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();
    private BattleStartPayload _initialPayloadSnapshot;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;

    private void Start()
    {
        BootstrapScene();
    }

    private void BootstrapScene()
    {
        BattleSessionManager battleSessionManager = BattleSessionManager.Instance;
        if (battleSessionManager == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BattleSessionManager.Instance is null.", this);
            return;
        }

        if (!battleSessionManager.TryGetPayload(out BattleStartPayload payload) || payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BattleStartPayload is missing.", this);
            return;
        }

        if (!ValidateBootstrapSetup(payload))
        {
            return;
        }

        _initialPayloadSnapshot = ClonePayload(payload);

        bool success = BootstrapFromPayload(ClonePayload(_initialPayloadSnapshot));
        if (!success)
        {
            return;
        }

        battleSessionManager.ClearPayload();

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneFlowManager] Battle scene bootstrapped. RuntimeUnitCount={_runtimeUnits.Count}",
                this
            );
        }
    }

    public bool RestartCurrentBattle()
    {
        if (_initialPayloadSnapshot == null)
        {
            Debug.LogError("[BattleSceneFlowManager] Restart failed. Initial payload snapshot is null.", this);
            return false;
        }

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.HideAll();
        }

        DestroyRuntimeUnits();

        bool success = BootstrapFromPayload(ClonePayload(_initialPayloadSnapshot));
        if (!success)
        {
            Debug.LogError("[BattleSceneFlowManager] Restart failed during bootstrap.", this);
            return false;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSceneFlowManager] Battle restarted in-place. RuntimeUnitCount={_runtimeUnits.Count}",
                this
            );
        }

        return true;
    }

    private bool BootstrapFromPayload(BattleStartPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BootstrapFromPayload failed. Payload is null.", this);
            return false;
        }

        if (!ValidateBootstrapSetup(payload))
        {
            return false;
        }

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.Initialize();
            battleSceneUIManager.HideAll();
        }

        _runtimeUnits.Clear();

        bool allyOk = SpawnTeam(payload.AllyUnits, allyPlaceholders, false, 1);
        bool enemyOk = SpawnTeam(payload.EnemyUnits, enemyPlaceholders, true, 7);

        if (!allyOk || !enemyOk)
        {
            return false;
        }

        battleOrdersManager.Initialize(_runtimeUnits);

        battleSimulationManager.Initialize(
            _runtimeUnits,
            battlefieldRect,
            battleStatusGridUIManager,
            battleSceneUIManager,
            payload);

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.RefreshSpeedText();
        }

        return true;
    }

    private void DestroyRuntimeUnits()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            GameObject rootObject = unit.RuntimeRootObject;
            if (rootObject != null)
            {
                rootObject.SetActive(false);
                Destroy(rootObject);
            }
        }

        _runtimeUnits.Clear();
    }

    private bool ValidateBootstrapSetup(BattleStartPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] Payload is null.", this);
            return false;
        }

        if (runtimeUnitRootPrefab == null)
        {
            Debug.LogError("[BattleSceneFlowManager] runtimeUnitRootPrefab is not assigned.", this);
            return false;
        }

        if (battleSimulationManager == null)
        {
            Debug.LogError("[BattleSceneFlowManager] battleSimulationManager is not assigned.", this);
            return false;
        }

        if (battleOrdersManager == null)
        {
            Debug.LogError("[BattleSceneFlowManager] battleOrdersManager is not assigned.", this);
            return false;
        }

        if (battlefieldRect == null)
        {
            Debug.LogError("[BattleSceneFlowManager] battlefieldRect is not assigned.", this);
            return false;
        }

        if (allyPlaceholders == null || allyPlaceholders.Length < 6)
        {
            Debug.LogError("[BattleSceneFlowManager] allyPlaceholders must have length 6.", this);
            return false;
        }

        if (enemyPlaceholders == null || enemyPlaceholders.Length < 6)
        {
            Debug.LogError("[BattleSceneFlowManager] enemyPlaceholders must have length 6.", this);
            return false;
        }

        if (payload.AllyUnits == null || payload.AllyUnits.Count == 0)
        {
            Debug.LogError("[BattleSceneFlowManager] Payload ally list is empty.", this);
            return false;
        }

        if (payload.EnemyUnits == null || payload.EnemyUnits.Count == 0)
        {
            Debug.LogError("[BattleSceneFlowManager] Payload enemy list is empty.", this);
            return false;
        }

        return true;
    }

    private bool SpawnTeam(
        IReadOnlyList<BattleUnitSnapshot> snapshots,
        RectTransform[] placeholders,
        bool isEnemy,
        int unitNumberStart)
    {
        if (snapshots == null)
        {
            return false;
        }

        int spawnCount = Mathf.Min(6, Mathf.Min(snapshots.Count, placeholders.Length));
        RectTransform parent = runtimeUnitRoot != null ? runtimeUnitRoot : battlefieldRect;

        for (int i = 0; i < spawnCount; i++)
        {
            BattleUnitSnapshot snapshot = snapshots[i];
            RectTransform placeholder = placeholders[i];

            if (snapshot == null)
            {
                Debug.LogWarning($"[BattleSceneFlowManager] Snapshot is null. Team={(isEnemy ? "Enemy" : "Ally")}, Index={i}", this);
                continue;
            }

            if (placeholder == null)
            {
                Debug.LogError($"[BattleSceneFlowManager] Placeholder is null. Team={(isEnemy ? "Enemy" : "Ally")}, Index={i}", this);
                return false;
            }

            GameObject runtimeRoot = Instantiate(runtimeUnitRootPrefab, parent);
            BattleRuntimeUnit runtimeUnit = runtimeRoot.GetComponentInChildren<BattleRuntimeUnit>(true);

            if (runtimeUnit == null)
            {
                Debug.LogError(
                    $"[BattleSceneFlowManager] BattleRuntimeUnit component not found under runtime root prefab. Team={(isEnemy ? "Enemy" : "Ally")}, Index={i}",
                    this
                );

                Destroy(runtimeRoot);
                return false;
            }

            runtimeUnit.Initialize(snapshot.Clone(), unitNumberStart + i, isEnemy);
            runtimeUnit.PlaceOnBattlefieldPlaceholder(placeholder, battlefieldRect);
            runtimeUnit.ClampInsideBattlefield(battlefieldRect);

            _runtimeUnits.Add(runtimeUnit);
        }

        return true;
    }

    private BattleStartPayload ClonePayload(BattleStartPayload source)
    {
        List<BattleUnitSnapshot> allyUnits = CloneSnapshots(source.AllyUnits);
        List<BattleUnitSnapshot> enemyUnits = CloneSnapshots(source.EnemyUnits);

        return new BattleStartPayload(
            allyUnits,
            enemyUnits,
            source.SelectedEncounterIndex,
            source.EnemyAverageLevel,
            source.PreviewRewardGold,
            source.BattleSeed
        );
    }

    private List<BattleUnitSnapshot> CloneSnapshots(IReadOnlyList<BattleUnitSnapshot> source)
    {
        List<BattleUnitSnapshot> result = new List<BattleUnitSnapshot>();

        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            BattleUnitSnapshot snapshot = source[i];
            if (snapshot != null)
            {
                result.Add(snapshot.Clone());
            }
        }

        return result;
    }
}