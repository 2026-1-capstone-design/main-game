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
            unit.State.TickAttackLock(deltaTime);
            unit.State.TickSkillLock(deltaTime);
        }
    }
}
