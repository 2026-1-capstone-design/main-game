using System.Collections.Generic;

public sealed class BattleCooldownSystem
{
    public void Tick(IReadOnlyList<BattleRuntimeUnit> units, float deltaTime)
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            unit.State.TickAttackCooldown(deltaTime);
            unit.State.TickSkillCooldown(deltaTime);
            unit.State.TickBufflCooldown(deltaTime);

            // 공격 lock 해제 시점은 tick 기반 추정값이 아니라 실제 Animator 상태를 기준으로 맞춘다.
            // 무기별 AnimatorOverrideController가 공격 clip 길이를 바꾸고, transition은 base AnimatorController에 남아 있어
            // clip 길이 + transition duration 계산만으로는 실제 시각적 공격 종료 시점과 어긋날 수 있다.
            // 특히 Training 환경에서는 simulation tick 진행과 Animator 평가 시간이 분리될 수 있으므로,
            // 공격 애니메이션 및 복귀 transition이 실제로 끝났을 때 CombatState의 공격 상태를 해제한다.
            if (unit.IsAttacking && !unit.IsAttackAnimationPlaying())
            {
                unit.State.SetAttackState(false);
            }
        }
    }
}
