using System;
using System.Collections.Generic;
using UnityEngine;

// 특정 팀이 로스터에서 차지하는 슬롯 구간을 표현한다.
// 즉, "이 팀을 로스터 슬롯/번호 체계에서 어떻게 배치하나"를 표현하는 정책 모델이다.
// 실제 팀 유닛 목록 자체는 BattleTeamEntry가 들고 있고, 이 타입은 슬롯 수와 번호 시작 위치만 다룬다.
[Serializable]
public sealed class BattleTeamLayout
{
    public BattleTeamId TeamId { get; }

    public int MaxUnitCount { get; }

    public int GlobalSlotStart { get; }

    // 팀별 슬롯 정보 하나를 생성한다.
    public BattleTeamLayout(BattleTeamId teamId, int maxUnitCount, int globalSlotStart)
    {
        TeamId = teamId;
        MaxUnitCount = Mathf.Max(1, maxUnitCount);
        GlobalSlotStart = Mathf.Max(1, globalSlotStart);
    }
}

// 팀별 슬롯 수와 전역 유닛 번호 정책을 캡슐화한다.
[Serializable]
public sealed class BattleRosterLayout
{
    // inspector/debug 용 순회 순서를 유지하는 팀 레이아웃 목록
    private readonly List<BattleTeamLayout> _teams = new List<BattleTeamLayout>();

    // 팀 ID로 빠르게 팀 레이아웃을 찾기 위한 인덱스
    private readonly Dictionary<BattleTeamId, BattleTeamLayout> _layoutByTeam =
        new Dictionary<BattleTeamId, BattleTeamLayout>();

    // 이 로스터가 관리하는 팀 레이아웃 목록
    public IReadOnlyList<BattleTeamLayout> Teams => _teams;

    // 주어진 팀 레이아웃 목록으로 로스터 레이아웃을 구성한다.
    public BattleRosterLayout(IEnumerable<BattleTeamLayout> teams)
    {
        if (teams == null)
        {
            throw new ArgumentNullException(nameof(teams));
        }

        foreach (BattleTeamLayout team in teams)
        {
            if (team == null)
            {
                continue;
            }

            _teams.Add(team);
            _layoutByTeam[team.TeamId] = team;
        }

        if (_teams.Count == 0)
        {
            throw new ArgumentException("BattleRosterLayout requires at least one team layout.", nameof(teams));
        }
    }

    // 팀 목록 순서대로 전역 슬롯 시작 번호를 배치하는 기본 레이아웃을 만든다.
    public static BattleRosterLayout CreateSequential(IReadOnlyList<BattleTeamEntry> teams)
    {
        if (teams == null || teams.Count == 0)
        {
            throw new ArgumentException("Battle roster layout requires at least one team.", nameof(teams));
        }

        List<BattleTeamLayout> layouts = new List<BattleTeamLayout>(teams.Count);
        int globalSlotStart = 1;

        for (int i = 0; i < teams.Count; i++)
        {
            BattleTeamEntry team = teams[i];
            if (team == null)
            {
                continue;
            }

            int maxUnitCount = team.Units != null ? Mathf.Max(1, team.Units.Count) : 1;
            layouts.Add(new BattleTeamLayout(team.TeamId, maxUnitCount, globalSlotStart));
            globalSlotStart += maxUnitCount;
        }

        return new BattleRosterLayout(layouts);
    }

    // 특정 팀의 슬롯 레이아웃을 조회한다.
    public bool TryGetTeamLayout(BattleTeamId teamId, out BattleTeamLayout layout) =>
        _layoutByTeam.TryGetValue(teamId, out layout);

    // 특정 팀이 가질 수 있는 최대 슬롯 수를 반환한다.
    public int GetMaxUnitCount(BattleTeamId teamId) =>
        TryGetTeamLayout(teamId, out BattleTeamLayout layout) ? layout.MaxUnitCount : 0;

    // 특정 팀의 전역 슬롯 시작 번호를 반환한다.
    public int GetGlobalSlotStart(BattleTeamId teamId) =>
        TryGetTeamLayout(teamId, out BattleTeamLayout layout) ? layout.GlobalSlotStart : -1;

    // 팀의 로컬 슬롯 인덱스를 전역 유닛 번호로 변환한다.
    public int AllocateUnitNumber(BattleTeamId teamId, int localSlotIndex)
    {
        if (!TryGetTeamLayout(teamId, out BattleTeamLayout layout))
        {
            throw new ArgumentException($"Unknown team id {teamId.Value}.", nameof(teamId));
        }

        int clampedSlotIndex = Mathf.Clamp(localSlotIndex, 0, layout.MaxUnitCount - 1);
        return layout.GlobalSlotStart + clampedSlotIndex;
    }

    // 전역 유닛 번호가 특정 팀의 몇 번째 로컬 슬롯인지 역으로 계산한다.
    public bool TryGetSlot(BattleTeamId teamId, int unitNumber, out int localSlotIndex)
    {
        localSlotIndex = -1;

        if (!TryGetTeamLayout(teamId, out BattleTeamLayout layout))
        {
            return false;
        }

        int slotIndex = unitNumber - layout.GlobalSlotStart;
        if (slotIndex < 0 || slotIndex >= layout.MaxUnitCount)
        {
            return false;
        }

        localSlotIndex = slotIndex;
        return true;
    }
}
