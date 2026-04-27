using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SessionManager : SingletonBehaviour<SessionManager>
{
    private readonly Dictionary<string, int> _classNameCounters = new(); // 클래스별 이름 번호를 누적 관리함.

    public int CurrentDay { get; private set; } = 1; // 현재 메인 루프의 날짜 (시장 재생성, 전투 후보 재생성, 보상 계산 등의 기준)
    public bool HasUsedBattleToday { get; private set; }
    public int PendingBattleRewardAmount { get; private set; }
    public bool HasPendingBattleReward => PendingBattleRewardAmount > 0;

    public event Action<int> DayChanged;
    public event Action<bool> BattleUsageChanged;
    public event Action<int> PendingBattleRewardChanged;

    // 새 세션 시작 시 이벤트 호출을 통해 날짜, 전투 사용 여부, 펜딩 보상, 이름 카운터를 전부 초기 상태로 리셋한다.
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
        if (HasUsedBattleToday)
            return;

        HasUsedBattleToday = true;
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
    }

    public void ResetBattleUsageForNewDay()
    {
        HasUsedBattleToday = false;
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
    }

    // 하루를 넘기고 오늘의 전투 사용 상태를 초기화
    public void AdvanceDay()
    {
        CurrentDay++;
        ResetBattleUsageForNewDay();
        DayChanged?.Invoke(CurrentDay);
    }

    // 전투 보상을 즉시 골드에 넣지 않고
    // '나중에 메인씬에서 지급할 보상'으로 임시 저장함.
    public void SetPendingBattleReward(int rewardAmount)
    {
        PendingBattleRewardAmount = Mathf.Max(0, rewardAmount);
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    // 저장된 펜딩 전투 보상을 꺼냄.
    // MainScene 진입 직후 ResourceManager.GrantPendingBattleReward()에서 소비됨
    public int ConsumePendingBattleReward()
    {
        int amount = PendingBattleRewardAmount;
        PendingBattleRewardAmount = 0;
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
        return amount;
    }

    public void ClearPendingBattleReward()
    {
        if (PendingBattleRewardAmount == 0)
            return;

        PendingBattleRewardAmount = 0;
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    public void SetCurrentDayForLoad(int day)
    {
        CurrentDay = Mathf.Max(1, day);
        HasUsedBattleToday = false;

        DayChanged?.Invoke(CurrentDay);
        BattleUsageChanged?.Invoke(HasUsedBattleToday);
    }

    public void SetBattleStateForLoad(bool hasUsedBattleToday, int pendingBattleRewardAmount)
    {
        HasUsedBattleToday = hasUsedBattleToday;
        PendingBattleRewardAmount = Mathf.Max(0, pendingBattleRewardAmount);

        BattleUsageChanged?.Invoke(HasUsedBattleToday);
        PendingBattleRewardChanged?.Invoke(PendingBattleRewardAmount);
    }

    public SaveClassCounterEntry[] GetClassCounterEntriesForSave()
    {
        if (_classNameCounters.Count == 0)
        {
            return Array.Empty<SaveClassCounterEntry>();
        }

        SaveClassCounterEntry[] entries = new SaveClassCounterEntry[_classNameCounters.Count];
        int index = 0;

        foreach (KeyValuePair<string, int> kv in _classNameCounters)
        {
            entries[index] = new SaveClassCounterEntry { classPrefix = kv.Key, currentNumber = kv.Value };
            index++;
        }

        return entries;
    }

    public void SetClassCounterEntriesForLoad(SaveClassCounterEntry[] entries)
    {
        _classNameCounters.Clear();

        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            SaveClassCounterEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.classPrefix))
            {
                continue;
            }

            _classNameCounters[entry.classPrefix] = Mathf.Max(0, entry.currentNumber);
        }
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
