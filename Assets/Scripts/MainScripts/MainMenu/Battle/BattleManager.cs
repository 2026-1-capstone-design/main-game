/*
주석들만으로 한번에 파악하기 힘든 긴 플로우나 복잡한 플로우들의 경우 사전의 형태로 Scripts/Mainscripts/ImportantFlows 에 정리해놨습니다.
기본적으로 주석들만 가지고도 플로우를 따라가면 파악에는 문제 없을 것 같지만,
한눈에 해당 플로우의 진입점부터 최종 목적지까지 파악하면 편할 것 같아서 따로 적어놨습니다. 목차를 보고 그대로 아래에서 찾아보시면 됩니다
*/

using System.Collections.Generic;
using UnityEngine;


public struct BattleResolution
{
    public bool WasResolved { get; private set; }
    public bool WasWin { get; private set; }
    public int PendingReward { get; private set; }
    public int DayAtResolution { get; private set; }

    public static BattleResolution Create(bool wasWin, int pendingReward, int dayAtResolution)
    {
        BattleResolution result = new BattleResolution();
        result.WasResolved = true;
        result.WasWin = wasWin;
        result.PendingReward = pendingReward;
        result.DayAtResolution = dayAtResolution;
        return result;
    }
}



[DisallowMultipleComponent]
public sealed class BattleManager : MonoBehaviour
{
    [SerializeField] private bool verboseLog = true;

    // 오늘 날짜 기준으로 생성된 전투 후보 목록
    // 메인씬 전투 준비 패널이 이걸 그대로 씀
    private readonly List<BattleEncounterPreview> _dailyEncounters = new List<BattleEncounterPreview>();

    private SessionManager _sessionManager;
    private BalanceSO _balance;
    private RecruitFactory _recruitFactory;
    private bool _initialized;

    // 리팩토링 대상 : 전투 준비 패널이 열려 있는지 나타내는 !!방어용!! 상태값.
    private bool _battlePanelOpen;
    private bool _resultOpen;
    private BattleResolution _lastResolution;

    private int _encounterGeneratedDay = -1;
    private int _selectedEncounterIndex = -1;
    private int _cheatEncounterAverageLevelOverride = -1;
    public int CheatEncounterAverageLevelOverride => _cheatEncounterAverageLevelOverride;
    public bool HasCheatEncounterAverageLevelOverride => _cheatEncounterAverageLevelOverride > 0;

    public bool IsBattlePanelOpen => _battlePanelOpen;
    public bool IsResultOpen => _resultOpen;
    public BattleResolution LastResolution => _lastResolution;
    public IReadOnlyList<BattleEncounterPreview> DailyEncounters => _dailyEncounters;
    public int SelectedEncounterIndex => _selectedEncounterIndex;
    public int EncounterGeneratedDay => _encounterGeneratedDay;

    // 전투 후보 생성과 선택을 관리하기 위한 참조들을 연결,
    // 하루 캐시와 선택 상태를 초기화
    public void Initialize(SessionManager sessionManager, BalanceSO balance, RecruitFactory recruitFactory)
    {
        if (_initialized)
        {
            return;
        }

        _sessionManager = sessionManager;
        _balance = balance;
        _recruitFactory = recruitFactory;

        if (_sessionManager == null)
        {
            Debug.LogError("[BattleManager] sessionManager is null.", this);
            return;
        }

        if (_balance == null)
        {
            Debug.LogError("[BattleManager] balance is null.", this);
            return;
        }

        if (_recruitFactory == null)
        {
            Debug.LogError("[BattleManager] recruitFactory is null.", this);
            return;
        }

        _battlePanelOpen = false;
        _resultOpen = false;
        _lastResolution = default;
        _encounterGeneratedDay = -1;
        _selectedEncounterIndex = -1;
        _dailyEncounters.Clear();

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[BattleManager] Initialized.", this);
        }
    }

    // 해당 날짜의 전투 후보 목록을 생성해 캐시.
    // 같은 날짜에 이미 후보가 있으면 재사용하고, 날짜가 바뀌면 새로 만든다.
    public void InitializeDay(int currentDay)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleManager] InitializeDay called before Initialize.", this);
            return;
        }

        int safeDay = Mathf.Max(1, currentDay);

        if (_encounterGeneratedDay == safeDay && _dailyEncounters.Count > 0)
        {
            if (verboseLog)
            {
                Debug.Log($"[BattleManager] InitializeDay skipped. Daily encounters already cached for Day={safeDay}", this);
            }

            return;
        }

        _dailyEncounters.Clear();

        List<BattleEncounterPreview> encounters = _recruitFactory.CreateBattleEncounterPreviewsForDay(safeDay);
        if (encounters != null)
        {
            _dailyEncounters.AddRange(encounters);
        }

        _encounterGeneratedDay = safeDay;
        _selectedEncounterIndex = -1;
        _battlePanelOpen = false;
        _resultOpen = false;
        _lastResolution = default;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleManager] Daily battle encounters initialized. Day={safeDay}, EncounterCount={_dailyEncounters.Count}",
                this
            );
        }
    }

    // 오늘 전투 사용 여부와 상대 후보 준비 상태를 검사한 뒤,
    // 전투 준비 패널을 열 수 있는지 판단함.
    public bool TryOpenBattlePreparation(out string failReason)
    {
        failReason = null;

        if (!_initialized)
        {
            failReason = "BattleManager가 아직 초기화되지 않음.";
            return false;
        }

        if (_resultOpen)
        {
            failReason = "전투 결과창이 아직 열려 있음.";
            return false;
        }

        if (_battlePanelOpen)
        {
            failReason = "이미 전투 준비 패널이 열려 있음.";
            return false;
        }

        if (!_sessionManager.CanUseBattleToday())
        {
            failReason = "오늘 전투는 이미 사용됨.";
            return false;
        }

        if (_encounterGeneratedDay != _sessionManager.CurrentDay || _dailyEncounters.Count == 0)
        {
            failReason = "오늘 전투 후보가 아직 준비되지 않음.";
            return false;
        }

        _battlePanelOpen = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleManager] Battle preparation opened. Day={_sessionManager.CurrentDay}, SelectedEncounterIndex={_selectedEncounterIndex}",
                this
            );
        }

        return true;
    }

    public void ClosePreparation()
    {
        _battlePanelOpen = false;

        if (verboseLog)
        {
            Debug.Log("[BattleManager] Battle preparation closed.", this);
        }
    }

    public bool TrySelectEncounter(int encounterIndex)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleManager] TrySelectEncounter called before Initialize.", this);
            return false;
        }

        if (encounterIndex < 0 || encounterIndex >= _dailyEncounters.Count)
        {
            Debug.LogWarning($"[BattleManager] Invalid encounter index selected: {encounterIndex}", this);
            return false;
        }

        _selectedEncounterIndex = encounterIndex;

        if (verboseLog)
        {
            BattleEncounterPreview encounter = _dailyEncounters[encounterIndex];
            Debug.Log(
                $"[BattleManager] Encounter selected. Index={encounterIndex}, Difficulty={encounter.Difficulty}, AvgLv={encounter.AverageLevel:0.0}",
                this
            );
        }

        return true;
    }

    public bool HasSelectedEncounter()
    {
        return _selectedEncounterIndex >= 0 && _selectedEncounterIndex < _dailyEncounters.Count;
    }

    // 현재 선택된 전투 후보를 그대로 꺼낸다.
    // UI 표시나 일반 조회용 유틸
    public bool TryGetSelectedEncounter(out BattleEncounterPreview encounter)
    {
        if (HasSelectedEncounter())
        {
            encounter = _dailyEncounters[_selectedEncounterIndex];
            return true;
        }

        encounter = null;
        return false;
    }

    // legacy path
    public bool TryBeginBattle(out string failReason)
    {
        return TryOpenBattlePreparation(out failReason);
    }

    // legacy path
    public BattleResolution ResolveCurrentBattle()
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleManager] ResolveCurrentBattle called before Initialize.", this);
            return default;
        }

        if (!_battlePanelOpen)
        {
            Debug.LogError("[BattleManager] ResolveCurrentBattle called while battle panel is not open.", this);
            return default;
        }

        bool wasWin = Random.value < 0.5f;
        int pendingReward = 0;

        if (wasWin)
        {
            pendingReward = Mathf.Max(0, _sessionManager.CurrentDay * GetRewardPerDay());
        }

        _sessionManager.SetPendingBattleReward(pendingReward);

        _battlePanelOpen = false;
        _resultOpen = true;
        _lastResolution = BattleResolution.Create(wasWin, pendingReward, _sessionManager.CurrentDay);

        if (verboseLog)
        {
            Debug.Log($"[BattleManager] Battle resolved. WasWin={wasWin}, PendingReward={pendingReward}", this);
        }

        return _lastResolution;
    }

    // legacy path kept alive for easy reconnection later
    public void FinishResultFlow()
    {
        _battlePanelOpen = false;
        _resultOpen = false;
        _lastResolution = default;
        _sessionManager.ClearPendingBattleReward();

        if (verboseLog)
        {
            Debug.Log("[BattleManager] Result flow finished.", this);
        }
    }


    private int GetRewardPerDay()
    {
        return _balance != null ? Mathf.Max(0, _balance.battleVictoryRewardPerDay) : 100;
    }

    public void SetCheatEncounterAverageLevelOverride(int averageLevel)
    {
        _cheatEncounterAverageLevelOverride = Mathf.Max(1, averageLevel);

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleManager] Cheat encounter average level override set. " +
                $"OverrideDayAnchor={_cheatEncounterAverageLevelOverride}",
                this
            );
        }
    }

    public void ClearCheatEncounterAverageLevelOverride()
    {
        _cheatEncounterAverageLevelOverride = -1;

        if (verboseLog)
        {
            Debug.Log("[BattleManager] Cheat encounter average level override cleared.", this);
        }
    }

    // 실제 전투 시작 직전에 사용할 전투 후보를 반환
    // 치트코드의 override가 켜져 있으면 현재 캐시를 그대로 쓰지 않고,
    // override 기준으로 적 팀을 다시 생성해서 반환
    public bool TryGetSelectedEncounterForBattle(out BattleEncounterPreview encounter)
    {
        encounter = null;

        if (!HasSelectedEncounter())
        {
            return false;
        }

        if (!HasCheatEncounterAverageLevelOverride)
        {
            encounter = _dailyEncounters[_selectedEncounterIndex];
            return encounter != null;
        }

        List<BattleEncounterPreview> overriddenEncounters =
            _recruitFactory.CreateBattleEncounterPreviewsForDay(_cheatEncounterAverageLevelOverride);

        if (overriddenEncounters == null || overriddenEncounters.Count == 0)
        {
            Debug.LogWarning("[BattleManager] Cheat override encounter rebuild failed. Falling back to cached encounter.", this);
            encounter = _dailyEncounters[_selectedEncounterIndex];
            return encounter != null;
        }

        if (_selectedEncounterIndex < 0 || _selectedEncounterIndex >= overriddenEncounters.Count)
        {
            Debug.LogWarning("[BattleManager] Selected encounter index is invalid for overridden encounter list.", this);
            encounter = _dailyEncounters[_selectedEncounterIndex];
            return encounter != null;
        }

        encounter = overriddenEncounters[_selectedEncounterIndex];

        if (verboseLog && encounter != null)
        {
            Debug.Log(
                $"[BattleManager] Using overridden encounter for battle. " +
                $"SelectedIndex={_selectedEncounterIndex}, OverrideDayAnchor={_cheatEncounterAverageLevelOverride}, " +
                $"AvgLv={encounter.AverageLevel:0.0}",
                this
            );
        }

        return encounter != null;
    }

    // 치트코드 인스펙터 상 기준값으로 오늘의 전투 후보 목록을 다시 만든다.
    // 현재 날짜는 유지하고, 후보 내용만 새로 갈아끼움
    public bool RegenerateDailyEncountersForCheat()
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleManager] RegenerateDailyEncountersForCheat called before Initialize.", this);
            return false;
        }

        int currentDay = _sessionManager != null ? Mathf.Max(1, _sessionManager.CurrentDay) : 1;
        int generationAnchor = HasCheatEncounterAverageLevelOverride
            ? Mathf.Max(1, _cheatEncounterAverageLevelOverride)
            : currentDay;

        _dailyEncounters.Clear();

        List<BattleEncounterPreview> encounters =
            _recruitFactory.CreateBattleEncounterPreviewsForDay(generationAnchor);

        if (encounters != null)
        {
            _dailyEncounters.AddRange(encounters);
        }

        // 생성 기준(anchor)만 치트값으로 바꾼다.
        _encounterGeneratedDay = currentDay;

        //  선택 해제
        _selectedEncounterIndex = -1;

        // 패널이 열려 있더라도 결과창 상태는 정리
        _resultOpen = false;
        _lastResolution = default;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleManager] Cheat encounter regeneration complete. " +
                $"CurrentDay={currentDay}, GenerationAnchor={generationAnchor}, " +
                $"EncounterCount={_dailyEncounters.Count}, SelectionCleared=true",
                this
            );
        }

        return true;
    }
}
