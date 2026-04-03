using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SessionManager : SingletonBehaviour<SessionManager>
{
    private readonly Dictionary<string, int> _classNameCounters = new();

    public int CurrentDay { get; private set; } = 1;
    public bool HasUsedBattleToday { get; private set; }
    public int PendingBattleRewardAmount { get; private set; }
    public bool HasPendingBattleReward => PendingBattleRewardAmount > 0;

    public event Action<int> DayChanged;
    public event Action<bool> BattleUsageChanged;
    public event Action<int> PendingBattleRewardChanged;

    public void StartNewSession()
    {
        CurrentDay = 1;
        HasUsedBattleToday = false;
        PendingBattleRewardAmount = 0;
        _classNameCounters.Clear();

        DayChanged?.Invoke(CurrentDay);
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    public bool CanUseBattleToday()
    {
        return !HasUsedBattleToday;
    }

    public void MarkBattleUsed()
    {
        if (HasUsedBattleToday) return;

        HasUsedBattleToday = true;
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
    }

    public void ResetBattleUsageForNewDay()
    {
        HasUsedBattleToday = false;
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
    }

    public void AdvanceDay()
    {
        CurrentDay++;
        ResetBattleUsageForNewDay();
        DayChanged?.Invoke(CurrentDay);
    }

    public void SetPendingBattleReward(int rewardAmount)
    {
        PendingBattleRewardAmount = Mathf.Max(0, rewardAmount);
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    public int ConsumePendingBattleReward()
    {
        int amount = PendingBattleRewardAmount;
        PendingBattleRewardAmount = 0;
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
        return amount;
    }

    public void ClearPendingBattleReward()
    {
        if (PendingBattleRewardAmount == 0) return;

        PendingBattleRewardAmount = 0;
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    public int ConsumeNextClassNumber(string classPrefix)
    {
        if (string.IsNullOrWhiteSpace(classPrefix))
        {
            Debug.LogError("[SessionManager] classPrefix is null or empty.");
            return 1;
        }

        if (_classNameCounters.TryGetValue(classPrefix, out int current))
        {
            current++;
            _classNameCounters[classPrefix] = current;
            return current;
        }

        _classNameCounters[classPrefix] = 1;
        return 1;
    }

    public string ConsumeNextClassName(string classPrefix)
    {
        int nextNumber = ConsumeNextClassNumber(classPrefix);
        return $"{classPrefix}{nextNumber:00}";
    }

    public int PeekCurrentClassNumber(string classPrefix)
    {
        if (string.IsNullOrWhiteSpace(classPrefix))
        {
            return 0;
        }

        return _classNameCounters.TryGetValue(classPrefix, out int current) ? current : 0;
    }
}