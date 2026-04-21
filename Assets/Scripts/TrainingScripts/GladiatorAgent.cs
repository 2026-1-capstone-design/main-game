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

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private Vector3 _arenaCenter;

    // TrainingBootstrapper가 에피소드마다 호출해 새 유닛을 연결한다.
    public void Initialize(BattleRuntimeUnit unit, BattleSceneFlowManager flowManager)
    {
        _selfUnit = unit;
        _flowManager = flowManager;
        BoxCollider col = flowManager != null ? flowManager.BattlefieldCollider : null;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        if (_selfUnit != null)
            _selfUnit.SetExternallyControlled(true);
    }

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

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled || _flowManager == null)
            return;

        List<BattleRuntimeUnit> opponents = GetOpponentsSorted();
        for (int i = 0; i < OpponentSlots; i++)
        {
            bool invalid = i >= opponents.Count || opponents[i].IsCombatDisabled;
            if (invalid)
                actionMask.SetActionEnabled(0, i + 2, false);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
            return;

        int mainAction = actions.DiscreteActions[0];
        int rotateAction = actions.DiscreteActions[1];

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
            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            int opponentSlot = mainAction - 2;
            List<BattleRuntimeUnit> opponents = GetOpponentsSorted();
            BattleRuntimeUnit target = opponentSlot < opponents.Count ? opponents[opponentSlot] : null;
            _selfUnit.SetExternalAttackTarget(
                target != null && !target.IsCombatDisabled ? target : null);
        }
        else // 멈춤
        {
            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
        }

        AddReward(-0.001f);
    }

    public override void OnEpisodeBegin()
    {
        // 리셋은 TrainingBootstrapper가 담당한다.
    }

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

    public void GiveEndReward(bool allyWon)
    {
        bool thisTeamWon = (_selfUnit != null && _selfUnit.IsEnemy) ? !allyWon : allyWon;
        SetReward(thisTeamWon ? 1f : -1f);
    }

    // 월드 델타 벡터를 자신의 로컬 프레임(right/forward)으로 변환한다.
    // 사거리와 거리 비교가 가능하도록 정규화하지 않는다.
    private Vector2 WorldToLocal(Vector3 worldDelta)
    {
        float x = Vector3.Dot(worldDelta, _selfUnit.transform.right);
        float z = Vector3.Dot(worldDelta, _selfUnit.transform.forward);
        return new Vector2(x, z);
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
