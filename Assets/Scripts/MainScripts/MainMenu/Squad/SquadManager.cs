using System.Collections.Generic;
using UnityEngine;

// 전투에 출전할 검투사 슬롯(최대 BattleTeamConstants.MaxUnitsPerTeam)을 관리하는 씬 레벨 매니저.
// SaveGameService와 MainFlowManager가 참조하며, 저장 시 런타임 ID 배열로 직렬화된다.
[DisallowMultipleComponent]
public sealed class SquadManager : MonoBehaviour
{
    private readonly OwnedGladiatorData[] _slots = new OwnedGladiatorData[BattleTeamConstants.MaxUnitsPerTeam];

    public int SlotCount => _slots.Length;

    public OwnedGladiatorData GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length)
        {
            return null;
        }

        return _slots[slotIndex];
    }

    public bool IsInSquad(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return false;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == gladiator)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAssignToSlot(int slotIndex, OwnedGladiatorData gladiator)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length || gladiator == null)
        {
            return false;
        }

        // 같은 검투사가 다른 슬롯에 이미 있으면 먼저 제거
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == gladiator)
            {
                _slots[i] = null;
            }
        }

        _slots[slotIndex] = gladiator;
        return true;
    }

    public void ClearSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slots.Length)
        {
            _slots[slotIndex] = null;
        }
    }

    // 검투사가 보유 목록에서 제거될 때(예: 시장 판매) 슬롯에서도 비운다.
    public void RemoveGladiatorFromSquad(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return;
        }

        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == gladiator)
            {
                _slots[i] = null;
            }
        }
    }

    // 전투 시작 시 배치된 검투사 목록을 반환한다 (빈 슬롯 제외, 슬롯 순서 유지).
    public List<OwnedGladiatorData> GetAssignedGladiators()
    {
        var result = new List<OwnedGladiatorData>(_slots.Length);
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null)
            {
                result.Add(_slots[i]);
            }
        }

        return result;
    }

    public int[] GetSlotRuntimeIds()
    {
        int[] ids = new int[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            ids[i] = _slots[i] != null ? _slots[i].RuntimeId : -1;
        }

        return ids;
    }

    // 저장 데이터에서 복원한다. GladiatorManager 복원이 완료된 후 호출해야 한다.
    public void RestoreFromSave(int[] runtimeIds, GladiatorManager gladiatorManager)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i] = null;
        }

        if (runtimeIds == null || gladiatorManager == null)
        {
            return;
        }

        IReadOnlyList<OwnedGladiatorData> owned = gladiatorManager.OwnedGladiators;
        int len = Mathf.Min(runtimeIds.Length, _slots.Length);

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
                    _slots[i] = owned[j];
                    break;
                }
            }
        }
    }
}
