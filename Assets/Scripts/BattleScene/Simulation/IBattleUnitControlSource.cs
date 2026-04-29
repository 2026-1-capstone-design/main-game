public interface IBattleUnitControlSource
{
    bool TryBuildPlan(
        BattleUnitCombatState self,
        BattleFieldSnapshot snapshot,
        float tickDeltaTime,
        out BattleControlPlan plan
    );

    void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command);
}
