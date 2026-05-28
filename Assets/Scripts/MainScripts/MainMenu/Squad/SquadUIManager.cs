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
    private enum PickerSortMode
    {
        RecentAcquired,
        Level,
    }

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
    private RectTransform squadBackground;

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

    [SerializeField]
    private Button recentAcquiredSortButton;

    [SerializeField]
    private Button levelSortButton;

    [SerializeField]
    private RectTransform pickerBackground;

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
    private Button detailBackButton;

    [SerializeField]
    private RawImage detailPortraitImage;

    [SerializeField]
    private GladiatorModelPreviewView detailPortraitPreviewView;

    [SerializeField]
    private Image detailWeaponIcon;

    [SerializeField]
    private WeaponModelPreviewView detailWeaponPreviewView;

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
    private Transform[] _teamTabOriginalParents;
    private int[] _teamTabOriginalSiblingIndices;
    private Transform[] _sortButtonOriginalParents;
    private int[] _sortButtonOriginalSiblingIndices;
    private PickerSortMode _currentPickerSortMode = PickerSortMode.RecentAcquired;

    private readonly List<OwnedItemViewData> _pickerBuffer = new List<OwnedItemViewData>();
    private readonly List<OwnedGladiatorData> _sortedPickerBuffer = new List<OwnedGladiatorData>();
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
        CaptureTeamTabLayout();
        CapturePickerSortButtonLayout();

        BindButton(backButton, OnBackClicked);
        BindButton(pickerCloseButton, OnPickerCloseClicked);
        BindButton(pickerClearButton, OnPickerClearClicked);
        BindButton(recentAcquiredSortButton, OnRecentAcquiredSortClicked);
        BindButton(levelSortButton, OnLevelSortClicked);
        BindButton(detailBackButton, OnDetailBackClicked);
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

    private void OnRecentAcquiredSortClicked()
    {
        _currentPickerSortMode = PickerSortMode.RecentAcquired;
        RefreshPickerSortButtons();
        if (_pendingSlotIndex >= 0)
        {
            RefreshPickerGrid(_pendingSlotIndex);
        }
    }

    private void OnLevelSortClicked()
    {
        _currentPickerSortMode = PickerSortMode.Level;
        RefreshPickerSortButtons();
        if (_pendingSlotIndex >= 0)
        {
            RefreshPickerGrid(_pendingSlotIndex);
        }
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

    private void OnDetailBackClicked()
    {
        _pendingGladiator = null;
        SetDetailPanelActive(false);
    }

    private void OpenPicker(int slotIndex)
    {
        if (pickerTitleText != null)
        {
            pickerTitleText.text = $"슬롯 {slotIndex + 1} 검투사 선택";
        }

        _pendingGladiator = null;
        SetDetailPanelActive(false);
        RefreshPickerSortButtons();
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
        BuildSortedPickerBuffer(owned);

        for (int i = 0; i < _sortedPickerBuffer.Count; i++)
        {
            OwnedGladiatorData g = _sortedPickerBuffer[i];
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

            GameObject modelPrefab = g.GladiatorClass != null ? g.GladiatorClass.previewModelPrefab : null;
            Sprite portrait = g.GladiatorClass != null ? g.GladiatorClass.icon : null;
            _pickerBuffer.Add(
                new OwnedItemViewData(
                    modelPrefab,
                    g.CustomizeIndicates,
                    portrait,
                    g.DisplayName,
                    $"Lv.{g.Level}",
                    string.Empty,
                    g
                )
            );
        }

        Canvas.ForceUpdateCanvases();
        pickerGridViewer.SetItems(_pickerBuffer, OnPickerCellClicked);
    }

    private void BuildSortedPickerBuffer(IReadOnlyList<OwnedGladiatorData> owned)
    {
        _sortedPickerBuffer.Clear();
        if (owned == null)
        {
            return;
        }

        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i] != null)
            {
                _sortedPickerBuffer.Add(owned[i]);
            }
        }

        _sortedPickerBuffer.Sort(CompareGladiatorsForCurrentSort);
    }

    private int CompareGladiatorsForCurrentSort(OwnedGladiatorData left, OwnedGladiatorData right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        if (_currentPickerSortMode == PickerSortMode.Level)
        {
            int levelCompare = right.Level.CompareTo(left.Level);
            if (levelCompare != 0)
            {
                return levelCompare;
            }
        }

        return right.RuntimeId.CompareTo(left.RuntimeId);
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

            teamTabButtons[i].interactable = true;
            MoveTeamTabAroundBackground(teamTabButtons[i], i == activeTeamIndex);
        }
    }

    private void RefreshPickerSortButtons()
    {
        MovePickerSortButtonAroundBackground(
            recentAcquiredSortButton,
            _currentPickerSortMode == PickerSortMode.RecentAcquired
        );
        MovePickerSortButtonAroundBackground(levelSortButton, _currentPickerSortMode == PickerSortMode.Level);
    }

    private void MovePickerSortButtonAroundBackground(Button sortButton, bool isActive)
    {
        if (sortButton == null || pickerBackground == null)
        {
            return;
        }

        RectTransform buttonTransform = sortButton.transform as RectTransform;
        if (buttonTransform == null)
        {
            return;
        }

        int buttonIndex = GetPickerSortButtonIndex(sortButton);
        if (!isActive)
        {
            RestorePickerSortButtonParent(buttonTransform, buttonIndex);
            return;
        }

        Transform backgroundParent = pickerBackground.parent;
        if (backgroundParent == null)
        {
            return;
        }

        buttonTransform.SetParent(backgroundParent, true);
        int backgroundIndex = pickerBackground.GetSiblingIndex();
        buttonTransform.SetSiblingIndex(Mathf.Min(backgroundIndex + 1, backgroundParent.childCount - 1));
    }

    private void RestorePickerSortButtonParent(RectTransform buttonTransform, int buttonIndex)
    {
        if (
            buttonTransform == null
            || _sortButtonOriginalParents == null
            || _sortButtonOriginalSiblingIndices == null
            || buttonIndex < 0
            || buttonIndex >= _sortButtonOriginalParents.Length
        )
        {
            return;
        }

        Transform originalParent = _sortButtonOriginalParents[buttonIndex];
        if (originalParent == null)
        {
            return;
        }

        if (buttonTransform.parent != originalParent)
        {
            buttonTransform.SetParent(originalParent, true);
        }

        int siblingIndex = Mathf.Clamp(
            _sortButtonOriginalSiblingIndices[buttonIndex],
            0,
            originalParent.childCount - 1
        );
        buttonTransform.SetSiblingIndex(siblingIndex);
    }

    private int GetPickerSortButtonIndex(Button sortButton)
    {
        if (sortButton == recentAcquiredSortButton)
        {
            return 0;
        }

        if (sortButton == levelSortButton)
        {
            return 1;
        }

        return -1;
    }

    private void MoveTeamTabAroundBackground(Button teamTabButton, bool isActive)
    {
        if (teamTabButton == null || squadBackground == null)
        {
            return;
        }

        RectTransform tabTransform = teamTabButton.transform as RectTransform;
        if (tabTransform == null)
        {
            return;
        }

        int tabIndex = System.Array.IndexOf(teamTabButtons, teamTabButton);
        if (!isActive)
        {
            RestoreTeamTabParent(tabTransform, tabIndex);
            return;
        }

        Transform backgroundParent = squadBackground.parent;
        if (backgroundParent == null)
        {
            return;
        }

        // SquadBackground와 SquadSlotPanel이 형제인 배치라 선택된 탭만 배경의 형제로 올려 렌더 순서를 분리한다.
        tabTransform.SetParent(backgroundParent, true);
        int backgroundIndex = squadBackground.GetSiblingIndex();
        tabTransform.SetSiblingIndex(Mathf.Min(backgroundIndex + 1, backgroundParent.childCount - 1));
    }

    private void RestoreTeamTabParent(RectTransform tabTransform, int tabIndex)
    {
        if (tabTransform == null || _teamTabOriginalParents == null || _teamTabOriginalSiblingIndices == null)
        {
            return;
        }

        if (tabIndex < 0 || tabIndex >= _teamTabOriginalParents.Length)
        {
            return;
        }

        Transform originalParent = _teamTabOriginalParents[tabIndex];
        if (originalParent == null)
        {
            return;
        }

        if (tabTransform.parent != originalParent)
        {
            tabTransform.SetParent(originalParent, true);
        }

        int siblingIndex = Mathf.Clamp(_teamTabOriginalSiblingIndices[tabIndex], 0, originalParent.childCount - 1);
        tabTransform.SetSiblingIndex(siblingIndex);
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

        GameObject modelPrefab = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.previewModelPrefab : null;
        if (detailPortraitPreviewView != null && modelPrefab != null)
        {
            detailPortraitPreviewView.Show(
                modelPrefab,
                gladiator.CustomizeIndicates,
                gladiator.EquippedWeapon?.Weapon?.leftWeaponPrefab,
                gladiator.EquippedWeapon?.Weapon?.rightWeaponPrefab
            );
            if (detailPortraitImage != null && detailPortraitImage.GetComponent<GladiatorModelPreviewView>() == null)
            {
                detailPortraitImage.enabled = false;
            }
        }
        else if (detailPortraitImage != null)
        {
            if (detailPortraitPreviewView != null)
            {
                detailPortraitPreviewView.Clear();
            }

            Sprite portrait = gladiator.GladiatorClass != null ? gladiator.GladiatorClass.icon : null;
            detailPortraitImage.texture = portrait != null ? portrait.texture : null;
            detailPortraitImage.enabled = portrait != null;
        }

        SetWeaponPreview(detailWeaponIcon, detailWeaponPreviewView, gladiator.EquippedWeapon);

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
        _detailBuilder.AppendLine(BuildPersonalityNameText(gladiator));
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

    private static string BuildPersonalityNameText(OwnedGladiatorData gladiator)
    {
        if (gladiator == null)
        {
            return string.Empty;
        }

        string personalityName =
            gladiator.Personality != null && !string.IsNullOrWhiteSpace(gladiator.Personality.personalityName)
                ? gladiator.Personality.personalityName
                : "성격 없음";

        return $"<color=#FFFFFF>{personalityName}</color> {gladiator.DisplayName}";
    }

    private void RefreshArtifactIcons(OwnedGladiatorData gladiator)
    {
        if (detailArtifactIcons == null)
        {
            return;
        }

        Sprite equippedArtifactIcon = gladiator.EquippedArtifact?.Artifact?.icon;
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

    private static void SetWeaponPreview(
        Image fallbackImage,
        WeaponModelPreviewView previewView,
        OwnedWeaponData weapon
    )
    {
        GameObject leftPrefab = weapon?.Weapon?.leftWeaponPrefab;
        GameObject rightPrefab = weapon?.Weapon?.rightWeaponPrefab;
        bool usePreview = previewView != null && (leftPrefab != null || rightPrefab != null);

        if (previewView != null)
        {
            if (usePreview)
            {
                previewView.Show(leftPrefab, rightPrefab);
            }
            else
            {
                previewView.Clear();
            }
        }

        if (fallbackImage != null)
        {
            fallbackImage.sprite = usePreview ? null : weapon?.Weapon?.icon;
            fallbackImage.enabled = !usePreview && weapon?.Weapon?.icon != null;
            fallbackImage.preserveAspect = true;
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

        if (squadBackground == null && panelRoot != null)
        {
            Transform backgroundTransform = FindChildTransform(panelRoot.transform, "SquadBackground");
            squadBackground = backgroundTransform as RectTransform;
        }

        if (pickerBackground == null && pickerPanelRoot != null)
        {
            Transform pickerBackgroundTransform =
                FindChildTransform(pickerPanelRoot.transform, "GladiatorBackground")
                ?? FindChildTransform(pickerPanelRoot.transform, "PickerBackground");
            pickerBackground = pickerBackgroundTransform as RectTransform;
        }

        if (detailPortraitPreviewView == null && detailPortraitImage != null)
        {
            detailPortraitPreviewView = detailPortraitImage.GetComponent<GladiatorModelPreviewView>();
        }

        if (detailWeaponPreviewView == null && detailWeaponIcon != null)
        {
            detailWeaponPreviewView = detailWeaponIcon.GetComponentInChildren<WeaponModelPreviewView>(true);
        }
    }

    private static Transform FindChildTransform(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildTransform(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CaptureTeamTabLayout()
    {
        if (teamTabButtons == null)
        {
            return;
        }

        _teamTabOriginalParents = new Transform[teamTabButtons.Length];
        _teamTabOriginalSiblingIndices = new int[teamTabButtons.Length];

        for (int i = 0; i < teamTabButtons.Length; i++)
        {
            Button button = teamTabButtons[i];
            if (button == null)
            {
                continue;
            }

            Transform tabTransform = button.transform;
            _teamTabOriginalParents[i] = tabTransform.parent;
            _teamTabOriginalSiblingIndices[i] = tabTransform.GetSiblingIndex();
        }
    }

    private void CapturePickerSortButtonLayout()
    {
        Button[] sortButtons = { recentAcquiredSortButton, levelSortButton };
        _sortButtonOriginalParents = new Transform[sortButtons.Length];
        _sortButtonOriginalSiblingIndices = new int[sortButtons.Length];

        for (int i = 0; i < sortButtons.Length; i++)
        {
            Button button = sortButtons[i];
            if (button == null)
            {
                continue;
            }

            Transform buttonTransform = button.transform;
            _sortButtonOriginalParents[i] = buttonTransform.parent;
            _sortButtonOriginalSiblingIndices[i] = buttonTransform.GetSiblingIndex();
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
