using System.Collections.Generic;

// PlayerCommandControlBuffer는 플레이어 명령을 controller stack에 올리기 위한 임시 plan 저장소다.
// 명령 완료 조건과 UI/LLM 입력 연결은 후속 작업에서 이 버퍼 위에 추가한다.
public sealed class PlayerCommandControlBuffer
{
    private readonly Dictionary<BattleUnitCombatState, BattleControlPlan> _plans =
        new Dictionary<BattleUnitCombatState, BattleControlPlan>();

    public void SetPlan(BattleUnitCombatState state, BattleControlPlan plan)
    {
        if (state == null)
            return;

        _plans[state] = plan;
    }

    public bool TryGetPlan(BattleUnitCombatState state, out BattleControlPlan plan)
    {
        if (state != null && _plans.TryGetValue(state, out plan))
            return true;

        plan = default;
        return false;
    }

    public void Clear(BattleUnitCombatState state)
    {
        if (state != null)
            _plans.Remove(state);
    }

    public void ClearAll()
    {
        _plans.Clear();
    }
}

// PlayerCommandControlPlanner는 플레이어 명령을 ML/BuiltInAI보다 높은 우선순위로 실행하는 스캐폴딩이다.
public sealed class PlayerCommandControlPlanner : IBattleControlPlanner
{
    private readonly PlayerCommandControlBuffer _buffer;

    public PlayerCommandControlPlanner(PlayerCommandControlBuffer buffer)
    {
        _buffer = buffer;
    }

    public bool IsActive(BattleUnitCombatState self, in BattlePlanningContext context) =>
        _buffer != null && _buffer.TryGetPlan(self, out _);

    public bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan)
    {
        if (_buffer != null && _buffer.TryGetPlan(self, out plan))
            return true;

        plan = default;
        return false;
    }
}
