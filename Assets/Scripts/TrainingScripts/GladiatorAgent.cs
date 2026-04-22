using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size        = 85
//   Discrete Branches = 2
//     Branch 0 Size = 8   (이동/공격 — 상호 배타)
//     Branch 1 Size = 3   (회전 — 이동/공격과 동시 가능)
//
// Observation (85 floats) — 검투사 시점 기준, 모든 좌표는 자신이 바라보는 방향 기준 로컬 프레임 (월드 단위, 비정규화):
//   자신      (8):      경기장 중심 상대좌표(x,z), 체력, 최대 체력, 공격력, 사거리, 이동속도, 공격 쿨타임
//   내 팀 동료 (5 × 7): UnitNumber 오름차순 고정 슬롯 — 상대좌표(x,z), 체력, 최대 체력, 공격력, 사거리, 이동속도
//   상대팀    (6 × 7): UnitNumber 오름차순 고정 슬롯 — 상대좌표(x,z), 체력, 최대 체력, 공격력, 사거리, 이동속도
//   사망/없는 슬롯은 전부 0, action masking으로 해당 슬롯 공격 비활성화
//   좌표를 정규화하지 않는 이유: 에이전트가 사거리(AttackRange)와 거리 벡터를 직접 비교할 수 있어야 함
//
// Action:
//   Branch 0 (이동/공격): 0=멈춤  1=앞으로  2~7=상대팀 고정 슬롯 0~5 공격
//   Branch 1 (회전):      0=없음  1=왼쪽    2=오른쪽
public class GladiatorAgent : Agent
{
    private const float RotationSpeedDegPerSec = 240f;
    private const int TeammateSlots = 5;
    private const int OpponentSlots = 6;

    // 유효하지 않은 행동(사거리 밖 공격, 쿨타임 중 공격, 유효하지 않은 대상 공격)에 부과하는 패널티.
    // 값이 크면 에이전트가 해당 행동을 강하게 회피한다.
    private const float InvalidActionPenalty = -10f;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;

    private float _prevDistToNearestEnemy;

    // TrainingBootstrapper가 에피소드마다 호출해 새 유닛을 연결한다.
    public void Initialize(BattleRuntimeUnit unit, BattleSceneFlowManager flowManager)
    {
        // 이전 유닛 이벤트 구독 해제
        if (_selfUnit != null)
        {
            _selfUnit.State.OnDamageTaken -= HandleDamageTaken;
            _selfUnit.State.OnDied -= HandleSelfDied;
            _selfUnit.OnAttackLanded -= HandleAttackLanded;
        }

        _selfUnit = unit;
        _flowManager = flowManager;

        BoxCollider col = flowManager?.BattlefieldCollider;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        _arenaExtentsMin = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : float.MaxValue;

        if (_selfUnit != null)
        {
            _selfUnit.SetExternallyControlled(true);
            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
        }

        _prevDistToNearestEnemy = GetDistToNearestOpponent();
    }


    // ── 이벤트 핸들러 ──────────────────────────────────────────────

    private void HandleDamageTaken(float damage)
    {
        AddReward(-0.1f);
    }

    private void HandleSelfDied()
    {
        AddReward(-5f);
        EndEpisode();
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, bool wasKill)
    {
        AddReward(10f);
        if (wasKill)
        {
            AddReward(100f);
            EndEpisode();
        }
    }

    // ── 관측 수집 ──────────────────────────────────────────────────

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            for (int i = 0; i < 85; i++)
                sensor.AddObservation(0f);
            return;
        }

        // 자신 (8 floats): 경기장 중심 상대좌표 + 스탯
        Vector2 arenaLocal = WorldToLocal(_arenaCenter - _selfUnit.Position);
        sensor.AddObservation(arenaLocal.x);
        sensor.AddObservation(arenaLocal.y);
        sensor.AddObservation(_selfUnit.CurrentHealth);
        sensor.AddObservation(_selfUnit.MaxHealth);
        sensor.AddObservation(_selfUnit.Attack);
        sensor.AddObservation(_selfUnit.AttackRange);
        sensor.AddObservation(_selfUnit.MoveSpeed);
        sensor.AddObservation(_selfUnit.AttackCooldownRemaining);

        // 내 팀 동료 (5 고정 슬롯 × 7 floats)
        AddUnitSlotObservations(sensor, GetTeammatesSorted(), TeammateSlots);

        // 상대팀 (6 고정 슬롯 × 7 floats)
        AddUnitSlotObservations(sensor, GetOpponentsSorted(), OpponentSlots);
    }

    private void AddUnitSlotObservations(VectorSensor sensor, List<BattleRuntimeUnit> units, int slots)
    {
        for (int i = 0; i < slots; i++)
        {
            BattleRuntimeUnit unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                for (int j = 0; j < 7; j++)
                    sensor.AddObservation(0f);
            }
            else
            {
                Vector2 localPos = WorldToLocal(unit.Position - _selfUnit.Position);
                sensor.AddObservation(localPos.x);
                sensor.AddObservation(localPos.y);
                sensor.AddObservation(unit.CurrentHealth);
                sensor.AddObservation(unit.MaxHealth);
                sensor.AddObservation(unit.Attack);
                sensor.AddObservation(unit.AttackRange);
                sensor.AddObservation(unit.MoveSpeed);
            }
        }
    }

    // ── 액션 마스킹 ───────────────────────────────────────────────
    // 마스킹은 유효하지 않은 공격 액션을 원천 차단하는 1차 방어선이다.
    // OnActionReceived의 패널티+EndEpisode는 Heuristic 등 마스킹이 우회될 때만 발동하는 안전망이다.

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled || _flowManager == null)
            return;

        // actionMask.SetActionEnabled(0, 0, false);

        // 쿨타임 또는 공격 중이면 모든 공격 슬롯 비활성화

        bool attackBlocked = _selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking;

        List<BattleRuntimeUnit> opponents = GetOpponentsSorted();
        for (int i = 0; i < OpponentSlots; i++)
        {
            BattleRuntimeUnit opp = i < opponents.Count ? opponents[i] : null;
            bool invalid = opp == null || opp.IsCombatDisabled
                           || attackBlocked
                           || IsOutOfAttackRange(opp);
            if (invalid)
                actionMask.SetActionEnabled(0, i + 2, false);
        }
    }

    private bool IsOutOfAttackRange(BattleRuntimeUnit target)
    {
        if (target == null || _selfUnit == null)
            return true;
        Vector3 delta = target.Position - _selfUnit.Position;
        delta.y = 0f;
        float effectiveRange = _selfUnit.BodyRadius + target.BodyRadius + _selfUnit.AttackRange + 0.05f;
        return delta.magnitude > effectiveRange;
    }

    // ── 액션 수신 및 보상 함수 ────────────────────────────────────

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
            return;

        int mainAction = actions.DiscreteActions[0];
        int rotateAction = actions.DiscreteActions[1];

        // 매 틱 생존 패널티
        // AddReward(-0.001f);

        // 경기장 경계 접촉 패널티
        float playableRadius = _arenaExtentsMin - _selfUnit.BodyRadius;
        Vector3 flatPos = new Vector3(_selfUnit.Position.x, 0f, _selfUnit.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        float distFromCenter = Vector3.Distance(flatPos, flatCenter);
        if (distFromCenter >= playableRadius)
            AddReward(-1f);

        // Average-distance shaping: closer to enemies gives a small bonus.
        float avgDist = GetAverageDistToOpponents();
        if (avgDist < float.MaxValue)
        {
            float closeness = 1f / (1f + avgDist);
            AddReward(closeness * 0.005f);
        }

        // 접근 보상: 전진 중에 가장 가까운 적과의 거리가 줄었을 때
        // float currentDist = GetDistToNearestOpponent();
        // if (mainAction == 1 && currentDist < _prevDistToNearestEnemy && currentDist < float.MaxValue)
        //     AddReward(0.05f);
        // _prevDistToNearestEnemy = currentDist;


        float rotDelta = rotateAction switch
        {
            1 => -RotationSpeedDegPerSec,
            2 => RotationSpeedDegPerSec,
            _ => 0f
        };

        if (mainAction == 1) // 앞으로 이동
        {
            _selfUnit.SetExternalMovement(_selfUnit.transform.forward, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
        }
        else if (mainAction >= 2) // 공격: 상대팀 고정 슬롯 0~5
        {
            int opponentSlot = mainAction - 2;
            List<BattleRuntimeUnit> opponents = GetOpponentsSorted();
            BattleRuntimeUnit target = opponentSlot < opponents.Count ? opponents[opponentSlot] : null;

            // 공격 쿨타임 또는 공격 애니메이션 중 공격 시도 → 패널티 후 Episode 리셋
            if (_selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking)
            {
                AddReward(InvalidActionPenalty);
                // EndEpisode();
                return;
            }

            // 유효하지 않은 대상(null 또는 이미 사망) 공격 시도 → 패널티 후 Episode 리셋
            // Action Masking으로 차단되지 않은 경우(Heuristic 등)에 대한 방어 처리
            if (target == null || target.IsCombatDisabled)
            {
                AddReward(InvalidActionPenalty);
                // EndEpisode();
                return;
            }

            // 사거리 밖의 유효한 타겟 공격 시도 → 패널티 후 Episode 리셋
            {
                Vector3 delta = target.Position - _selfUnit.Position;
                delta.y = 0f;
                float dist = delta.magnitude;
                float effectiveRange = _selfUnit.BodyRadius + target.BodyRadius + _selfUnit.AttackRange + 0.05f;
                if (dist > effectiveRange)
                {
                    AddReward(InvalidActionPenalty);
                    // EndEpisode();
                    return;
                }
            }

            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(
                target != null && !target.IsCombatDisabled ? target : null);
        }
        else // 멈춤
        {
            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
        }
    }

    public override void OnEpisodeBegin()
    {
        // 전장 리셋 없이 Episode만 재시작(패널티 리셋)한 경우 동일 유닛을 계속 제어한다.
        // 전장 리셋 후에는 TrainingBootstrapper가 Initialize()를 다시 호출한다.
        _prevDistToNearestEnemy = GetDistToNearestOpponent();
    }

    // ── 전투 종료 보상 ────────────────────────────────────────────

    // 팀 우승/패배 보상 — TrainingBootstrapper가 전투 종료 시 호출한다.
    public void GiveEndReward(bool allyWon)
    {
        // 팀 승패 기반 보상은 사용하지 않는다.
    }

    // ── 유틸리티 ──────────────────────────────────────────────────

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var kb = Keyboard.current;
        var discrete = actionsOut.DiscreteActions;
        if (kb == null)
            return;

        // Branch 0: 이동/공격 (상호 배타 — 이동 우선)
        if (kb.wKey.isPressed)
            discrete[0] = 1;
        else if (kb.jKey.isPressed)
            discrete[0] = 2;
        else if (kb.kKey.isPressed)
            discrete[0] = 3;
        else if (kb.lKey.isPressed)
            discrete[0] = 4;
        else if (kb.uKey.isPressed)
            discrete[0] = 5;
        else if (kb.iKey.isPressed)
            discrete[0] = 6;
        else if (kb.oKey.isPressed)
            discrete[0] = 7;
        else
            discrete[0] = 0;

        // Branch 1: 회전 (Q=왼쪽, E=오른쪽)
        if (kb.qKey.isPressed)
            discrete[1] = 1;
        else if (kb.eKey.isPressed)
            discrete[1] = 2;
        else
            discrete[1] = 0;
    }

    // 월드 델타 벡터를 자신의 로컬 프레임(right/forward)으로 변환한다.
    // 사거리와 거리 비교가 가능하도록 정규화하지 않는다.
    private Vector2 WorldToLocal(Vector3 worldDelta)
    {
        float x = Vector3.Dot(worldDelta, _selfUnit.transform.right);
        float z = Vector3.Dot(worldDelta, _selfUnit.transform.forward);
        return new Vector2(x, z);
    }

    private BattleRuntimeUnit GetNearestOpponentUnit()
    {
        if (_selfUnit == null || _flowManager == null)
            return null;
        BattleRuntimeUnit nearest = null;
        float minSqr = float.MaxValue;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy == _selfUnit.IsEnemy)
                continue;
            float sqr = (unit.Position - _selfUnit.Position).sqrMagnitude;
            if (sqr < minSqr)
            { minSqr = sqr; nearest = unit; }
        }
        return nearest;
    }

    private float GetDistToNearestOpponent()
    {
        if (_selfUnit == null || _flowManager == null)
            return float.MaxValue;

        float minDist = float.MaxValue;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy == _selfUnit.IsEnemy)
                continue;
            Vector3 delta = unit.Position - _selfUnit.Position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < minDist)
                minDist = dist;
        }
        return minDist;
    }

    private float GetAverageDistToOpponents()
    {
        if (_selfUnit == null || _flowManager == null)
            return float.MaxValue;

        float total = 0f;
        int count = 0;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy == _selfUnit.IsEnemy)
                continue;

            Vector3 delta = unit.Position - _selfUnit.Position;
            delta.y = 0f;
            total += delta.magnitude;
            count++;
        }

        if (count == 0)
            return float.MaxValue;

        return total / count;
    }

    private List<BattleRuntimeUnit> GetTeammatesSorted()
    {
        var list = new List<BattleRuntimeUnit>();
        if (_flowManager == null || _selfUnit == null)
            return list;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit != null && unit != _selfUnit && unit.IsEnemy == _selfUnit.IsEnemy)
                list.Add(unit);
        }
        list.Sort((a, b) => a.UnitNumber.CompareTo(b.UnitNumber));
        return list;
    }

    private List<BattleRuntimeUnit> GetOpponentsSorted()
    {
        var list = new List<BattleRuntimeUnit>();
        if (_flowManager == null || _selfUnit == null)
            return list;
        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit != null && unit.IsEnemy != _selfUnit.IsEnemy)
                list.Add(unit);
        }
        list.Sort((a, b) => a.UnitNumber.CompareTo(b.UnitNumber));
        return list;
    }
}
