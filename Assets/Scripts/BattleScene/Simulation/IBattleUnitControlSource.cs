public interface IBattleUnitControlSource
{
    // 제어 주체가 이번 틱에 실행할 plan을 만든다.
    // BuiltIn AI는 전술 점수와 planner로 plan을 만들고, ML-Agent는 action buffer 입력을 plan으로 번역한다.
    bool TryBuildPlan(
        BattleUnitCombatState self,
        BattleFieldSnapshot snapshot,
        float tickDeltaTime,
        out BattleControlPlan plan
    );

    // combat phase에서 공격/스킬 command가 성공 또는 실패로 처리된 뒤 호출된다.
    // 특히 ML-Agent 입력은 버튼성 command를 한 번 소비해야 다음 틱에 같은 명령이 반복 실행되지 않는다.
    void ConsumeCommand(BattleUnitCombatState self, BattleCombatCommand command);
}
