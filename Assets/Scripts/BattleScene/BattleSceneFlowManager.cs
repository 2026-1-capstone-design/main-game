using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// BattleSceneFlowManager 책임:
// - BattleSessionManager에서 payload 읽기
// - ally/enemy snapshot 기반 runtime unit 12개 생성 (ally 1~6 / enemy 7~12 번호 부여)
// - Battlefield(BoxCollider) 위 placeholder 기준 배치
// - BattleSimulationManager 초기화
// - BattleSceneUIManager / BattleStatusGridUIManager와 연결
// - 초기 payload snapshot 보관 및 clone 재사용 (F7 in-place restart)
// - 기존 runtime unit destroy 후 재생성
[DisallowMultipleComponent]
public sealed class BattleSceneFlowManager : MonoBehaviour
{
    [Header("Spawn")]
    // RectTransform 대신 3D BoxCollider로 교체된 전장 영역
    [SerializeField] private BoxCollider battlefieldCollider;
    [SerializeField] private GameObject runtimeUnitRootPrefab;
    [SerializeField] private Transform runtimeUnitRoot;
    [SerializeField] private Transform[] allyPlaceholders = new Transform[6];
    [SerializeField] private Transform[] enemyPlaceholders = new Transform[6];

    [Header("Battle")]
    [SerializeField] private BattleSimulationManager battleSimulationManager;
    [SerializeField] private BattleStatusGridUIManager battleStatusGridUIManager;
    [SerializeField] private BattleSceneUIManager battleSceneUIManager;
    [SerializeField] private BattleOrdersManager battleOrdersManager;

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;
    // TrainingScene에서는 false로 설정해 BattleSessionManager 의존성을 우회한다.
    [SerializeField] private bool autoBootstrapFromSessionManager = true;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();
    private SpawnResult _spawnResult;
    // BattleScene 진입 시 payload를 clone해 보관. F7 재시작 시 이 snapshot을 다시 clone해서 사용한다.
    private BattleStartPayload _initialPayloadSnapshot;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;
    public BoxCollider BattlefieldCollider => battlefieldCollider;

    // 유닛 스폰 및 초기화가 완료됐을 때 발화. 초기 진입과 F7 재시작 모두 호출된다.
    public event Action OnUnitsSpawned;

    private void Start()
    {
        if (autoBootstrapFromSessionManager)
            BootstrapScene();
    }

    // BattleScene 진입 시 호출되는 초기 부트스트랩.
    // BattleSessionManager에서 BattleStartPayload를 읽고, clone해서 _initialPayloadSnapshot으로 저장한다.
    // 이후 BattleSessionManager의 payload는 clear한다.
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

    // F7 치트 또는 결과 패널의 재시작 요청 시 호출된다.
    // _initialPayloadSnapshot을 다시 clone해서 기존 runtime unit을 전부 destroy한 뒤 같은 씬 안에서 다시 bootstrap한다.
    // Scene 재로드는 하지 않는다. 결과 패널은 HideAll()로 닫는다.
    public bool RestartCurrentBattle()
    {
        if (_initialPayloadSnapshot == null)
        {
            Debug.LogError("[FlowManager] RestartCurrentBattle() called before initial Bootstrap.", this);
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

    public bool BootstrapFromPayload(BattleStartPayload payload)
    {
        if (payload == null)
        {
            Debug.LogError("[BattleSceneFlowManager] BootstrapFromPayload failed. Payload is null.", this);
            return false;
        }

        if (!ValidateBootstrapSetup(payload))
        {
            Debug.LogError("[BattleSceneFlowManager] BootstrapFromPayload failed. Validation failed.", this);
            return false;
        }

        _initialPayloadSnapshot = ClonePayload(payload);

        Vector3 ExtractPosition(Transform placeholder)
        {
            return placeholder != null ? placeholder.position : Vector3.zero;
        }

        return BootstrapCore(
            ClonePayload(_initialPayloadSnapshot),
            allyPlaceholders.Select(ExtractPosition).ToArray(),
            enemyPlaceholders.Select(ExtractPosition).ToArray());
    }

    // TrainingBootstrapper에서 무작위 배치 시 사용한다.
    public bool ResetAndBootstrap(BattleStartPayload payload, Vector3[] allyPositions, Vector3[] enemyPositions)
    {
        DestroyRuntimeUnits();
        bool success = BootstrapCore(payload, allyPositions, enemyPositions);
        if (verboseLog && success)
            Debug.Log($"[BattleSceneFlowManager] ResetAndBootstrap complete. RuntimeUnitCount={_runtimeUnits.Count}", this);
        return success;
    }

    private bool BootstrapCore(BattleStartPayload payload, Vector3[] allyPositions, Vector3[] enemyPositions)
    {
        if (payload == null)
            return false;

        if (runtimeUnitRootPrefab == null || battleSimulationManager == null || battlefieldCollider == null)
            return false;

        if (payload.AllyUnits == null || payload.AllyUnits.Count == 0)
            return false;
        if (payload.EnemyUnits == null || payload.EnemyUnits.Count == 0)
            return false;

        try
        {
            var context = new BattleSceneContext(
                battleSimulationManager,
                battlefieldCollider,
                battleStatusGridUIManager,
                battleSceneUIManager,
                battleOrdersManager);

            _spawnResult = BattleBootstrapper.Bootstrap(
                payload,
                runtimeUnitRootPrefab,
                runtimeUnitRoot,
                allyPositions,
                enemyPositions,
                context);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BattleSceneFlowManager] BootstrapCore failed. {exception.Message}", this);
            return false;
        }

        _runtimeUnits.Clear();
        if (_spawnResult != null)
            _runtimeUnits.AddRange(_spawnResult.Units);

        OnUnitsSpawned?.Invoke();
        return true;
    }

    private void DestroyRuntimeUnits()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
                continue;

            GameObject rootObject = unit.RuntimeRootObject;
            if (rootObject != null)
            {
                rootObject.SetActive(false);
                Destroy(rootObject);
            }
        }

        _runtimeUnits.Clear();
        _spawnResult = null;
    }

    private bool ValidateBootstrapSetup(BattleStartPayload payload)
    {
        if (payload == null)
            return false;
        if (runtimeUnitRootPrefab == null)
            return false;
        if (battleSimulationManager == null)
            return false;
        if (battlefieldCollider == null)
        {
            Debug.LogError("[BattleSceneFlowManager] battlefieldCollider is not assigned. Please assign a BoxCollider.", this);
            return false;
        }

        if (allyPlaceholders == null || allyPlaceholders.Length < 6)
            return false;
        if (enemyPlaceholders == null || enemyPlaceholders.Length < 6)
            return false;

        if (payload.AllyUnits == null || payload.AllyUnits.Count == 0)
            return false;
        if (payload.EnemyUnits == null || payload.EnemyUnits.Count == 0)
            return false;

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
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            BattleUnitSnapshot snapshot = source[i];
            if (snapshot != null)
                result.Add(snapshot.Clone());
        }

        return result;
    }
}
