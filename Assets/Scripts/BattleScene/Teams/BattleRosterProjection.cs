using System;

// 런타임 유닛을 player/hostile 인덱스 관점으로 해석하는 읽기 모델 계약이다.
public interface IBattleRosterProjection
{
    int GetPlayerUnitCount();

    int GetHostileUnitCount();

    bool IsPlayerUnit(BattleRuntimeUnit unit);

    bool TryGetPlayerIndex(BattleRuntimeUnit unit, out int unitIndex);

    bool TryGetHostileIndex(BattleRuntimeUnit unit, out int unitIndex);

    string GetDisplayUnitId(BattleRuntimeUnit unit);
}

public sealed class BattleRosterProjection : IBattleRosterProjection
{
    private readonly BattleStartPayload _payload;
    private readonly BattleTeamId _hostileTeamId;

    public BattleRosterProjection(BattleStartPayload payload)
    {
        _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        _hostileTeamId = payload.GetHostileTeam().TeamId;
    }

    public int GetPlayerUnitCount() => _payload.GetPlayerTeam().Units.Count;

    public int GetHostileUnitCount() => _payload.GetHostileTeam().Units.Count;

    public bool IsPlayerUnit(BattleRuntimeUnit unit) => unit != null && unit.TeamId == _payload.PlayerTeamId;

    public bool TryGetPlayerIndex(BattleRuntimeUnit unit, out int unitIndex)
    {
        unitIndex = -1;

        if (!IsPlayerUnit(unit))
        {
            return false;
        }

        return _payload.TryGetTeamLocalUnitIndex(unit.TeamId, unit.UnitNumber, out unitIndex);
    }

    public bool TryGetHostileIndex(BattleRuntimeUnit unit, out int unitIndex)
    {
        unitIndex = -1;

        if (unit == null || unit.TeamId != _hostileTeamId)
        {
            return false;
        }

        return _payload.TryGetTeamLocalUnitIndex(unit.TeamId, unit.UnitNumber, out unitIndex);
    }

    public string GetDisplayUnitId(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "UNKNOWN";
        }

        if (TryGetPlayerIndex(unit, out int playerIndex))
        {
            return $"A_{playerIndex + 1:00}";
        }

        if (TryGetHostileIndex(unit, out int hostileIndex))
        {
            return $"E_{hostileIndex + 1:00}";
        }

        return $"U_{Math.Max(0, unit.UnitNumber):00}";
    }
}
