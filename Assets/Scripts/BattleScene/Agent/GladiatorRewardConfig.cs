using UnityEngine;

// Reward 철학 (Stage 1 BC + RL fine-tune):
//   1. 효율(kill/win/damage)을 1차 목표로 두지 않는다. 그것만 추구하면 부자연스러운 살인병기가 된다.
//   2. 자연스러움을 reward에 명시한다: 이동 일관성, 무기 부합 행동, 적 정면 응시, 분산.
//   3. BC pretrain이 룰베이스 자연스러움을 정책에 새기고, RL은 그 위에 미세 적응만.
//   4. 모든 step penalty/bonus의 자릿수는 attackLanded(+0.02)의 1/10 ~ 1/4로 통제.
[CreateAssetMenu(menuName = "Battle/Training/Gladiator Reward Config")]
public sealed class GladiatorRewardConfig : ScriptableObject
{
    // ─── 시간 / 거리 / 후퇴 ───────────────────────────────────
    public float step = -0.0005f; // 시간 페널티 약화 (-0.001 → -0.0005)
    public float approach = 0.0003f; // 약화 (전투 강요 줄임)
    public float retreatDistance = 0.0005f;
    public float chaseTarget = 0.0003f; // 사거리 밖 적 추격 의도에 대한 약한 보상 (자동 chase 제거됨)
    public float disengaged = -0.005f; // 약화: 가만히 있어도 그렇게 큰 페널티는 안 줌
    public float goodRetreat = 0.001f;
    public float badRetreat = -0.01f;

    // ─── 공격 / 스킬 행동 ─────────────────────────────────────
    public float inRangeNoAttack = -0.02f; // 강화: 사거리 안에서 공격 안 하면 큰 페널티 (근접 무기 학습 강화)
    public float dangerousAttack = -0.02f;
    public float invalidAction = -0.5f; // 약화 (-1 → -0.5)

    // ─── 피격 / 사망 ─────────────────────────────────────────
    public float damageTaken = -0.005f; // 효율 약화
    public float damageTakenPerPoint = -0.001f;
    public float death = -1f; // 약화 (-2 → -1)

    // ─── 가해 / 처치 ─────────────────────────────────────────
    public float damageDealt = 0.005f; // 약화
    public float attackLanded = 0.02f; // 약화 (0.05 → 0.02)
    public float kill = 2f; // 대폭 약화 (10 → 2)

    // ─── 종료 / 경계 ─────────────────────────────────────────
    public float win = 5f; // 약화 (20 → 5)
    public float loss = -5f;
    public float timeout = -5f;
    public float boundary = -0.1f; // 약화

    // ─── 회전 (자연스러움 우선) ─────────────────────────────
    // 회전 자체는 페널티 0. 적을 정면에 두는 게 자연스러움이라 회전을 막으면 안 됨.
    public float rotateActionCost = 0f;
    public float rotateWhileMoving = 0f;

    // ─── 신규: 자연스러움 reward (BC + RL 핵심) ─────────────
    // 이동 일관성: 직전 step의 move와 같은 방향이면 +. 매번 방향 바꾸면 지그재그.
    public float movementConsistency = 0.0008f;
    public float directionFlipPenalty = -0.001f; // 정반대 방향 전환 시 -

    // 무기 부합 행동:
    //   원거리(AttackRange ≥ 3) + KeepRange/Backward 사용 시 +
    //   근거리(AttackRange < 3) + Forward 사용 시 +
    public float weaponMatchedAction = 0.003f;
    public float weaponMismatchedAction = -0.001f; // 원거리 + Forward 또는 근거리 + Backward 시 약한 -

    // 분산 강화: 가까운 아군 1.5 unit 안 시 페널티 (시각 명료성).
    public float teammateProximityPenalty = -0.0015f;
    public float teammateProximityRadius = 1.5f;

    // 본대 중심 고립: 너무 멀어지면 페널티 (지나친 분산 방지).
    public float isolationFromTeamPenalty = -0.0008f;
    public float isolationRadius = 8f;

    // 적 정면 응시:
    //   사거리 안 적이 정면 ±60° 밖에 있는데 IntentAttack 시 -. (회전 학습 동기 부여)
    public float notFacingTargetPenalty = -0.005f;
    public float facingConeDegrees = 60f;

    // 적 방향 이동 보너스:
    //   사거리 밖 적이 있고 IntentMove forward 방향이 적 방향과 일치 시 +. 자동 chase 대체.
    public float moveTowardTargetBonus = 0.001f;

    // ─── 봉인 의도 위반 안전망 ────────────────────────────
    public float forbiddenIntent = -0.3f; // 약화
}
