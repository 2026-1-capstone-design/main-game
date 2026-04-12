using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    [FieldDescription("매 스텝마다 부과되는 기본 패널티. 에이전트가 불필요하게 시간을 끌지 않도록 유도한다.")]
    public float step = -0.001f;

    [FieldDescription("적에게 가까워질수록(거리 감소량 비례) 부여되는 보상. 적극적인 교전을 유도한다.")]
    public float approach = 0.0005f;

    [FieldDescription("후퇴가 적절한 상황에서 거리를 벌릴수록(거리 증가량 비례) 부여되는 보상. 전략적 후퇴를 유도한다.")]
    public float retreatDistance = 0.0005f;

    [FieldDescription("공격 범위 밖에서 기본 공격을 시도할 때 부여되는 보상. 사정거리 밖에서도 공격 의지를 유지하도록 유도한다.")]
    public float chaseTarget = 0.0005f;

    [FieldDescription("살아있는 적이 있음에도 이동·공격 없이 정지 상태일 때 부과되는 패널티. 소극적 대기 행동을 억제한다.")]
    public float disengaged = -0.01f;

    [FieldDescription("후퇴가 필요한 상황(ShouldRetreat)에서 스페이싱 이동을 취했을 때 부여되는 보상. 적절한 후퇴를 장려한다.")]
    public float goodRetreat = 0.001f;

    [FieldDescription("후퇴가 불필요한 상황에서 스페이싱 이동을 취했을 때 부과되는 패널티. 불필요한 도망을 억제한다.")]
    public float badRetreat = -0.02f;

    [FieldDescription("공격 가능한 사정거리 내 적이 있음에도 공격하지 않을 때 부과되는 패널티. 소극적 교전을 억제한다.")]
    public float inRangeNoAttack = -0.01f;

    [FieldDescription("후퇴해야 하는 상황에서 기본 공격을 시도할 때 부과되는 패널티. 무리한 공격을 억제한다.")]
    public float dangerousAttack = -0.03f;

    [FieldDescription("피해를 받을 때마다 부과되는 기본 패널티.")]
    public float damageTaken = -0.01f;

    [FieldDescription("받은 피해량 1포인트당 추가로 부과되는 패널티. 피해 규모에 비례한 추가 억제 효과.")]
    public float damageTakenPerPoint = -0.002f;

    [FieldDescription("사망 시 부과되는 패널티.")]
    public float death = -2f;

    [FieldDescription("적에게 피해를 입힐 때 부여되는 보상.")]
    public float damageDealt = 0.01f;

    [FieldDescription("공격이 적에게 명중했을 때 부여되는 보상.")]
    public float attackLanded = 0.05f;

    [FieldDescription("적을 처치했을 때 부여되는 보상.")]
    public float kill = 10f;

    [FieldDescription("플레이어블 영역 경계를 벗어났을 때 매 스텝 부과되는 패널티. 아레나 이탈을 억제한다.")]
    public float boundary = -0.2f;

    [FieldDescription("유효한 타겟 없이 공격 커맨드를 입력했을 때 부과되는 패널티. 헛된 행동을 억제한다.")]
    public float invalidAction = -1f;

    [FieldDescription("직전 스텝 대비 이동 입력 변화량에 비례하는 패널티. 움직임의 부드러움을 유도한다.")]
    public float actionDelta = -0.001f;

    [FieldDescription("직전 스텝 대비 회전 입력 변화량에 비례하는 패널티. 회전의 부드러움을 유도한다.")]
    public float turnDelta = -0.0005f;

    [FieldDescription("정지 상태에서 회전만 과도하게 할 때(idle jitter) 부과되는 패널티. 제자리 회전 반복을 억제한다.")]
    public float idleJitter = -0.001f;

    [FieldDescription("스킬을 사용할 수 없는 상황에서 스킬 커맨드를 입력했을 때 부과되는 패널티.")]
    public float invalidSkill = -0.02f;

    [FieldDescription("스킬이 정상적으로 발동되었을 때 부여되는 보상.")]
    public float skillActivated = 0.02f;

    [Header("MA-POCA 팀 리워드")]
    [FieldDescription("팀이 전투에서 승리했을 때 팀 전체에 부여되는 그룹 보상.")]
    public float groupWin = 20f;

    [FieldDescription("팀이 전투에서 패배했을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupLoss = -20f;

    [FieldDescription("제한 시간 내에 전투가 끝나지 않았을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupTimeout = -20f;

    [FieldDescription("전투가 외부 요인으로 중단되었을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupInterrupted = -20f;
}
