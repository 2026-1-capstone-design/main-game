/*
주석들만으로 한번에 파악하기 힘든 긴 플로우나 복잡한 플로우들의 경우 사전의 형태로 Scripts/Mainscripts/ImportantFlows 에 정리해놨습니다.
기본적으로 주석들만 가지고도 플로우를 따라가면 파악에는 문제 없을 것 같지만,
한눈에 해당 플로우의 진입점부터 최종 목적지까지 파악하면 편할 것 같아서 따로 적어놨습니다. 목차를 보고 그대로 아래에서 찾아보시면 됩니다
*/

using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SessionManager : SingletonBehaviour<SessionManager>
{
    private readonly Dictionary<string, int> _classNameCounters = new();        // 클래스별 이름 번호를 누적 관리함.

    public int CurrentDay { get; private set; } = 1;        // 현재 메인 루프의 날짜 (시장 재생성, 전투 후보 재생성, 보상 계산 등의 기준)
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
