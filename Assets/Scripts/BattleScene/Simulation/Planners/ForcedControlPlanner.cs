using System.Collections.Generic;

// ForcedControlPlanBuffer는 도발/공포/정신지배처럼 다른 입력보다 우선하는 강제 행동 계획을 보관한다.
// 현재 스킬 효과 이관 전 단계이므로 외부 시스템이 명시적으로 plan을 넣은 경우에만 활성화된다.
public sealed class ForcedControlPlanBuffer
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

// ForcedControlPlanner는 강제 행동 효과를 최상위 controller priority로 연결하는 스캐폴딩이다.
public sealed class ForcedControlPlanner : IBattleControlPlanner
{
    private readonly ForcedControlPlanBuffer _buffer;

    public ForcedControlPlanner(ForcedControlPlanBuffer buffer)
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
