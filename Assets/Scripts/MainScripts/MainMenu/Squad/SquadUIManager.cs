using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 팀 편성 패널을 담당하는 UI 매니저.
// 슬롯 클릭 시 보유 검투사 선택 그리드(pickerPanel)를 열고,
// 검투사를 선택하면 해당 슬롯에 배치하고 피커를 닫는다.
[DisallowMultipleComponent]
public sealed class SquadUIManager : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject panelRoot;

    [Header("Buttons")]
    [SerializeField]
    private Button backButton;

    [Header("Squad Slots")]
    [SerializeField]
    private SquadSlotCell[] slotCells;

    [Header("Gladiator Picker")]
    [SerializeField]
    private GameObject pickerPanelRoot;

    [SerializeField]
    private Button pickerCloseButton;

    // 현재 슬롯에 배치된 검투사를 제거하고 피커를 닫는 버튼
    [SerializeField]
    private Button pickerClearButton;

    [SerializeField]
    private OwnedItemGridViewer pickerGridViewer;

    [SerializeField]
    private TMP_Text pickerTitleText;

    private MainFlowManager _flow;
    private SquadManager _squadManager;
    private GladiatorManager _gladiatorManager;
    private bool _initialized;
    private int _pendingSlotIndex = -1;

    private readonly List<OwnedItemViewData> _pickerBuffer = new List<OwnedItemViewData>();

    public void Initialize(MainFlowManager flow, SquadManager squadManager, GladiatorManager gladiatorManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _squadManager = squadManager;
        _gladiatorManager = gladiatorManager;

        BindButton(backButton, OnBackClicked);
        BindButton(pickerCloseButton, OnPickerCloseClicked);
        BindButton(pickerClearButton, OnPickerClearClicked);

        if (slotCells != null)
        {
            for (int i = 0; i < slotCells.Length; i++)
            {
                if (slotCells[i] != null)
                {
                    slotCells[i].Setup(i, null, OnSlotClicked, OnSlotClearClicked);
                }
            }
        }

        SetPickerPanelActive(false);
        SetPanelActive(false);

        _initialized = true;
    }

    public void OpenPanel()
    {
        SetPickerPanelActive(false);
        SetPanelActive(true);
        RefreshSlots();
    }

    public void ClosePanel()
    {
        SetPickerPanelActive(false);
        SetPanelActive(false);
    }

    private void OnBackClicked()
    {
        _flow?.HandleSquadBackRequested();
    }

    private void OnSlotClicked(int slotIndex)
    {
        _pendingSlotIndex = slotIndex;
        OpenPicker(slotIndex);
    }

    private void OnSlotClearClicked(int slotIndex)
    {
        _squadManager?.ClearSlot(slotIndex);
        RefreshSlots();
    }

    private void OnPickerCloseClicked()
    {
        _pendingSlotIndex = -1;
        SetPickerPanelActive(false);
    }

    private void OnPickerClearClicked()
    {
        if (_pendingSlotIndex >= 0)
        {
            _squadManager?.ClearSlot(_pendingSlotIndex);
            RefreshSlots();
        }

        _pendingSlotIndex = -1;
        SetPickerPanelActive(false);
    }

    private void OpenPicker(int slotIndex)
    {
        if (pickerTitleText != null)
        {
            pickerTitleText.text = $"슬롯 {slotIndex + 1} 검투사 선택";
        }

        RefreshPickerGrid(slotIndex);
        SetPickerPanelActive(true);
    }

    private void RefreshPickerGrid(int slotIndex)
    {
        if (pickerGridViewer == null || _gladiatorManager == null || _squadManager == null)
        {
            return;
        }

        _pickerBuffer.Clear();
        IReadOnlyList<OwnedGladiatorData> owned = _gladiatorManager.OwnedGladiators;

        for (int i = 0; i < owned.Count; i++)
        {
            OwnedGladiatorData g = owned[i];
            if (g == null)
            {
                continue;
            }

            // 이 슬롯 이외의 다른 슬롯에 이미 배치된 검투사는 선택 목록에서 제외한다.
            bool assignedElsewhere = false;
            for (int s = 0; s < _squadManager.SlotCount; s++)
            {
                if (s == slotIndex)
                {
                    continue;
                }

                if (_squadManager.GetSlot(s) == g)
                {
                    assignedElsewhere = true;
                    break;
                }
            }

            if (assignedElsewhere)
            {
                continue;
            }

            Sprite portrait = g.GladiatorClass?.icon;
            _pickerBuffer.Add(new OwnedItemViewData(portrait, g.DisplayName, $"Lv.{g.Level}", string.Empty, g));
        }

        Canvas.ForceUpdateCanvases();
        pickerGridViewer.SetItems(_pickerBuffer, OnPickerCellClicked);
    }

    private void OnPickerCellClicked(OwnedItemViewData data)
    {
        if (_pendingSlotIndex < 0 || data.Source is not OwnedGladiatorData gladiator)
        {
            return;
        }

        _squadManager?.TryAssignToSlot(_pendingSlotIndex, gladiator);
        RefreshSlots();

        _pendingSlotIndex = -1;
        SetPickerPanelActive(false);
    }

    private void RefreshSlots()
    {
        if (slotCells == null || _squadManager == null)
        {
            return;
        }

        for (int i = 0; i < slotCells.Length; i++)
        {
            if (slotCells[i] == null)
            {
                continue;
            }

            slotCells[i].Refresh(_squadManager.GetSlot(i));
        }
    }

    private void SetPickerPanelActive(bool value)
    {
        if (pickerPanelRoot != null)
        {
            pickerPanelRoot.SetActive(value);
        }
    }

    private void SetPanelActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
