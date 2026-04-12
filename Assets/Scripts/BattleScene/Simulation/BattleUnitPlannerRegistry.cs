using System.Collections.Generic;

// 유닛별 planner override를 관리한다.
// override가 없으면 기본 planner를 반환해 simulation 코어가 제어 방식 분기를 몰라도 되게 한다.
public sealed class BattleUnitPlannerRegistry
{
    private readonly Dictionary<BattleUnitCombatState, IBattleUnitPlanner> _overrides =
        new Dictionary<BattleUnitCombatState, IBattleUnitPlanner>();

    public IBattleUnitPlanner DefaultPlanner { get; set; }

    public void SetOverride(BattleUnitCombatState state, IBattleUnitPlanner planner)
    {
        if (state == null)
        {
            return;
        }

        if (planner == null)
        {
            _overrides.Remove(state);
            return;
        }

        _overrides[state] = planner;
    }

    public bool TryGet(BattleUnitCombatState state, out IBattleUnitPlanner planner)
    {
        if (state == null)
        {
            planner = null;
            return false;
        }

        if (_overrides.TryGetValue(state, out planner))
        {
            return true;
        }

        planner = DefaultPlanner;
        return planner != null;
    }

    public void Clear()
    {
        _overrides.Clear();
    }
}
