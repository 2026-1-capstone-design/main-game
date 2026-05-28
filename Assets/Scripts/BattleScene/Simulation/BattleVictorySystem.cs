using System.Collections.Generic;
using UnityEngine;

public sealed class BattleVictorySystem
{
    private readonly List<BattleRuntimeUnit> _survivorBuffer = new List<BattleRuntimeUnit>(
        BattleTeamConstants.MaxUnitsInBattle
    );
    private readonly HashSet<BattleTeamId> _livingTeams = new HashSet<BattleTeamId>();

    public BattleOutcome? Evaluate(
        IReadOnlyList<BattleRuntimeUnit> units,
        int currentTick,
        BattleTeamId playerTeamId,
        int previewRewardGold
    )
    {
        if (units == null)
            return null;

        _survivorBuffer.Clear();
        _livingTeams.Clear();

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            _survivorBuffer.Add(unit);
            _livingTeams.Add(unit.TeamId);
        }

        if (_livingTeams.Count > 1)
            return null;

        BattleTeamId? winnerTeamId = null;
        foreach (BattleTeamId teamId in _livingTeams)
        {
            winnerTeamId = teamId;
            break;
        }

        bool wasWin = winnerTeamId.HasValue && winnerTeamId.Value == playerTeamId;
        int currentDay = SessionManager.Instance != null ? Mathf.Max(1, SessionManager.Instance.CurrentDay) : 1;
        int pendingReward = wasWin ? CalculateVictoryReward(currentDay, previewRewardGold) : 0;

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.SetPendingBattleReward(pendingReward);
            SessionManager.Instance.RecordBattleResult(wasWin, CountDefeatedEnemies(units, playerTeamId));
        }

        BattleResolution resolution = BattleResolution.Create(wasWin, pendingReward, currentDay);
        BattleTeam winner = !winnerTeamId.HasValue ? BattleTeam.None : (wasWin ? BattleTeam.Ally : BattleTeam.Enemy);

        return new BattleOutcome(winner, winnerTeamId, currentTick, _survivorBuffer, resolution);
    }

    private static int CountDefeatedEnemies(IReadOnlyList<BattleRuntimeUnit> units, BattleTeamId playerTeamId)
    {
        if (units == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit != null && unit.TeamId != playerTeamId && unit.IsCombatDisabled)
            {
                count++;
            }
        }

        return count;
    }

    private static int CalculateVictoryReward(int currentDay, int previewRewardGold)
    {
        if (previewRewardGold > 0)
        {
            return previewRewardGold;
        }

        BalanceSO balance = ContentDatabaseProvider.Instance != null ? ContentDatabaseProvider.Instance.Balance : null;
        return RecruitFactory.CalculateRewardForDifficulty(balance, currentDay, BattleEncounterDifficulty.Medium);
    }
}
