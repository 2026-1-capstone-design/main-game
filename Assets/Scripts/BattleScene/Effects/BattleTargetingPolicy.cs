using UnityEngine;

// 타겟 판정이 어떤 전투 경로에서 발생했는지 구분한다.
// 같은 후보라도 기본 공격, 스킬, AI 계획, 강제 지정에 따라 장신구 보정 규칙이 달라질 수 있다.
public enum BattleTargetingReason
{
    BasicAttack,
    Skill,
    Planner,
    Forced,
}

// 타겟 우선순위 계산 중 장신구/상태가 수정할 수 있는 점수 컨텍스트다.
// Value는 호출자가 계산한 기본 점수에서 시작하며, 보정 훅들이 같은 값을 누적 수정한다.
public struct BattleTargetScore
{
    public BattleRuntimeUnit Requester;
    public BattleRuntimeUnit Candidate;
    public float Value;
    public BattleTargetingReason Reason;
}

// 전투 스냅샷과 전투 루프가 타겟 가능 여부와 점수 보정을 위임하는 정책이다.
// 구현체는 후보를 제외하거나 기본 점수를 가감하되, 실제 타겟 선택 루프는 호출자가 유지한다.
public interface IBattleTargetingPolicy
{
    bool CanTarget(BattleRuntimeUnit requester, BattleRuntimeUnit candidate, BattleTargetingReason reason);
    float ModifyTargetScore(
        BattleRuntimeUnit requester,
        BattleRuntimeUnit candidate,
        float baseScore,
        BattleTargetingReason reason
    );
}

// 장신구/상태 보정이 없을 때 사용하는 기본 정책이다.
// 기존 IsValidEnemyTarget 판정과 동일하게 동작해 정책 레이어 추가 전 결과를 보존한다.
public sealed class DefaultBattleTargetingPolicy : IBattleTargetingPolicy
{
    public static readonly DefaultBattleTargetingPolicy Instance = new DefaultBattleTargetingPolicy();

    private DefaultBattleTargetingPolicy() { }

    public bool CanTarget(BattleRuntimeUnit requester, BattleRuntimeUnit candidate, BattleTargetingReason reason) =>
        BattleFieldSnapshot.IsValidEnemyTarget(
            requester != null ? requester.State : null,
            candidate != null ? candidate.State : null
        );

    public float ModifyTargetScore(
        BattleRuntimeUnit requester,
        BattleRuntimeUnit candidate,
        float baseScore,
        BattleTargetingReason reason
    ) => baseScore;
}
