using UnityEngine;

// 일반 이동 한 번을 보정하기 위한 요청 DTO다.
// 이동 시스템은 최종 이동 거리 계산 직전에 이 값을 정책에 넘겨 속도/방향 보정을 받는다.
public struct BattleMoveRequest
{
    public BattleRuntimeUnit Mover;
    public Vector3 Direction;
    public BattleRuntimeUnit Target;
    public float Speed;
    public bool IsMovingTowardAlly;
    public bool IsMovingTowardEnemy;

    public static BattleMoveRequest ForMover(
        BattleRuntimeUnit mover,
        Vector3 direction,
        BattleRuntimeUnit target,
        float speed
    )
    {
        BattleTeamId moverTeam = mover != null ? mover.TeamId : default;
        BattleTeamId targetTeam = target != null ? target.TeamId : default;
        bool hasTarget = target != null;
        return new BattleMoveRequest
        {
            Mover = mover,
            Direction = direction,
            Target = target,
            Speed = Mathf.Max(0f, speed),
            IsMovingTowardAlly = hasTarget && moverTeam == targetTeam,
            IsMovingTowardEnemy = hasTarget && moverTeam != targetTeam,
        };
    }
}

// 넉백, 순간 이동, 끌어오기처럼 일반 이동이 아닌 위치 변경 요청이다.
// 강제 이동 무시 효과가 어떤 이동을 막을지 판단할 때 사용한다.
public struct BattleForcedMovementRequest
{
    public BattleRuntimeUnit Source;
    public BattleRuntimeUnit Target;
    public Vector3 Destination;
    public Vector3 Direction;
    public float Distance;
    public bool IsKnockback;
    public bool IsTeleport;
}

// 전투 이동을 보정하거나 강제 이동을 무시할 수 있는 정책이다.
// BattleArtifactSystem이 이 인터페이스를 구현해 장신구 훅을 물리 시스템에 연결한다.
public interface IBattleMovementPolicy
{
    void ModifyMoveSpeed(ref BattleMoveRequest request);
    bool CanIgnoreForcedMovement(BattleRuntimeUnit target, in BattleForcedMovementRequest request);
}

// 이동 보정 장신구가 없을 때 사용하는 무동작 정책이다.
// 속도는 변경하지 않고 모든 강제 이동을 허용한다.
public sealed class DefaultBattleMovementPolicy : IBattleMovementPolicy
{
    public static readonly DefaultBattleMovementPolicy Instance = new DefaultBattleMovementPolicy();

    private DefaultBattleMovementPolicy() { }

    public void ModifyMoveSpeed(ref BattleMoveRequest request) { }

    public bool CanIgnoreForcedMovement(BattleRuntimeUnit target, in BattleForcedMovementRequest request) => false;
}
