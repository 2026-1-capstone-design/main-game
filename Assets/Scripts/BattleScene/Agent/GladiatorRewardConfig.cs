using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    [FieldDescription("매 스텝마다 부과되는 기본 패널티.")]
    public float step = -0.0005f;

    [FieldDescription("타겟 전환 시 부과되는 패널티.")]
    public float targetSwitchPenalty = -0.01f;

    [FieldDescription("태세 전환 시 부과되는 패널티.")]
    public float stanceSwitchPenalty = -0.01f;

    [FieldDescription("입힌 피해를 대상 최대 체력 대비 비율로 환산해 부여하는 보상.")]
    public float damageDealtRatio = 1f;

    [FieldDescription("받은 피해를 자기 최대 체력 대비 비율로 환산해 부과하는 패널티.")]
    public float damageTakenRatio = -1f;

    [FieldDescription("공격이 적에게 명중했을 때 부여되는 보상.")]
    public float attackLanded = 0.05f;

    [FieldDescription("적을 처치했을 때 부여되는 보상.")]
    public float kill = 3f;

    [FieldDescription("사망 시 부과되는 패널티.")]
    public float death = -3f;

    [FieldDescription("플레이어블 영역 경계를 벗어났을 때 매 스텝 부과되는 패널티.")]
    public float boundary = -0.2f;

    [FieldDescription("유효한 타겟 없이 공격 커맨드를 입력했을 때 부과되는 패널티.")]
    public float invalidAction = -1f;

    [FieldDescription("직전 스텝 대비 이동 입력 변화량에 비례하는 패널티.")]
    public float actionDelta = -0.001f;

    [Header("Commitment")]
    public float anchorCommitmentReward = 0.005f;
    public float roleCommitmentReward = 0.005f;
    public float commitmentAbortPenalty = -0.02f;

    [Header("Role Rewards")]
    public float engageApproachReward = 0.01f;
    public float engageOpportunityReward = 0.01f;
    public float engageReacquireReward = 0.01f;
    public float peelFocusReward = 0.03f;
    public float peelSupportReward = 0.01f;
    public float assassinateApproachReward = 0.012f;
    public float assassinateIsolationReward = 0.015f;
    public float assassinateRetreatReward = 0.012f;
    public float assassinateFinishReward = 0.08f;
    public float regroupSafetyReward = 0.02f;
    public float regroupOverstayPenalty = -0.02f;
    public int regroupWindowSteps = 8;

    [Header("MA-POCA 팀 리워드")]
    [FieldDescription("팀이 전투에서 승리했을 때 팀 전체에 부여되는 그룹 보상.")]
    public float groupWin = 10f;

    [FieldDescription("팀이 전투에서 패배했을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupLoss = -10f;

    [FieldDescription("전투가 외부 요인으로 중단되었을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupInterrupted = -10f;

    [Header("승리 보상 배율")]
    [FieldDescription(
        "남은 경기 시간이 100%일 때 적용되는 최대 배율. 배율 = 1 + (winSpeedBonus - 1) * timeRemainingRatio."
    )]
    public float winSpeedBonus = 1.5f;

    [FieldDescription("승리 팀 HP가 100%일 때 적용되는 최대 배율. 배율 = 1 + (winHpBonus - 1) * hpRatio.")]
    public float winHpBonus = 1.5f;

    [FieldDescription("타임아웃 기본 보상 = groupLoss * timeoutMultiplier.")]
    public float timeoutMultiplier = 1.2f;

    [FieldDescription(
        "타임아웃 HP 배율의 최대값. 최종 HP 배율 = 1 + (timeoutHpRatioMultiplierMax - 1) * enemyHpRatio. "
            + "적군 HP 비율이 0%이면 1, 100%이면 timeoutHpRatioMultiplierMax."
    )]
    public float timeoutHpRatioMultiplierMax = 1.5f;
}
