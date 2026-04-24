using System;
using System.Collections.Generic;

// 하나의 전투 팀에 속한 유닛 스냅샷 묶음과 팀 메타데이터를 담는다.
// 즉, "누가 이 팀에 속해 있나"를 표현하는 내용 모델이다.
// 같은 TeamId를 쓰더라도 슬롯 수나 전역 번호 시작점 같은 배치 정책은 BattleTeamLayout이 맡는다.
[Serializable]
public sealed class BattleTeamEntry
{
    private readonly List<BattleUnitSnapshot> _units = new List<BattleUnitSnapshot>();

    public BattleTeamId TeamId { get; }

    public bool IsPlayerOwned { get; }

    public IReadOnlyList<BattleUnitSnapshot> Units => _units;

    public BattleTeamEntry(BattleTeamId teamId, bool isPlayerOwned, IEnumerable<BattleUnitSnapshot> units)
    {
        TeamId = teamId;
        IsPlayerOwned = isPlayerOwned;

        if (units == null)
        {
            return;
        }

        foreach (BattleUnitSnapshot unit in units)
        {
            if (unit != null)
            {
                _units.Add(unit);
            }
        }
    }
}
