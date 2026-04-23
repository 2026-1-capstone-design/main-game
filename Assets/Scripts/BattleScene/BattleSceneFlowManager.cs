using System;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField]
    private BoxCollider battlefieldCollider;

    [SerializeField]
    private GameObject runtimeUnitRootPrefab;

    [SerializeField]
    private Transform runtimeUnitRoot;

    [SerializeField]
    private Transform[] allyPlaceholders = new Transform[6];

    [SerializeField]
    private Transform[] enemyPlaceholders = new Transform[6];

    [Header("Battle")]
    [SerializeField]
    private BattleSimulationManager battleSimulationManager;

    [SerializeField]
    private BattleStatusGridUIManager battleStatusGridUIManager;

    [SerializeField]
    private BattleSceneUIManager battleSceneUIManager;

    [SerializeField]
    private BattleOrdersManager battleOrdersManager;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>();

    // BattleScene 진입 시 payload를 clone해 보관. F7 재시작 시 이 snapshot을 다시 clone해서 사용한다.
    private BattleStartPayload _initialPayloadSnapshot;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;

    // 유닛 스폰 및 초기화가 완료됐을 때 발화. 초기 진입과 F7 재시작 모두 호출된다.
    public event Action OnUnitsSpawned;

    private void Start()
    {
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

        // 아군: 유닛 번호 1~6, 적군: 유닛 번호 7~12
        bool allyOk = SpawnTeam(payload.AllyUnits, allyPlaceholders, false, 1);
        bool enemyOk = SpawnTeam(payload.EnemyUnits, enemyPlaceholders, true, 7);

        if (!allyOk || !enemyOk)
        {
            return false;
        }

        battleOrdersManager.Initialize(_runtimeUnits, battlefieldCollider);

        // SimulationManager�� BoxCollider�� �����մϴ�.
        battleSimulationManager.Initialize(
            _runtimeUnits,
            battlefieldCollider,
            battleStatusGridUIManager,
            battleSceneUIManager,
            payload
        );

        if (battleSceneUIManager != null)
        {
            battleSceneUIManager.RefreshSpeedText();
        }

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
    }

    private bool ValidateBootstrapSetup(BattleStartPayload payload)
    {
        if (payload == null)
            return false;
        if (runtimeUnitRootPrefab == null)
            return false;
        if (battleSimulationManager == null)
            return false;
        if (battleOrdersManager == null)
            return false;
        if (battlefieldCollider == null)
        {
            Debug.LogError(
                "[BattleSceneFlowManager] battlefieldCollider is not assigned. Please assign a BoxCollider.",
                this
            );
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

    // Root 프리팹 전체를 instantiate한 뒤, 내부 자식 BattleRuntimeUnit 컴포넌트를
    // GetComponentInChildren으로 찾는다. 실제 위치/배치는 Root RectTransform(BoxCollider) 기준.
    private bool SpawnTeam(
        IReadOnlyList<BattleUnitSnapshot> snapshots,
        Transform[] placeholders,
        bool isEnemy,
        int unitNumberStart
    )
    {
        if (snapshots == null)
            return false;

        int spawnCount = Mathf.Min(6, Mathf.Min(snapshots.Count, placeholders.Length));
        Transform parent = runtimeUnitRoot != null ? runtimeUnitRoot : battlefieldCollider.transform;

        for (int i = 0; i < spawnCount; i++)
        {
            BattleUnitSnapshot snapshot = snapshots[i];
            Transform placeholder = placeholders[i];

            if (snapshot == null)
                continue;
            if (placeholder == null)
                return false;

            GameObject runtimeRoot = Instantiate(runtimeUnitRootPrefab, parent);
            BattleRuntimeUnit runtimeUnit = runtimeRoot.GetComponentInChildren<BattleRuntimeUnit>(true);

            if (runtimeUnit == null)
            {
                Destroy(runtimeRoot);
                return false;
            }

            runtimeUnit.Initialize(snapshot.Clone(), unitNumberStart + i, isEnemy);
            runtimeUnit.PlaceOnBattlefieldPlaceholder(placeholder, battlefieldCollider.transform);
            // �ʱ� ���� �� �� ������ Ƣ��� ������ �ִٸ� Ŭ���� ó��
            //runtimeUnit.ClampInsideBattlefield(battlefieldCollider);

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
