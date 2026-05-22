using System.Collections.Generic;
using UnityEngine;

// 전투에 출전할 검투사 슬롯(최대 BattleTeamConstants.MaxUnitsPerTeam)을 관리하는 씬 레벨 매니저.
// SaveGameService와 MainFlowManager가 참조하며, 5개 팀 프리셋을 런타임 ID 배열로 직렬화한다.
[DisallowMultipleComponent]
public sealed class SquadManager : MonoBehaviour
{
    public const int SquadTeamCount = 5;

    private readonly OwnedGladiatorData[,] _teamSlots = new OwnedGladiatorData[
        SquadTeamCount,
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    private int _activeTeamIndex;

    public int SlotCount => BattleTeamConstants.MaxUnitsPerTeam;
    public int TeamCount => SquadTeamCount;
    public int ActiveTeamIndex => _activeTeamIndex;

    public bool SetActiveTeam(int teamIndex)
    {
        if (!IsValidTeamIndex(teamIndex))
        {
            return false;
        }

        _activeTeamIndex = teamIndex;
        return true;
    }

    public OwnedGladiatorData GetSlot(int slotIndex)
    {
        return GetSlot(_activeTeamIndex, slotIndex);
    }

    public OwnedGladiatorData GetSlot(int teamIndex, int slotIndex)
    {
        if (!IsValidTeamIndex(teamIndex) || !IsValidSlotIndex(slotIndex))
        {
            return null;
        }

        return _teamSlots[teamIndex, slotIndex];
    }

    public bool IsInSquad(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return false;
        }

        for (int team = 0; team < SquadTeamCount; team++)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_teamSlots[team, slot] == gladiator)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryAssignToSlot(int slotIndex, OwnedGladiatorData gladiator)
    {
        if (!IsValidSlotIndex(slotIndex) || gladiator == null)
        {
            return false;
        }

        // 같은 팀 안에서는 검투사 하나가 한 슬롯에만 들어가도록 기존 배치를 먼저 제거한다.
        for (int i = 0; i < SlotCount; i++)
        {
            if (_teamSlots[_activeTeamIndex, i] == gladiator)
            {
                _teamSlots[_activeTeamIndex, i] = null;
            }
        }

        _teamSlots[_activeTeamIndex, slotIndex] = gladiator;
        return true;
    }

    public void ClearSlot(int slotIndex)
    {
        if (IsValidSlotIndex(slotIndex))
        {
            _teamSlots[_activeTeamIndex, slotIndex] = null;
        }
    }

    // 검투사가 보유 목록에서 제거될 때(예: 시장 판매) 슬롯에서도 비운다.
    public void RemoveGladiatorFromSquad(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return;
        }

        for (int team = 0; team < SquadTeamCount; team++)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_teamSlots[team, slot] == gladiator)
                {
                    _teamSlots[team, slot] = null;
                }
            }
        }
    }

    // 전투 시작 시 현재 선택된 팀의 검투사 목록을 반환한다 (빈 슬롯 제외, 슬롯 순서 유지).
    public List<OwnedGladiatorData> GetAssignedGladiators()
    {
        return GetAssignedGladiators(_activeTeamIndex);
    }

    public List<OwnedGladiatorData> GetAssignedGladiators(int teamIndex)
    {
        var result = new List<OwnedGladiatorData>(SlotCount);
        if (!IsValidTeamIndex(teamIndex))
        {
            return result;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            OwnedGladiatorData gladiator = _teamSlots[teamIndex, i];
            if (gladiator != null)
            {
                result.Add(gladiator);
            }
        }

        return result;
    }

    public int[] GetSlotRuntimeIds()
    {
        int[] ids = new int[SquadTeamCount * SlotCount];
        for (int team = 0; team < SquadTeamCount; team++)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                OwnedGladiatorData gladiator = _teamSlots[team, slot];
                ids[GetFlattenedIndex(team, slot)] = gladiator != null ? gladiator.RuntimeId : -1;
            }
        }

        return ids;
    }

    // 저장 데이터에서 복원한다. GladiatorManager 복원이 완료된 후 호출해야 한다.
    public void RestoreFromSave(int[] runtimeIds, GladiatorManager gladiatorManager)
    {
        for (int team = 0; team < SquadTeamCount; team++)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                _teamSlots[team, slot] = null;
            }
        }

        if (runtimeIds == null || gladiatorManager == null)
        {
            return;
        }

        IReadOnlyList<OwnedGladiatorData> owned = gladiatorManager.OwnedGladiators;
        int len = Mathf.Min(runtimeIds.Length, SquadTeamCount * SlotCount);

        for (int i = 0; i < len; i++)
        {
            int targetId = runtimeIds[i];
            if (targetId < 0)
            {
                continue;
            }

            for (int j = 0; j < owned.Count; j++)
            {
                if (owned[j] != null && owned[j].RuntimeId == targetId)
                {
                    int teamIndex = i / SlotCount;
                    int slotIndex = i % SlotCount;
                    _teamSlots[teamIndex, slotIndex] = owned[j];
                    break;
                }
            }
        }
    }

    public void RestoreActiveTeamIndex(int activeTeamIndex)
    {
        SetActiveTeam(activeTeamIndex);
    }

    private bool IsValidTeamIndex(int teamIndex)
    {
        return teamIndex >= 0 && teamIndex < SquadTeamCount;
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < SlotCount;
    }

    private int GetFlattenedIndex(int teamIndex, int slotIndex)
    {
        return teamIndex * SlotCount + slotIndex;
    }
}
