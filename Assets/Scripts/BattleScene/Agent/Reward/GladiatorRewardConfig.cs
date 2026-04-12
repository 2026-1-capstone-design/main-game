using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    [Header("Core Rewards")]
    [FieldDescription("매 스텝마다 부과되는 기본 패널티.")]
    public float step = -0.0005f;

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

    [FieldDescription("유효한 타겟 없이 공격 커맨드를 입력했을 때 부과되는 패널티.")]
    public float invalidAction = -1f;

    [Header("Action Stability")]
    [FieldDescription("직전 스텝 대비 이동 입력 변화량에 비례하는 패널티.")]
    public float actionDelta = -0.001f;

    [Header("Action Stability/Switch Penalty")]
    [FieldDescription("공격 command 선택이 바뀔 때 부과되는 패널티.")]
    public float commandSwitchPenalty = -0.02f;

    [FieldDescription("role 선택이 바뀔 때 부과되는 패널티.")]
    public float roleSwitchPenalty = -0.02f;

    [FieldDescription("fight mode 선택이 바뀔 때 부과되는 패널티.")]
    public float fightModeSwitchPenalty = -0.02f;

    [FieldDescription("anchor 선택이 바뀔 때 부과되는 패널티.")]
    public float anchorSwitchPenalty = -0.05f;

    [Header("Action Stability/Commitment")]
    [FieldDescription("같은 공격 command를 commitment window 이상 유지할 때 부여되는 보상.")]
    public float commandCommitmentReward = 0f;

    [FieldDescription("같은 role을 commitment window 이상 유지할 때 부여되는 보상.")]
    public float roleCommitmentReward = 0f;

    [FieldDescription("같은 fight mode를 commitment window 이상 유지할 때 부여되는 보상.")]
    public float fightModeCommitmentReward = 0f;

    [FieldDescription("같은 anchor를 commitment window 이상 유지할 때 부여되는 보상.")]
    public float anchorCommitmentReward = 0f;

    [Header("Fight Mode/Common")]
    [FieldDescription("사거리 비율 비교 시 0으로 나누는 것을 피하기 위한 최소 유효 사거리.")]
    public float minimumEffectiveRange = 0.001f;

    [Header("Fight Mode/Pressure")]
    [FieldDescription("Pressure에서 anchor에 접근할 때 거리 감소량에 곱해지는 보상.")]
    public float pressureApproachReward = 0.015f;

    [FieldDescription("Pressure + Attack이 내 사거리 안에서 유리한 위협 교환을 만들 때 부여되는 보상.")]
    public float pressureFavorableRangeReward = 0.04f;

    [FieldDescription("Pressure + Move가 사거리 밖에서 공격 가능한 거리로 접근할 때 거리 감소량에 곱해지는 보상.")]
    public float pressureMoveIntoRangeReward = 0.02f;

    [FieldDescription("Pressure에서 적 사거리 안이고 위협 교환이 불리할 때 받은 피해 비율에 곱해지는 추가 패널티.")]
    public float pressureUnsafeDamageTakenRatio = -0.5f;

    [Header("Fight Mode/Keep Range")]
    [FieldDescription("KeepRange가 보상받는 최소 거리 비율. target distance / own effective attack range 기준.")]
    public float keepRangeBandMin = 0.75f;

    [FieldDescription("KeepRange가 보상받는 최대 거리 비율. target distance / own effective attack range 기준.")]
    public float keepRangeBandMax = 1.05f;

    [FieldDescription("KeepRange에서 적정 거리 band 안에 있을 때 부여되는 보상.")]
    public float keepRangeBandReward = 0.04f;

    [FieldDescription("KeepRange + Move가 너무 가까운 상태에서 거리를 벌릴 때 거리 증가량에 곱해지는 보상.")]
    public float keepRangeRecoverReward = 0.02f;

    [FieldDescription("KeepRange에서 적정 거리보다 가까울 때 받은 피해 비율에 곱해지는 추가 패널티.")]
    public float keepRangeTooCloseDamageTakenRatio = -0.5f;

    [Header("Fight Mode/Retreat")]
    [FieldDescription("Retreat에서 target과 거리를 벌릴 때 거리 증가량에 곱해지는 보상.")]
    public float retreatSeparationReward = 0.015f;

    [FieldDescription("Retreat에서 적 유효 사거리 밖으로 이탈했을 때 부여되는 보상.")]
    public float retreatEscapeRangeReward = 0.03f;

    [Header("Fight Mode/Neutral")]
    [FieldDescription("Neutral 전용 shaping 예약 필드. 현재는 보상 계산에 사용하지 않는다.")]
    public float neutralReservedReward = 0f;

    [Header("Role Rewards")]
    public float engageApproachReward = 0.01f;
    public float engageReacquireReward = 0.01f;
    public float regroupSafetyReward = 0.02f;
    public float regroupCohesionReward = 0.02f;
    public float regroupOverstayPenalty = -0.02f;
    public int regroupWindowSteps = 8;

    [Header("MA-POCA Team Rewards")]
    [FieldDescription("팀이 전투에서 승리했을 때 팀 전체에 부여되는 그룹 보상.")]
    public float groupWin = 10f;

    [FieldDescription("팀이 전투에서 패배했을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupLoss = -10f;

    [FieldDescription("전투가 외부 요인으로 중단되었을 때 팀 전체에 부과되는 그룹 패널티.")]
    public float groupInterrupted = -10f;

    [Header("Victory Multipliers")]
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
