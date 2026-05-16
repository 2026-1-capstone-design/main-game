using UnityEngine;

// 행동 타입별 실행 플랜을 생성하는 인터페이스.
// 새 행동 타입 추가 시 이 인터페이스를 구현하는 클래스 하나만 추가하면 된다.
public interface IBattleActionPlanner
{
    BattleActionType ActionType { get; }

    // unit이 tick snapshot 상황에서 수행할 실행 플랜을 구성한다.
    BattleActionExecutionPlan Build(BattleRuntimeUnit unit, BattleFieldSnapshot snapshot);

    // 빌드된 플랜이 실제로 실행 가능한 상태인지 검사한다.
    bool IsUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan);
}

// Deprecated: legacy built-in AI planner 내부에서만 쓰는 실행 계획 DTO다.
public struct BattleActionExecutionPlan
{
    public BattleActionType Action;
    public BattleUnitCombatState TargetEnemy;
    public BattleUnitCombatState TargetAlly;
    public Vector3 DesiredPosition;
    public bool HasDesiredPosition;
}
