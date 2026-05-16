// 유닛별 전투 제어 컨트롤러 계약이다.
// 컨트롤러 우선순위와 활성 조건은 registry가 처리하며, 하위 시스템은 최종 BattleControlPlan만 소비한다.
public interface IBattleControlPlanner
{
    bool IsActive(BattleUnitCombatState self, in BattlePlanningContext context);

    bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan);
}
