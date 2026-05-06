using System.Collections.Generic;
using UnityEngine;

// 전투 중 소환할 유닛의 원본, 팀, 위치, 임시 지속시간을 담는 요청이다.
public struct BattleSummonRequest
{
    public BattleRuntimeUnit Source;
    public BattleTeamId TeamId;
    public BattleUnitSnapshot Snapshot;
    public Vector3 SpawnPosition;
    public float Duration;
}

// 효과 구현체가 전투 런타임 유닛 목록을 안전한 경로로 변경하기 위한 계약이다.
public interface IBattleRosterMutationSink
{
    BattleRuntimeUnit Summon(BattleSummonRequest request);
    void ChangeTeam(BattleRuntimeUnit unit, BattleTeamId newTeamId, float duration);
    void RestoreTeam(BattleRuntimeUnit unit);
    void DisableCommandAndSkill(BattleRuntimeUnit unit, float duration);
}

// 전투 중 런타임 유닛 구성, 임시 팀 변경, 명령/스킬 금지 상태를 관리한다.
// BattleSimulationManager의 리스트와 state 역조회 딕셔너리를 직접 공유받아 스냅샷 재빌드 경로를 유지한다.
public sealed class BattleRosterMutationSystem : IBattleRosterMutationSink
{
    private readonly List<TeamChange> _teamChanges = new List<TeamChange>();
    private readonly List<TimedUnitBlock> _commandBlocks = new List<TimedUnitBlock>();
    private readonly List<PendingSummon> _pendingSummons = new List<PendingSummon>();
    private List<BattleRuntimeUnit> _runtimeUnits;
    private List<BattleUnitCombatState> _unitStates;
    private Dictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;
    private SphereCollider _battlefieldCollider;
    private BattleTeamId _playerTeamId;
    private float _battleTime;
    private int _nextSummonedUnitNumber = 10000;

    public void Configure(
        List<BattleRuntimeUnit> runtimeUnits,
        List<BattleUnitCombatState> unitStates,
        Dictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        SphereCollider battlefieldCollider,
        BattleTeamId playerTeamId
    )
    {
        _runtimeUnits = runtimeUnits;
        _unitStates = unitStates;
        _runtimeUnitByState = runtimeUnitByState;
        _battlefieldCollider = battlefieldCollider;
        _playerTeamId = playerTeamId;
    }

    public void Clear()
    {
        DestroyPendingSummons();
        _teamChanges.Clear();
        _commandBlocks.Clear();
        _battleTime = 0f;
        _nextSummonedUnitNumber = 10000;
    }

    public void Tick(float battleTime)
    {
        _battleTime = battleTime;

        for (int i = _teamChanges.Count - 1; i >= 0; i--)
        {
            TeamChange change = _teamChanges[i];
            if (change.RestoreAtBattleTime > battleTime)
                continue;

            RestoreTeamAt(i, change);
        }

        for (int i = _commandBlocks.Count - 1; i >= 0; i--)
        {
            if (_commandBlocks[i].UntilBattleTime <= battleTime)
                _commandBlocks.RemoveAt(i);
        }
    }

    public bool IsCommandDisabled(BattleRuntimeUnit unit)
    {
        if (unit == null)
            return false;

        for (int i = 0; i < _commandBlocks.Count; i++)
        {
            if (_commandBlocks[i].Unit == unit)
                return true;
        }

        return false;
    }

    public bool IsSkillDisabled(BattleRuntimeUnit unit) => IsCommandDisabled(unit);

    public BattleRuntimeUnit Summon(BattleSummonRequest request)
    {
        if (request.Source == null || request.Snapshot == null || _runtimeUnits == null)
            return null;

        GameObject prefab = request.Source.RuntimeRootObject;
        if (prefab == null)
            return null;

        Transform parent = _battlefieldCollider != null ? _battlefieldCollider.transform : prefab.transform.parent;
        // TODO: 소환 스킬이 짧은 시간 안에 반복 시전되거나 여러 유닛이 동시에 소환할 수 있는 경우
        // Object.Instantiate를 오브젝트 풀링으로 교체한다. 호출 횟수가 늘어날수록 GC 스파이크가 발생한다.
        GameObject runtimeRoot = Object.Instantiate(prefab, parent);
        BattleRuntimeUnit runtimeUnit = runtimeRoot.GetComponentInChildren<BattleRuntimeUnit>(true);
        if (runtimeUnit == null)
        {
            Object.Destroy(runtimeRoot);
            return null;
        }

        runtimeUnit.SetRuntimeRootObject(runtimeRoot);
        runtimeUnit.Initialize(
            request.Snapshot.Clone(),
            _nextSummonedUnitNumber++,
            request.TeamId,
            request.TeamId == _playerTeamId
        );
        runtimeUnit.PlaceAt(request.SpawnPosition, parent);
        runtimeUnit.ClampInsideBattlefield(_battlefieldCollider);

        _pendingSummons.Add(new PendingSummon(runtimeUnit));
        if (request.Duration > 0f)
            DisableCommandAndSkill(runtimeUnit, request.Duration);

        return runtimeUnit;
    }

    public void FlushPendingSummons()
    {
        if (_pendingSummons.Count == 0 || _runtimeUnits == null)
            return;

        for (int i = 0; i < _pendingSummons.Count; i++)
        {
            BattleRuntimeUnit runtimeUnit = _pendingSummons[i].RuntimeUnit;
            if (runtimeUnit == null || runtimeUnit.State == null)
                continue;

            _runtimeUnits.Add(runtimeUnit);
            _unitStates?.Add(runtimeUnit.State);
            if (_runtimeUnitByState != null)
                _runtimeUnitByState[runtimeUnit.State] = runtimeUnit;
        }

        _pendingSummons.Clear();
    }

    public void ChangeTeam(BattleRuntimeUnit unit, BattleTeamId newTeamId, float duration)
    {
        if (unit == null || unit.State == null)
            return;

        BattleTeamId originalTeamId = unit.TeamId;
        if (originalTeamId == newTeamId)
            return;

        float restoreAtBattleTime = duration > 0f ? _battleTime + duration : float.PositiveInfinity;
        _teamChanges.Add(new TeamChange(unit, originalTeamId, restoreAtBattleTime));
        unit.State.SetTeamId(newTeamId);
        unit.ClearExecutionPlan();
    }

    public void RestoreTeam(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.State == null)
            return;

        for (int i = _teamChanges.Count - 1; i >= 0; i--)
        {
            TeamChange change = _teamChanges[i];
            if (change.Unit != unit)
                continue;

            RestoreTeamAt(i, change);
            return;
        }
    }

    private void RestoreTeamAt(int index, TeamChange change)
    {
        BattleRuntimeUnit unit = change.Unit;
        if (unit != null && unit.State != null)
        {
            unit.State.SetTeamId(change.OriginalTeamId);
            unit.ClearExecutionPlan();
        }

        _teamChanges.RemoveAt(index);
    }

    public void DisableCommandAndSkill(BattleRuntimeUnit unit, float duration)
    {
        if (unit == null || duration <= 0f)
            return;

        _commandBlocks.Add(new TimedUnitBlock(unit, _battleTime + duration));
    }

    private readonly struct TeamChange
    {
        public BattleRuntimeUnit Unit { get; }
        public BattleTeamId OriginalTeamId { get; }
        public float RestoreAtBattleTime { get; }

        public TeamChange(BattleRuntimeUnit unit, BattleTeamId originalTeamId, float restoreAtBattleTime)
        {
            Unit = unit;
            OriginalTeamId = originalTeamId;
            RestoreAtBattleTime = restoreAtBattleTime;
        }
    }

    private readonly struct TimedUnitBlock
    {
        public BattleRuntimeUnit Unit { get; }
        public float UntilBattleTime { get; }

        public TimedUnitBlock(BattleRuntimeUnit unit, float untilBattleTime)
        {
            Unit = unit;
            UntilBattleTime = untilBattleTime;
        }
    }

    // 소환 생성물은 현재 순회 중인 런타임 로스터를 건드리지 않도록 다음 틱 시작까지 대기한다.
    private readonly struct PendingSummon
    {
        public BattleRuntimeUnit RuntimeUnit { get; }

        public PendingSummon(BattleRuntimeUnit runtimeUnit)
        {
            RuntimeUnit = runtimeUnit;
        }
    }

    private void DestroyPendingSummons()
    {
        for (int i = 0; i < _pendingSummons.Count; i++)
        {
            BattleRuntimeUnit runtimeUnit = _pendingSummons[i].RuntimeUnit;
            if (runtimeUnit == null || runtimeUnit.RuntimeRootObject == null)
                continue;

            Object.Destroy(runtimeUnit.RuntimeRootObject);
        }

        _pendingSummons.Clear();
    }
}
