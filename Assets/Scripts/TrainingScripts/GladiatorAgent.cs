using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

// BehaviorParameters 설정 (Inspector):
//   Space Size        = 73
//   Discrete Branches = 2
//     Branch 0 Size = 8   (이동/공격 — 상호 배타)
//     Branch 1 Size = 3   (회전 — 이동/공격과 동시 가능)
//
// Observation (73 floats):
//   자신 (7): 바라보는 방향(각도), 체력, 최대 체력, 공격력, 사거리, 이동속도, 공격 쿨타임
//   나머지 11 유닛 × 6: 거리, 체력, 최대 체력, 공격력, 사거리, 이동속도  (사망 슬롯은 전부 0)
//
// Action:
//   Branch 0 (이동/공격): 0=멈춤  1=앞으로  2~7=적 슬롯 1~6 공격 (unit number 7~12)
//   Branch 1 (회전):      0=없음  1=왼쪽    2=오른쪽
public class GladiatorAgent : Agent
{
    private const float MaxNormalizeDistance = 50f;
    private const float RotationSpeedDegPerSec = 240f;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;

    // TrainingBootstrapper가 에피소드마다 호출해 새 유닛을 연결한다.
    public void Initialize(BattleRuntimeUnit unit, BattleSceneFlowManager flowManager)
    {
        _selfUnit = unit;
        _flowManager = flowManager;
        if (_selfUnit != null)
        {
            _selfUnit.SetExternallyControlled(true);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            for (int i = 0; i < 73; i++)
                sensor.AddObservation(0f);
            return;
        }

        // 자신 (7 floats)
        Vector3 fwd = _selfUnit.transform.forward;
        float angle = Mathf.Atan2(fwd.x, fwd.z) / Mathf.PI; // [-1, 1]
        sensor.AddObservation(angle);
        sensor.AddObservation(_selfUnit.CurrentHealth);
        sensor.AddObservation(_selfUnit.MaxHealth);
        sensor.AddObservation(_selfUnit.Attack);
        sensor.AddObservation(_selfUnit.AttackRange);
        sensor.AddObservation(_selfUnit.MoveSpeed);
        sensor.AddObservation(_selfUnit.AttackCooldownRemaining);

        // 나머지 유닛 (unit number 1~12, 자신 제외, 순서 고정) × 6 floats
        int slotsFilled = 0;
        for (int num = 1; num <= 12 && slotsFilled < 11; num++)
        {
            if (num == _selfUnit.UnitNumber)
                continue;

            BattleRuntimeUnit other = FindUnitByNumber(num);
            if (other == null || other.IsCombatDisabled)
            {
                for (int i = 0; i < 6; i++)
                    sensor.AddObservation(0f);
            }
            else
            {
                float dist = Vector3.Distance(_selfUnit.Position, other.Position);
                sensor.AddObservation(Mathf.Clamp01(dist / MaxNormalizeDistance));
                sensor.AddObservation(other.CurrentHealth);
                sensor.AddObservation(other.MaxHealth);
                sensor.AddObservation(other.Attack);
                sensor.AddObservation(other.AttackRange);
                sensor.AddObservation(other.MoveSpeed);
            }

            slotsFilled++;
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
        else if (mainAction >= 2) // 공격 (2→unit7, 3→unit8, ..., 7→unit12)
        {
            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            int targetUnitNumber = mainAction + 5;
            BattleRuntimeUnit target = FindUnitByNumber(targetUnitNumber);
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

        // Branch 1: 회전 (Q=왼쪽, E=오른쪽) — 이동/공격과 동시 가능
        if (kb.qKey.isPressed)
            discrete[1] = 1;
        else if (kb.eKey.isPressed)
            discrete[1] = 2;
        else
            discrete[1] = 0;
    }

    public void GiveEndReward(bool allyWon) => SetReward(allyWon ? 1f : -1f);

    private BattleRuntimeUnit FindUnitByNumber(int unitNumber)
    {
        if (_flowManager == null)
            return null;

        foreach (BattleRuntimeUnit unit in _flowManager.RuntimeUnits)
        {
            if (unit != null && unit.UnitNumber == unitNumber)
                return unit;
        }
        return null;
    }
}
