using System.Collections.Generic;
using UnityEngine;

public sealed class BattleVictorySystem
{
    public BattleOutcome? Evaluate(IReadOnlyList<BattleRuntimeUnit> units, int currentTick)
    {
        if (units == null)
            return null;

        bool hasLivingAlly = false;
        bool hasLivingEnemy = false;
        var survivors = new List<BattleRuntimeUnit>(units.Count);

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            survivors.Add(unit);

            if (unit.IsEnemy)
                hasLivingEnemy = true;
            else
                hasLivingAlly = true;

            if (hasLivingAlly && hasLivingEnemy)
                return null;
        }

        bool wasWin = hasLivingAlly && !hasLivingEnemy;
        int currentDay = SessionManager.Instance != null
            ? Mathf.Max(1, SessionManager.Instance.CurrentDay)
            : 1;
        int pendingReward = wasWin ? CalculateVictoryReward(currentDay) : 0;

        if (SessionManager.Instance != null)
            SessionManager.Instance.SetPendingBattleReward(pendingReward);

        BattleResolution resolution = BattleResolution.Create(wasWin, pendingReward, currentDay);
        BattleTeam winner = wasWin
            ? BattleTeam.Ally
            : (hasLivingEnemy ? BattleTeam.Enemy : BattleTeam.None);

        return new BattleOutcome(winner, currentTick, survivors, resolution);
    }

    private static int CalculateVictoryReward(int currentDay)
    {
        BalanceSO balance = ContentDatabaseProvider.Instance != null
            ? ContentDatabaseProvider.Instance.Balance
            : null;
        int rewardPerDay = balance != null
            ? Mathf.Max(0, balance.battleVictoryRewardPerDay)
            : 100;

        return Mathf.Max(0, currentDay) * rewardPerDay;
    }
}
