// 유닛별 전투 제어 계획 공급자 계약이다.
// 구현 출처가 legacy AI든 agent policy든 simulation system은 최종 BattleControlPlan만 소비한다.
public interface IBattleControlPlanProvider
{
    bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan);

    void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command);
}
