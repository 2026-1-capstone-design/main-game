using System.Collections.Generic;

public sealed class BattleCooldownSystem
{
    public void Tick(
        IReadOnlyList<BattleRuntimeUnit> units,
        float deltaTime,
        IBattleEffectSink effects,
        bool includeSkillCooldown = true
    )
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            unit.State.TickAttackCooldown(deltaTime);
            if (includeSkillCooldown)
                unit.State.TickSkillCooldown(deltaTime);
            unit.State.TickBufflCooldown(deltaTime, effects);

            if (!unit.IsAttacking)
                continue;

            if (unit.ShouldUseAnimatorAttackRelease)
            {
                // DefaultDur 무기는 실제 Animator 상태를 기준으로 공격 lock 해제 시점을 맞춘다.
                if (!unit.IsAttackAnimationPlaying())
                    unit.State.SetAttackState(false);
                continue;
            }

            // 커스텀 duration 무기는 Training 환경에서도 Animator 평가에 의존하지 않도록 전투 tick으로 해제한다.
            unit.State.TickAttackingLock(deltaTime);
        }
    }
}
