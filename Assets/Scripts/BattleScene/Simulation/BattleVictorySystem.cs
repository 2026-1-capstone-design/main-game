using System.Collections.Generic;
using UnityEngine;

public sealed class BattleVictorySystem
{
    public BattleOutcome? Evaluate(IReadOnlyList<BattleRuntimeUnit> units, int currentTick, BattleTeamId playerTeamId)
    {
        if (units == null)
            return null;

        var survivors = new List<BattleRuntimeUnit>(units.Count);
        HashSet<BattleTeamId> livingTeams = new HashSet<BattleTeamId>();

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            survivors.Add(unit);
            livingTeams.Add(unit.TeamId);
        }

        if (livingTeams.Count > 1)
            return null;

        BattleTeamId? winnerTeamId = null;
        foreach (BattleTeamId teamId in livingTeams)
        {
            winnerTeamId = teamId;
            break;
        }

        bool wasWin = winnerTeamId.HasValue && winnerTeamId.Value == playerTeamId;
        int currentDay = SessionManager.Instance != null ? Mathf.Max(1, SessionManager.Instance.CurrentDay) : 1;
        int pendingReward = wasWin ? CalculateVictoryReward(currentDay) : 0;

        if (SessionManager.Instance != null)
            SessionManager.Instance.SetPendingBattleReward(pendingReward);

        BattleResolution resolution = BattleResolution.Create(wasWin, pendingReward, currentDay);
        BattleTeam winner = !winnerTeamId.HasValue ? BattleTeam.None : (wasWin ? BattleTeam.Ally : BattleTeam.Enemy);

        return new BattleOutcome(winner, winnerTeamId, currentTick, survivors, resolution);
    }

    private static int CalculateVictoryReward(int currentDay)
    {
        BalanceSO balance = ContentDatabaseProvider.Instance != null ? ContentDatabaseProvider.Instance.Balance : null;
        int rewardPerDay = balance != null ? Mathf.Max(0, balance.battleVictoryRewardPerDay) : 100;

        return Mathf.Max(0, currentDay) * rewardPerDay;
    }
}
