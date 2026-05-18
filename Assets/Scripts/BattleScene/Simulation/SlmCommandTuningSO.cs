using UnityEngine;

// SLM 명령 처리에 쓰이는 튜닝 수치(거리/threshold/timeout).
// BattleSimulationManager가 보유하고 Planner와 Resolver에 SetTuning으로 전달한다.
// 비할당 시 각 필드의 코드 디폴트로 동작한다.
[CreateAssetMenu(menuName = "Prototype/Content/SlmCommandTuning")]
public sealed class SlmCommandTuningSO : ScriptableObject
{
    [Header("Command Lifecycle")]
    [Tooltip("명령 시퀀스 마지막 액션 종료 후 ML 복귀까지의 grace 시간(초).")]
    public float commandCompletionGraceSec = 0.5f;

    [Header("Action Timeouts (공통 10초)")]
    [Tooltip("attack 액션의 최대 유지 시간(초). 타겟 사망 또는 이 시간 경과 시 종료.")]
    public float attackDurationSec = 10f;

    [Tooltip("move 액션 도달 timeout(초). 종료 조건 충족 못해도 이 시간 지나면 강제 종료.")]
    public float moveTimeoutSec = 10f;

    [Tooltip("skill 액션 timeout(초). 스킬 시전 못해도 이 시간 지나면 강제 종료.")]
    public float skillTimeoutSec = 10f;

    [Tooltip("wait / noSkill의 디폴트 sec. SLM이 지정 안 한 경우 사용.")]
    public float defaultWaitDurationSec = 10f;

    [Header("Escape")]
    [Tooltip("도주 종료 조건: 액터와 상대(SLM 지정 적 / 가까운 적 평균 / 적 군집 중심) 사이 거리가 이 값 이상이면 종료(m).")]
    public float escapeDistance = 10f;

    [Tooltip("'액터를 위협하는 가까운 적'으로 간주할 반경(m).")]
    public float threatNearRadius = 10f;

    [Header("Help")]
    [Tooltip("도움이 필요하다고 간주할 위협 수 threshold. SLM 지정 아군을 PlannedTargetEnemy로 타겟팅하는 적이 이 수 미만이면 명령 종료.")]
    public int helpThreatThreshold = 1;

    [Header("Wait Clamp")]
    [Tooltip("SLM이 지정한 wait/noSkill sec의 최소값(초). actionPolicy.waitDurationMinSec와 일치.")]
    public float waitMinSec = 1f;

    [Tooltip("SLM이 지정한 wait/noSkill sec의 최대값(초). actionPolicy.waitDurationMaxSec와 일치.")]
    public float waitMaxSec = 10f;

    [Header("Flank 곡선 (탈레스 원의 호)")]
    [Tooltip("매 tick 산출하는 곡선 waypoint의 거리(m). 호 위 점에서 접선 방향으로 이 거리만큼 떨어진 점을 목표로 둠.")]
    public float flankStepDistance = 3f;
}
