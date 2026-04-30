using System.Collections.Generic;

public sealed class BattlePlanningSystem
{
    public void Build(
        IReadOnlyList<BattleUnitCombatState> states,
        BattleFieldSnapshot snapshot,
        float tickDeltaTime,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (states == null || snapshot == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            BattleUnitCombatState state = states[i];
            if (state == null || state.IsCombatDisabled)
                continue;

            state.ClearCurrentPlan();

            if (rosterMutationSystem != null && rosterMutationSystem.IsCommandDisabled(state))
                continue;

            // CurrentPlan은 실행 대상 상태에 저장하지만, plan 생성 책임은 State가 소유한 ControlSource가 가진다.
            IBattleUnitControlSource source = state.ControlSource;
            if (source == null)
                continue;

            if (!source.TryBuildPlan(state, snapshot, tickDeltaTime, out BattleControlPlan plan))
                continue;

            state.SetCurrentPlan(plan);
        }
    }
}
