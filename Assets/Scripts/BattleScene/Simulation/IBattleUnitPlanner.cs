// 유닛별 planner 계약이다.
// built-in AI와 ML policy 모두 최종 BattleControlPlan을 이 계층에서 만든다.
public interface IBattleUnitPlanner
{
    bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan);

    void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command);
}
