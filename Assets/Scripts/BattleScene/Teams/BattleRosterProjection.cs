using System;
using System.Collections.Generic;

// 런타임 유닛을 player/hostile 로스터 슬롯 관점으로 해석하는 읽기 모델 계약이다.
public interface IBattleRosterProjection
{
    int GetPlayerSlotCount();

    int GetHostileSlotCount();

    bool IsPlayerUnit(BattleRuntimeUnit unit);

    bool TryGetPlayerSlot(BattleRuntimeUnit unit, out int slotIndex);

    bool TryGetHostileSlot(BattleRuntimeUnit unit, out int slotIndex);

    string GetDisplayUnitId(BattleRuntimeUnit unit);

    IReadOnlyList<BattleRuntimeUnit> GetPlayerSlots();

    IReadOnlyList<BattleRuntimeUnit> GetHostileSlots();
}

public sealed class BattleRosterProjection : IBattleRosterProjection
{
    private readonly BattleStartPayload _payload;

    private readonly BattleRuntimeUnit[] _playerSlots;

    private readonly BattleRuntimeUnit[] _hostileSlots;

    private readonly Dictionary<BattleTeamId, int> _hostileSlotStartByTeam = new Dictionary<BattleTeamId, int>();

    public BattleRosterProjection(BattleStartPayload payload, IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        _playerSlots = new BattleRuntimeUnit[Math.Max(0, payload.RosterLayout.GetMaxUnitCount(payload.PlayerTeamId))];

        int hostileSlotCount = 0;
        for (int i = 0; i < payload.Teams.Count; i++)
        {
            BattleTeamEntry team = payload.Teams[i];
            if (team == null || team.TeamId == payload.PlayerTeamId)
            {
                continue;
            }

            _hostileSlotStartByTeam[team.TeamId] = hostileSlotCount;
            hostileSlotCount += Math.Max(0, payload.RosterLayout.GetMaxUnitCount(team.TeamId));
        }

        _hostileSlots = new BattleRuntimeUnit[hostileSlotCount];
        Bind(runtimeUnits);
    }

    public int GetPlayerSlotCount() => _playerSlots.Length;

    public int GetHostileSlotCount() => _hostileSlots.Length;

    public bool IsPlayerUnit(BattleRuntimeUnit unit) => unit != null && unit.TeamId == _payload.PlayerTeamId;

    public bool TryGetPlayerSlot(BattleRuntimeUnit unit, out int slotIndex)
    {
        slotIndex = -1;

        if (!IsPlayerUnit(unit))
        {
            return false;
        }

        return _payload.RosterLayout.TryGetSlot(unit.TeamId, unit.UnitNumber, out slotIndex);
    }

    public bool TryGetHostileSlot(BattleRuntimeUnit unit, out int slotIndex)
    {
        slotIndex = -1;

        if (unit == null || IsPlayerUnit(unit))
        {
            return false;
        }

        if (!_payload.RosterLayout.TryGetSlot(unit.TeamId, unit.UnitNumber, out int localSlotIndex))
        {
            return false;
        }

        if (!_hostileSlotStartByTeam.TryGetValue(unit.TeamId, out int hostileSlotStart))
        {
            return false;
        }

        slotIndex = hostileSlotStart + localSlotIndex;
        return true;
    }

    public string GetDisplayUnitId(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "UNKNOWN";
        }

        if (TryGetPlayerSlot(unit, out int playerSlotIndex))
        {
            return $"A_{playerSlotIndex + 1:00}";
        }

        if (TryGetHostileSlot(unit, out int hostileSlotIndex))
        {
            return $"E_{hostileSlotIndex + 1:00}";
        }

        return $"U_{Math.Max(0, unit.UnitNumber):00}";
    }

    public IReadOnlyList<BattleRuntimeUnit> GetPlayerSlots() => _playerSlots;

    public IReadOnlyList<BattleRuntimeUnit> GetHostileSlots() => _hostileSlots;

    // 런타임 유닛 목록을 순회하며 슬롯 배열에 실제 유닛 참조를 채운다.
    private void Bind(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        if (runtimeUnits == null)
        {
            return;
        }

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            if (TryGetPlayerSlot(unit, out int playerSlotIndex))
            {
                if (playerSlotIndex >= 0 && playerSlotIndex < _playerSlots.Length)
                {
                    _playerSlots[playerSlotIndex] = unit;
                }

                continue;
            }

            if (TryGetHostileSlot(unit, out int hostileSlotIndex))
            {
                if (hostileSlotIndex >= 0 && hostileSlotIndex < _hostileSlots.Length)
                {
                    _hostileSlots[hostileSlotIndex] = unit;
                }
            }
        }
    }
}
