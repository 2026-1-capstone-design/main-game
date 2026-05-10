using System.Collections.Generic;
using System.Text;
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

    [Header("Squad Team Tabs")]
    [SerializeField]
    private Button[] teamTabButtons = new Button[SquadManager.SquadTeamCount];

    [SerializeField]
    private TMP_Text squadTitleText;

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

    [Header("Gladiator Detail")]
    [SerializeField]
    private GameObject detailPanelRoot;

    [SerializeField]
    private Image detailPortraitImage;

    [SerializeField]
    private Image detailWeaponIcon;

    [SerializeField]
    private Image[] detailArtifactIcons = new Image[3];

    [SerializeField]
    private TMP_Text detailText;

    [SerializeField]
    private Button assignButton;

    private MainFlowManager _flow;
    private SquadManager _squadManager;
    private GladiatorManager _gladiatorManager;
    private bool _initialized;
    private int _pendingSlotIndex = -1;
    private OwnedGladiatorData _pendingGladiator;

    private readonly List<OwnedItemViewData> _pickerBuffer = new List<OwnedItemViewData>();
    private readonly StringBuilder _detailBuilder = new StringBuilder(256);

    public void Initialize(MainFlowManager flow, SquadManager squadManager, GladiatorManager gladiatorManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _squadManager = squadManager;
        _gladiatorManager = gladiatorManager;

        ResolveMissingReferences();

        BindButton(backButton, OnBackClicked);
        BindButton(pickerCloseButton, OnPickerCloseClicked);
        BindButton(pickerClearButton, OnPickerClearClicked);
        BindButton(assignButton, OnAssignClicked);
        BindTeamTabButtons();

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
        RefreshTeamTabs();
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

    private void OnTeamTabClicked(int teamIndex)
    {
        if (_squadManager == null || !_squadManager.SetActiveTeam(teamIndex))
        {
            return;
        }

        _pendingSlotIndex = -1;
        _pendingGladiator = null;
        SetPickerPanelActive(false);
        SetDetailPanelActive(false);
        RefreshTeamTabs();
        RefreshSlots();
    }

    private void OnPickerCloseClicked()
    {
        _pendingSlotIndex = -1;
        _pendingGladiator = null;
        SetPickerPanelActive(false);
        SetDetailPanelActive(false);
    }

    private void OnPickerClearClicked()
    {
        if (_pendingSlotIndex >= 0)
        {
            _squadManager?.ClearSlot(_pendingSlotIndex);
            RefreshSlots();
        }

        _pendingSlotIndex = -1;
        _pendingGladiator = null;
        SetPickerPanelActive(false);
        SetDetailPanelActive(false);
    }

    private void OnAssignClicked()
    {
        if (_pendingSlotIndex < 0 || _pendingGladiator == null)
        {
            return;
        }

        _squadManager?.TryAssignToSlot(_pendingSlotIndex, _pendingGladiator);
        RefreshSlots();

        _pendingSlotIndex = -1;
        _pendingGladiator = null;
        SetDetailPanelActive(false);
        SetPickerPanelActive(false);
    }

    private void OpenPicker(int slotIndex)
    {
        if (pickerTitleText != null)
        {
            pickerTitleText.text = $"슬롯 {slotIndex + 1} 검투사 선택";
        }

        _pendingGladiator = null;
        SetDetailPanelActive(false);
        RefreshPickerGrid(slotIndex);
        SetPickerPanelActive(true);

        if (pickerPanelRoot == null)
        {
            Debug.LogWarning("[SquadUIManager] pickerPanelRoot is not assigned.", this);
        }
    }

    private void RefreshPickerGrid(int slotIndex)
    {
        if (pickerGridViewer == null || _gladiatorManager == null || _squadManager == null)
        {
            Debug.LogWarning(
                "[SquadUIManager] Cannot refresh picker grid because a required reference is missing.",
                this
            );
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

        _pendingGladiator = gladiator;
        ShowGladiatorDetail(gladiator);
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

    private void RefreshTeamTabs()
    {
        if (_squadManager == null)
        {
            return;
        }

        int activeTeamIndex = _squadManager.ActiveTeamIndex;

        if (squadTitleText != null)
        {
            squadTitleText.text = activeTeamIndex == 0 ? "메인 스쿼드" : $"스쿼드 {activeTeamIndex + 1}";
        }

        if (teamTabButtons == null)
        {
            return;
        }

        for (int i = 0; i < teamTabButtons.Length; i++)
        {
            if (teamTabButtons[i] == null)
            {
                continue;
            }

            teamTabButtons[i].interactable = i != activeTeamIndex;
        }
    }

    private void SetPickerPanelActive(bool value)
    {
        if (pickerPanelRoot != null)
        {
            pickerPanelRoot.SetActive(value);
        }
    }

    private void ShowGladiatorDetail(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            SetDetailPanelActive(false);
            return;
        }

        if (detailPortraitImage != null)
        {
            Sprite portrait = gladiator.GladiatorClass?.icon;
            detailPortraitImage.sprite = portrait;
            detailPortraitImage.enabled = portrait != null;
            detailPortraitImage.preserveAspect = true;
        }

        if (detailWeaponIcon != null)
        {
            Sprite weaponIcon = gladiator.EquippedWeapon?.Weapon?.icon;
            detailWeaponIcon.sprite = weaponIcon;
            detailWeaponIcon.enabled = weaponIcon != null;
            detailWeaponIcon.preserveAspect = true;
        }

        RefreshArtifactIcons(gladiator);

        if (detailText != null)
        {
            detailText.text = BuildGladiatorDetailText(gladiator);
        }

        SetDetailPanelActive(true);
    }

    private string BuildGladiatorDetailText(OwnedGladiatorData gladiator)
    {
        _detailBuilder.Clear();
        _detailBuilder.AppendLine($"이름 : {gladiator.DisplayName}");
        _detailBuilder.AppendLine($"레벨 : {gladiator.Level}");
        _detailBuilder.AppendLine($"경험치 : {gladiator.Exp}");
        _detailBuilder.AppendLine($"충성도 : {gladiator.Loyalty}");
        _detailBuilder.AppendLine(
            $"체력 : {Mathf.FloorToInt(gladiator.CurrentHealth)} / {Mathf.FloorToInt(gladiator.CachedMaxHealth)}"
        );
        _detailBuilder.AppendLine($"공격력 : {Mathf.FloorToInt(gladiator.CachedAttack)}");
        _detailBuilder.AppendLine($"공격속도 : {gladiator.CachedAttackSpeed:0.##}");
        _detailBuilder.AppendLine($"이동속도 : {gladiator.CachedMoveSpeed:0.##}");
        _detailBuilder.AppendLine($"공격 사거리 : {gladiator.CachedAttackRange:0.##}");

        return _detailBuilder.ToString();
    }

    private void RefreshArtifactIcons(OwnedGladiatorData gladiator)
    {
        if (detailArtifactIcons == null)
        {
            return;
        }

        Sprite equippedArtifactIcon = gladiator.EquippedArtifact?.icon;
        for (int i = 0; i < detailArtifactIcons.Length; i++)
        {
            Image icon = detailArtifactIcons[i];
            if (icon == null)
            {
                continue;
            }

            // 현재 데이터 모델은 검투사당 장신구 1개만 지원한다.
            // UI는 3칸을 먼저 준비해두고, 실제 다중 장신구 데이터가 생기면 여기서 순서대로 채우면 된다.
            Sprite artifactIcon = i == 0 ? equippedArtifactIcon : null;
            icon.sprite = artifactIcon;
            icon.enabled = artifactIcon != null;
            icon.preserveAspect = true;
        }
    }

    private void SetDetailPanelActive(bool value)
    {
        if (detailPanelRoot != null)
        {
            detailPanelRoot.SetActive(value);
        }
    }

    private void SetPanelActive(bool value)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(value);
        }
    }

    private void ResolveMissingReferences()
    {
        if ((slotCells == null || slotCells.Length == 0) && panelRoot != null)
        {
            slotCells = panelRoot.GetComponentsInChildren<SquadSlotCell>(true);
        }

        if (pickerPanelRoot == null && pickerGridViewer != null)
        {
            pickerPanelRoot = pickerGridViewer.gameObject;
        }
    }

    private void BindTeamTabButtons()
    {
        if (teamTabButtons == null)
        {
            return;
        }

        for (int i = 0; i < teamTabButtons.Length; i++)
        {
            Button button = teamTabButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnTeamTabClicked(capturedIndex));
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
