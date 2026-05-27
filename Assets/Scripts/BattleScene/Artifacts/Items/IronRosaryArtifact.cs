using UnityEngine;

// 철의 묵주: 본인이 시전하지 않은 모든 강제 이동 효과를 확률적으로 무시한다 (1, 2, 3레벨)
// 수치 설정: 넉백, 당기기 등 외부에서 발생하는 강제 이동 요청에 대해 레벨당 10%의 확률(1Lv: 10%, 2Lv: 20%, 3Lv: 30%)로 해당 효과를 완전히 무시하도록 설정했습니다.
public sealed class IronRosaryArtifact : IMovementModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.IronRosary;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyMoveSpeed(BattleUnitCombatState owner, ref BattleMoveRequest request)
    {
        // 일반 이동 속도에는 영향을 주지 않음
    }

    public bool CanIgnoreForcedMovement(BattleUnitCombatState owner, in BattleForcedMovementRequest request)
    {
        // 자신이 시전한 강제 이동(ex. 돌진기)은 무시 대상에서 제외
        if (request.Source != null && request.Source.State == owner)
            return false;

        float ignoreChance = _level * 0.1f;

        // Random.value는 0.0 ~ 1.0 사이의 값을 반환
        return Random.value <= ignoreChance;
    }
}
