using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleUIManager : MonoBehaviour
{
    private static readonly BattleEncounterDifficulty[] DifficultyOrder =
    {
        BattleEncounterDifficulty.VeryLow,
        BattleEncounterDifficulty.Low,
        BattleEncounterDifficulty.Medium,
        BattleEncounterDifficulty.High,
    };

    [Header("Battle Preparation Panel")]
    [SerializeField]
    private GameObject battlePanelRoot;

    [SerializeField]
    private TMP_Text battleBodyText;

    [Header("Difficulty View")]
    [SerializeField]
    private TMP_Text difficultyText;

    [SerializeField]
    private RectTransform enemyBackground;

    [SerializeField]
    private Button veryEasyDifficultyTabButton;

    [SerializeField]
    private Button easyDifficultyTabButton;

    [SerializeField]
    private Button normalDifficultyTabButton;

    [SerializeField]
    private Button hardDifficultyTabButton;

    [SerializeField]
    private GameObject enemyInfoPanelRoot;

    [SerializeField]
    private TMP_Text storyText;

    [SerializeField]
    private TMP_Text rewardGoldText;

    [SerializeField]
    private Image enemyIcon1;

    [SerializeField]
    private Image enemyIcon2;

    [SerializeField]
    private Image enemyIcon3;

    [SerializeField]
    private Image enemyIcon4;

    [SerializeField]
    private Image enemyIcon5;

    [SerializeField]
    private Image enemyIcon6;

    [SerializeField]
    private TMP_Text enemyNameText1;

    [SerializeField]
    private TMP_Text enemyNameText2;

    [SerializeField]
    private TMP_Text enemyNameText3;

    [SerializeField]
    private TMP_Text enemyNameText4;

    [SerializeField]
    private TMP_Text enemyNameText5;

    [SerializeField]
    private TMP_Text enemyNameText6;

    [SerializeField]
    private TMP_Text enemyLevelText1;

    [SerializeField]
    private TMP_Text enemyLevelText2;

    [SerializeField]
    private TMP_Text enemyLevelText3;

    [SerializeField]
    private TMP_Text enemyLevelText4;

    [SerializeField]
    private TMP_Text enemyLevelText5;

    [SerializeField]
    private TMP_Text enemyLevelText6;

    [Header("Ally Info Panel")]
    [SerializeField]
    private GameObject allyInfoPanelRoot;

    [SerializeField]
    private RectTransform allyBackground;

    [SerializeField]
    private TMP_Text selectedSquadNameText;

    [SerializeField]
    private Button[] allySquadButtons = new Button[SquadManager.SquadTeamCount];

    [SerializeField]
    private Image[] allyImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] allyNameTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] allyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] allyWeaponImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] allyArtifactImages = new Image[BattleTeamConstants.MaxUnitsPerTeam * 3];

    [Header("Enemy Tooltip")]
    [SerializeField]
    private RectTransform enemyTooltipRoot;

    [SerializeField]
    private Vector2 enemyTooltipOffset = new Vector2(16f, -8f);

    [SerializeField]
    private Image enemyTooltipIcon;

    [SerializeField]
    private TMP_Text enemyTooltipLevelText;

    [SerializeField]
    private RawImage enemyTooltipLevelIcon;

    [SerializeField]
    private TMP_Text enemyTooltipAttackText;

    [SerializeField]
    private RawImage enemyTooltipAttackIcon;

    [SerializeField]
    private TMP_Text enemyTooltipHealthText;

    [SerializeField]
    private RawImage enemyTooltipHealthIcon;

    [SerializeField]
    private TMP_Text enemyTooltipAttackSpeedText;

    [SerializeField]
    private RawImage enemyTooltipAttackSpeedIcon;

    [SerializeField]
    private TMP_Text enemyTooltipMoveSpeedText;

    [SerializeField]
    private RawImage enemyTooltipMoveSpeedIcon;

    [SerializeField]
    private TMP_Text enemyTooltipRangeText;

    [SerializeField]
    private RawImage enemyTooltipRangeIcon;

    [Header("Preparation Buttons")]
    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button backButton;

    [Header("Deployment Panel")]
    [SerializeField]
    private GameObject deploymentPanelRoot;

    [SerializeField]
    private RectTransform deploymentBoardArea;

    [SerializeField]
    private Button deploymentStartButton;

    [SerializeField]
    private Button deploymentBackButton;

    [SerializeField]
    private Image[] deployEnemyIcons = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] deployEnemyMaskImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] deployEnemyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] deployAllyIcons = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] deployAllyMaskImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] deployAllyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RectTransform[] deploymentBoardEnemyViews = new RectTransform[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] deploymentBoardEnemyIcons = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RectTransform[] deploymentBoardAllyViews = new RectTransform[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] deploymentBoardAllyIcons = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    private MainFlowManager _flow;
    private BattleManager _battleManager;
    private SquadManager _squadManager;
    private IReadOnlyList<BattleEncounterPreview> _currentEncounters;
    private readonly BattleUnitSnapshot[] _visibleEnemies = new BattleUnitSnapshot[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly BattleUnitSnapshot[] _visibleAllies = new BattleUnitSnapshot[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly int[] _allyRuntimeIdsByDeploymentSlot = new int[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly int[] _enemyUnitIndicesByDeploymentSlot = new int[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly Vector2[] _allyNormalizedPositionsByDeploymentSlot = new Vector2[
        BattleTeamConstants.MaxUnitsPerTeam
    ];
    private readonly Vector2[] _enemyNormalizedPositionsByDeploymentSlot = new Vector2[
        BattleTeamConstants.MaxUnitsPerTeam
    ];
    private readonly bool[] _isAllyBoardIconPlacedBySlot = new bool[BattleTeamConstants.MaxUnitsPerTeam];
    private int _currentEncounterIndex = -1;
    private int _selectedAllyInfoTeamIndex;
    private int _deploymentSquadTeamIndex;
    private int _hoveredEnemyIndex = -1;
    private int _hoveredAllyIndex = -1;
    private int _draggingAllySlotIndex = -1;
    private bool _initialized;
    private bool _enemyTooltipVisible;
    private bool _loggedMissingEnemyTooltipRoot;

    public void Initialize(MainFlowManager flow, BattleManager battleManager, SquadManager squadManager = null)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _battleManager = battleManager;
        _squadManager = squadManager;

        BindDifficultyTabButtons();
        BindButton(startButton, OnStartClicked);
        BindButton(backButton, OnBackClicked);
        BindButton(deploymentStartButton, OnDeploymentStartClicked);
        BindButton(deploymentBackButton, OnDeploymentBackClicked);
        BindAllySquadButtons();
        BindEnemyHoverTargets();
        BindAllyHoverTargets();
        BindDeploymentAllyDragTargets();

        CloseAll();

        _initialized = true;
    }

    public void OpenBattlePanel()
    {
        IReadOnlyList<BattleEncounterPreview> encounters =
            _battleManager != null ? _battleManager.DailyEncounters : null;

        OpenBattlePanel(encounters, _battleManager != null ? _battleManager.SelectedEncounterIndex : -1);
    }

    public void OpenBattlePanel(IReadOnlyList<BattleEncounterPreview> encounters, int selectedIndex)
    {
        _currentEncounters = encounters;
        SetActive(battlePanelRoot, true);
        SetActive(deploymentPanelRoot, false);
        SetPreparationContentActive(true);
        _selectedAllyInfoTeamIndex = _squadManager != null ? _squadManager.ActiveTeamIndex : 0;

        if (battleBodyText != null)
        {
            battleBodyText.text = "상대 정보를 확인하고 전투를 시작하세요.";
        }

        int defaultEasyIndex = FindEncounterIndexByDifficulty(BattleEncounterDifficulty.Low);
        if (defaultEasyIndex < 0)
        {
            defaultEasyIndex = GetFirstAvailableEncounterIndex();
        }

        SetCurrentEncounter(defaultEasyIndex, true);

        if (startButton != null)
        {
            startButton.interactable = _currentEncounterIndex >= 0;
        }

        if (backButton != null)
        {
            backButton.interactable = true;
        }
    }

    public void RefreshSelection(int selectedIndex)
    {
        if (!IsValidEncounterIndex(selectedIndex))
        {
            return;
        }

        _currentEncounterIndex = selectedIndex;
        RenderCurrentEncounter();
    }

    public void CloseAll()
    {
        _currentEncounters = null;
        _currentEncounterIndex = -1;

        SetActive(battlePanelRoot, false);
        SetActive(deploymentPanelRoot, false);
        HideEnemyTooltip();
        ClearEnemySlots();
        ClearAllySlots();
        ClearDeploymentState();
        SetActive(enemyInfoPanelRoot, false);
        SetActive(allyInfoPanelRoot, false);

        if (difficultyText != null)
        {
            difficultyText.text = string.Empty;
        }

        if (rewardGoldText != null)
        {
            rewardGoldText.text = string.Empty;
        }

        if (startButton != null)
        {
            startButton.interactable = true;
        }

        if (backButton != null)
        {
            backButton.interactable = true;
        }
    }

    private void SetCurrentEncounter(int encounterIndex, bool selectEncounter)
    {
        _currentEncounterIndex = encounterIndex;
        RenderCurrentEncounter();

        if (selectEncounter && _flow != null && _currentEncounterIndex >= 0)
        {
            _flow.HandleBattleEncounterSelected(_currentEncounterIndex);
        }
    }

    private void RenderCurrentEncounter()
    {
        BattleEncounterPreview encounter = GetEncounterOrNull(_currentEncounterIndex);
        bool hasEncounter = encounter != null;

        if (difficultyText != null)
        {
            difficultyText.text = hasEncounter ? GetDifficultyDisplayName(encounter.Difficulty) : "-";
        }

        if (rewardGoldText != null)
        {
            rewardGoldText.text = hasEncounter ? $"{encounter.PreviewRewardGold} 골드" : string.Empty;
        }

        RenderEnemyImages(encounter);
        RenderAllyInfoPanel();
        RefreshDifficultyTabs();
        RefreshAllySquadTabs();

        if (startButton != null)
        {
            startButton.interactable = hasEncounter;
        }
    }

    private void RenderEnemyImages(BattleEncounterPreview encounter)
    {
        Image[] enemyIcons = GetEnemyIcons();
        TMP_Text[] enemyNameTexts = GetEnemyNameTexts();
        TMP_Text[] enemyLevelTexts = GetEnemyLevelTexts();

        IReadOnlyList<BattleUnitSnapshot> enemies = encounter != null ? encounter.EnemyUnits : null;
        int enemyCount = enemies != null ? enemies.Count : 0;

        for (int i = 0; i < enemyIcons.Length; i++)
        {
            BattleUnitSnapshot enemy = i < enemyCount ? enemies[i] : null;
            bool hasEnemy = enemy != null;
            _visibleEnemies[i] = enemy;

            if (enemyIcons[i] != null)
            {
                enemyIcons[i].sprite = hasEnemy ? enemy.PortraitSprite : null;
                enemyIcons[i].enabled = hasEnemy && enemy.PortraitSprite != null;
            }

            if (enemyNameTexts[i] != null)
            {
                enemyNameTexts[i].text = hasEnemy ? enemy.DisplayName : string.Empty;
            }

            if (enemyLevelTexts[i] != null)
            {
                enemyLevelTexts[i].text = hasEnemy ? $"Lv.{enemy.Level}" : string.Empty;
            }
        }
    }

    private void ClearEnemySlots()
    {
        Image[] enemyIcons = GetEnemyIcons();
        TMP_Text[] enemyNameTexts = GetEnemyNameTexts();
        TMP_Text[] enemyLevelTexts = GetEnemyLevelTexts();
        HideEnemyTooltip();

        for (int i = 0; i < enemyIcons.Length; i++)
        {
            _visibleEnemies[i] = null;

            if (enemyIcons[i] != null)
            {
                enemyIcons[i].sprite = null;
                enemyIcons[i].enabled = false;
            }

            if (enemyNameTexts[i] != null)
            {
                enemyNameTexts[i].text = string.Empty;
            }

            if (enemyLevelTexts[i] != null)
            {
                enemyLevelTexts[i].text = string.Empty;
            }
        }
    }

    private void RenderAllyInfoPanel()
    {
        if (selectedSquadNameText != null)
        {
            selectedSquadNameText.text = $"Squad {_selectedAllyInfoTeamIndex + 1}";
        }

        IReadOnlyList<OwnedGladiatorData> allies =
            _squadManager != null
                ? _squadManager.GetAssignedGladiators(_selectedAllyInfoTeamIndex)
                : System.Array.Empty<OwnedGladiatorData>();

        for (int i = 0; i < _visibleAllies.Length; i++)
        {
            OwnedGladiatorData ally = allies != null && i < allies.Count ? allies[i] : null;
            BattleUnitSnapshot snapshot =
                ally != null ? BattleUnitSnapshot.FromOwnedGladiator(ally, BattleTeamIds.Player) : null;
            _visibleAllies[i] = snapshot;

            Sprite portrait = snapshot != null ? snapshot.PortraitSprite : null;
            SetImage(GetArrayValue(allyImages, i), portrait);
            SetText(GetArrayValue(allyNameTexts, i), snapshot != null ? snapshot.DisplayName : string.Empty);
            SetText(GetArrayValue(allyLevelTexts, i), snapshot != null ? $"Lv.{snapshot.Level}" : string.Empty);
            SetImage(GetArrayValue(allyWeaponImages, i), ally != null ? ally.EquippedWeapon?.Weapon?.icon : null);
            SetAllyArtifactImages(i, ally);
        }
    }

    private void ClearAllySlots()
    {
        HideEnemyTooltip();
        for (int i = 0; i < _visibleAllies.Length; i++)
        {
            _visibleAllies[i] = null;
            SetImage(GetArrayValue(allyImages, i), null);
            SetText(GetArrayValue(allyNameTexts, i), string.Empty);
            SetText(GetArrayValue(allyLevelTexts, i), string.Empty);
            SetImage(GetArrayValue(allyWeaponImages, i), null);
            SetAllyArtifactImages(i, null);
        }
    }

    private void SetAllyArtifactImages(int allyIndex, OwnedGladiatorData ally)
    {
        for (int slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            int imageIndex = (allyIndex * 3) + slotIndex;
            Sprite icon =
                slotIndex == 0 && ally != null && ally.EquippedArtifact != null
                    ? ally.EquippedArtifact.Artifact?.icon
                    : null;
            SetImage(GetArrayValue(allyArtifactImages, imageIndex), icon);
        }
    }

    private void Update()
    {
        RefreshEnemyTooltipHoverFallback();

        if (_enemyTooltipVisible)
        {
            UpdateEnemyTooltipPosition();
        }
    }

    private void RefreshDifficultyTabs()
    {
        Button[] buttons = GetDifficultyTabButtons();
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttons[i].interactable = FindEncounterIndexByDifficulty(DifficultyOrder[i]) >= 0;
            }
        }

        MoveButtonsAroundBackground(buttons, enemyBackground, GetCurrentDifficultyOrderIndex());
    }

    private void OnDifficultyTabClicked(BattleEncounterDifficulty difficulty)
    {
        int encounterIndex = FindEncounterIndexByDifficulty(difficulty);
        if (encounterIndex >= 0)
        {
            SetCurrentEncounter(encounterIndex, true);
        }
    }

    private int GetCurrentDifficultyOrderIndex()
    {
        BattleEncounterPreview encounter = GetEncounterOrNull(_currentEncounterIndex);
        if (encounter == null)
        {
            return -1;
        }

        for (int i = 0; i < DifficultyOrder.Length; i++)
        {
            if (DifficultyOrder[i] == encounter.Difficulty)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindEncounterIndexByDifficulty(BattleEncounterDifficulty difficulty)
    {
        if (_currentEncounters == null)
        {
            return -1;
        }

        for (int i = 0; i < _currentEncounters.Count; i++)
        {
            BattleEncounterPreview encounter = _currentEncounters[i];
            if (encounter != null && encounter.Difficulty == difficulty)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetFirstAvailableEncounterIndex()
    {
        if (_currentEncounters == null)
        {
            return -1;
        }

        for (int i = 0; i < _currentEncounters.Count; i++)
        {
            if (_currentEncounters[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private BattleEncounterPreview GetEncounterOrNull(int index)
    {
        if (!IsValidEncounterIndex(index))
        {
            return null;
        }

        return _currentEncounters[index];
    }

    private bool IsValidEncounterIndex(int index)
    {
        return _currentEncounters != null && index >= 0 && index < _currentEncounters.Count;
    }

    private static string GetDifficultyDisplayName(BattleEncounterDifficulty difficulty)
    {
        switch (difficulty)
        {
            case BattleEncounterDifficulty.VeryLow:
                return "왕초보";
            case BattleEncounterDifficulty.Low:
                return "쉬움";
            case BattleEncounterDifficulty.Medium:
                return "보통";
            case BattleEncounterDifficulty.High:
                return "어려움";
            default:
                return difficulty.ToString();
        }
    }

    private Image[] GetEnemyIcons()
    {
        return new[] { enemyIcon1, enemyIcon2, enemyIcon3, enemyIcon4, enemyIcon5, enemyIcon6 };
    }

    private TMP_Text[] GetEnemyNameTexts()
    {
        return new[] { enemyNameText1, enemyNameText2, enemyNameText3, enemyNameText4, enemyNameText5, enemyNameText6 };
    }

    private TMP_Text[] GetEnemyLevelTexts()
    {
        return new[]
        {
            enemyLevelText1,
            enemyLevelText2,
            enemyLevelText3,
            enemyLevelText4,
            enemyLevelText5,
            enemyLevelText6,
        };
    }

    private Button[] GetDifficultyTabButtons()
    {
        return new[]
        {
            veryEasyDifficultyTabButton,
            easyDifficultyTabButton,
            normalDifficultyTabButton,
            hardDifficultyTabButton,
        };
    }

    private void BindDifficultyTabButtons()
    {
        Button[] buttons = GetDifficultyTabButtons();
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            BattleEncounterDifficulty difficulty = DifficultyOrder[i];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnDifficultyTabClicked(difficulty));
        }
    }

    private void BindEnemyHoverTargets()
    {
        Image[] enemyIcons = GetEnemyIcons();
        for (int i = 0; i < enemyIcons.Length; i++)
        {
            Image enemyIcon = enemyIcons[i];
            if (enemyIcon == null)
            {
                continue;
            }

            enemyIcon.raycastTarget = true;

            BindEnemyHoverTarget(enemyIcon, i);
        }
    }

    private void BindAllyHoverTargets()
    {
        if (allyImages == null)
        {
            return;
        }

        for (int i = 0; i < allyImages.Length; i++)
        {
            Image allyImage = allyImages[i];
            if (allyImage == null)
            {
                continue;
            }

            allyImage.raycastTarget = true;
            BindAllyHoverTarget(allyImage, i);
        }
    }

    private void BindEnemyHoverTarget(Image enemyIcon, int enemyIndex)
    {
        EventTrigger trigger = enemyIcon.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = enemyIcon.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit
        );

        AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnEnemyPointerEntered(enemyIndex));
        AddEventTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnEnemyPointerExited(enemyIndex));
    }

    private void BindAllyHoverTarget(Image allyImage, int allyIndex)
    {
        EventTrigger trigger = allyImage.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = allyImage.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit
        );

        AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnAllyPointerEntered(allyIndex));
        AddEventTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnAllyPointerExited(allyIndex));
    }

    private void BindDeploymentAllyDragTargets()
    {
        if (deployAllyIcons == null)
        {
            return;
        }

        for (int i = 0; i < deployAllyIcons.Length; i++)
        {
            Image allyIcon = deployAllyIcons[i];
            if (allyIcon == null)
            {
                continue;
            }

            allyIcon.raycastTarget = true;
            BindDeploymentAllyDragTarget(allyIcon, i);
        }
    }

    private void BindDeploymentAllyDragTarget(Image allyIcon, int slotIndex)
    {
        EventTrigger trigger = allyIcon.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = allyIcon.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.BeginDrag
            || entry.eventID == EventTriggerType.Drag
            || entry.eventID == EventTriggerType.EndDrag
        );

        AddEventTriggerEntry(trigger, EventTriggerType.BeginDrag, data => OnDeploymentAllyDragStarted(slotIndex, data));
        AddEventTriggerEntry(trigger, EventTriggerType.Drag, data => OnDeploymentAllyDragged(slotIndex, data));
        AddEventTriggerEntry(trigger, EventTriggerType.EndDrag, data => OnDeploymentAllyDragEnded(slotIndex, data));
    }

    private void ShowEnemyTooltip(int enemyIndex)
    {
        if (enemyIndex < 0 || enemyIndex >= _visibleEnemies.Length)
        {
            return;
        }

        ShowUnitTooltip(_visibleEnemies[enemyIndex], enemyIndex, -1, "Enemy");
    }

    private void ShowAllyTooltip(int allyIndex)
    {
        if (allyIndex < 0 || allyIndex >= _visibleAllies.Length)
        {
            return;
        }

        ShowUnitTooltip(_visibleAllies[allyIndex], -1, allyIndex, "Ally");
    }

    private void ShowUnitTooltip(BattleUnitSnapshot unit, int enemyIndex, int allyIndex, string logPrefix)
    {
        if (unit == null)
        {
            return;
        }

        if (enemyTooltipRoot == null)
        {
            if (!_loggedMissingEnemyTooltipRoot)
            {
                Debug.LogWarning("Enemy Tooltip Root가 BattleUIManager inspector에 연결되지 않았습니다.");
                _loggedMissingEnemyTooltipRoot = true;
            }

            return;
        }

        Debug.Log($"{logPrefix} hover 감지됨: {unit.DisplayName}");

        if (enemyTooltipIcon != null)
        {
            enemyTooltipIcon.sprite = unit.PortraitSprite;
            enemyTooltipIcon.enabled = unit.PortraitSprite != null;
        }

        SetText(enemyTooltipLevelText, unit.Level.ToString());
        SetText(enemyTooltipAttackText, FormatStat(unit.Attack));
        SetText(enemyTooltipHealthText, FormatStat(unit.MaxHealth));
        SetText(enemyTooltipAttackSpeedText, FormatStat(unit.AttackSpeed));
        SetText(enemyTooltipMoveSpeedText, FormatStat(unit.MoveSpeed));
        SetText(enemyTooltipRangeText, FormatStat(unit.AttackRange));

        SetTooltipStatIconsActive(true);
        enemyTooltipRoot.gameObject.SetActive(true);
        enemyTooltipRoot.SetAsLastSibling();
        _hoveredEnemyIndex = enemyIndex;
        _hoveredAllyIndex = allyIndex;
        _enemyTooltipVisible = true;
        UpdateEnemyTooltipPosition();
    }

    private void HideEnemyTooltip()
    {
        _enemyTooltipVisible = false;
        _hoveredEnemyIndex = -1;
        _hoveredAllyIndex = -1;

        if (enemyTooltipRoot != null)
        {
            enemyTooltipRoot.gameObject.SetActive(false);
        }
    }

    private void RefreshEnemyTooltipHoverFallback()
    {
        int enemyIndex = GetHoveredEnemyIndex();
        int allyIndex = enemyIndex < 0 ? GetHoveredAllyIndex() : -1;
        if (enemyIndex == _hoveredEnemyIndex && allyIndex == _hoveredAllyIndex)
        {
            return;
        }

        if (enemyIndex >= 0)
        {
            ShowEnemyTooltip(enemyIndex);
        }
        else if (allyIndex >= 0)
        {
            ShowAllyTooltip(allyIndex);
        }
        else
        {
            HideEnemyTooltip();
        }
    }

    private int GetHoveredEnemyIndex()
    {
        if (Mouse.current == null)
        {
            return -1;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Image[] enemyIcons = GetEnemyIcons();
        for (int i = 0; i < enemyIcons.Length; i++)
        {
            Image enemyIcon = enemyIcons[i];
            if (enemyIcon == null || !enemyIcon.gameObject.activeInHierarchy || _visibleEnemies[i] == null)
            {
                continue;
            }

            RectTransform rectTransform = enemyIcon.rectTransform;
            Canvas canvas = enemyIcon.GetComponentInParent<Canvas>();
            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetHoveredAllyIndex()
    {
        if (Mouse.current == null || allyImages == null)
        {
            return -1;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        int count = Mathf.Min(allyImages.Length, _visibleAllies.Length);
        for (int i = 0; i < count; i++)
        {
            Image allyImage = allyImages[i];
            if (allyImage == null || !allyImage.gameObject.activeInHierarchy || _visibleAllies[i] == null)
            {
                continue;
            }

            RectTransform rectTransform = allyImage.rectTransform;
            Canvas canvas = allyImage.GetComponentInParent<Canvas>();
            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateEnemyTooltipPosition()
    {
        if (enemyTooltipRoot == null || Mouse.current == null)
        {
            return;
        }

        RectTransform parentRect = enemyTooltipRoot.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas canvas = enemyTooltipRoot.GetComponentInParent<Canvas>();
        Camera eventCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                eventCamera,
                out Vector2 localPosition
            )
        )
        {
            return;
        }

        Vector2 tooltipSize = enemyTooltipRoot.rect.size;
        Rect parentBounds = parentRect.rect;
        enemyTooltipRoot.anchorMin = new Vector2(0f, 1f);
        enemyTooltipRoot.anchorMax = new Vector2(0f, 1f);
        enemyTooltipRoot.pivot = new Vector2(0f, 1f);

        Vector2 anchoredPosition =
            new Vector2(localPosition.x - parentBounds.xMin, localPosition.y - parentBounds.yMax) + enemyTooltipOffset;

        float minX = 0f;
        float maxX = Mathf.Max(0f, parentBounds.width - tooltipSize.x);
        float minY = -Mathf.Max(0f, parentBounds.height - tooltipSize.y);
        float maxY = 0f;

        if (maxX >= minX)
        {
            anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        }

        if (maxY >= minY)
        {
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        }

        enemyTooltipRoot.anchoredPosition = anchoredPosition;
    }

    private void SetTooltipStatIconsActive(bool value)
    {
        SetComponentActive(enemyTooltipLevelIcon, value);
        SetComponentActive(enemyTooltipAttackIcon, value);
        SetComponentActive(enemyTooltipHealthIcon, value);
        SetComponentActive(enemyTooltipAttackSpeedIcon, value);
        SetComponentActive(enemyTooltipMoveSpeedIcon, value);
        SetComponentActive(enemyTooltipRangeIcon, value);
    }

    private void OnStartClicked()
    {
        OpenDeploymentPanel();
    }

    private void OnDeploymentStartClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattleStartRequested(BuildCurrentDeploymentPlan());
        }
    }

    private void OnDeploymentBackClicked()
    {
        SetActive(deploymentPanelRoot, false);
        SetActive(battlePanelRoot, true);
        SetPreparationContentActive(true);
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattlePreparationBackRequested();
        }
    }

    private void OpenDeploymentPanel()
    {
        BattleEncounterPreview encounter = GetEncounterOrNull(_currentEncounterIndex);
        if (encounter == null)
        {
            return;
        }

        SetActive(battlePanelRoot, true);
        SetPreparationContentActive(false);
        SetActive(deploymentPanelRoot, true);
        HideEnemyTooltip();
        if (deploymentPanelRoot != null)
        {
            deploymentPanelRoot.transform.SetAsLastSibling();
        }

        _deploymentSquadTeamIndex = _selectedAllyInfoTeamIndex;

        ClearDeploymentState();
        SeedEnemyDeployment(encounter);
        SeedAllyDeploymentFromSelectedSquad();
        RefreshDeploymentAllyList();
        RefreshDeploymentEnemyList();
        RefreshDeploymentStartButton();
    }

    private void SetPreparationContentActive(bool value)
    {
        SetComponentActive(enemyBackground, value);
        SetComponentActive(veryEasyDifficultyTabButton, value);
        SetComponentActive(easyDifficultyTabButton, value);
        SetComponentActive(normalDifficultyTabButton, value);
        SetComponentActive(hardDifficultyTabButton, value);
        SetComponentActive(difficultyText, value);
        SetComponentActive(storyText, value);
        SetComponentActive(rewardGoldText, value);
        SetActive(enemyInfoPanelRoot, value);
        SetActive(allyInfoPanelRoot, value);
        SetComponentActive(startButton, value);
        SetComponentActive(backButton, value);
    }

    private void RefreshDeploymentAllyList()
    {
        IReadOnlyList<OwnedGladiatorData> allies = GetSelectedDeploymentSquadGladiators();
        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            OwnedGladiatorData ally = allies != null && i < allies.Count ? allies[i] : null;
            SetImage(GetArrayValue(deployAllyIcons, i), ally != null ? ally.GladiatorClass?.icon : null);
            SetComponentActive(GetArrayValue(deployAllyMaskImages, i), ally != null);
            SetText(GetArrayValue(deployAllyLevelTexts, i), ally != null ? $"Lv.{ally.Level}" : string.Empty);
            SetImage(GetArrayValue(deploymentBoardAllyIcons, i), ally != null ? ally.GladiatorClass?.icon : null);
            SetDeploymentBoardViewActive(i, true, ally != null && _isAllyBoardIconPlacedBySlot[i]);
            SetDeploymentBoardViewPosition(i, true, _allyNormalizedPositionsByDeploymentSlot[i]);
        }
    }

    private void RefreshDeploymentEnemyList()
    {
        BattleEncounterPreview encounter = GetEncounterOrNull(_currentEncounterIndex);
        IReadOnlyList<BattleUnitSnapshot> enemies = encounter != null ? encounter.EnemyUnits : null;
        int enemyCount = enemies != null ? enemies.Count : 0;

        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            BattleUnitSnapshot enemy = i < enemyCount ? enemies[i] : null;
            SetImage(GetArrayValue(deployEnemyIcons, i), enemy != null ? enemy.PortraitSprite : null);
            SetComponentActive(GetArrayValue(deployEnemyMaskImages, i), enemy != null);
            SetText(GetArrayValue(deployEnemyLevelTexts, i), enemy != null ? $"Lv.{enemy.Level}" : string.Empty);
            SetImage(GetArrayValue(deploymentBoardEnemyIcons, i), enemy != null ? enemy.PortraitSprite : null);
            SetDeploymentBoardViewActive(i, false, enemy != null);
            SetDeploymentBoardViewPosition(i, false, _enemyNormalizedPositionsByDeploymentSlot[i]);
        }
    }

    private void OnDeploymentAllyDragStarted(int slotIndex, BaseEventData eventData)
    {
        if (!IsValidDeploymentAllySlot(slotIndex))
        {
            return;
        }

        _draggingAllySlotIndex = slotIndex;
        _isAllyBoardIconPlacedBySlot[slotIndex] = true;
        SetDeploymentBoardViewActive(slotIndex, true, true);
        SetDeploymentAllyPositionFromEvent(slotIndex, eventData);
    }

    private void OnDeploymentAllyDragged(int slotIndex, BaseEventData eventData)
    {
        if (_draggingAllySlotIndex != slotIndex || !IsValidDeploymentAllySlot(slotIndex))
        {
            return;
        }

        SetDeploymentAllyPositionFromEvent(slotIndex, eventData);
    }

    private void OnDeploymentAllyDragEnded(int slotIndex, BaseEventData eventData)
    {
        if (_draggingAllySlotIndex == slotIndex && IsValidDeploymentAllySlot(slotIndex))
        {
            SetDeploymentAllyPositionFromEvent(slotIndex, eventData);
        }

        _draggingAllySlotIndex = -1;
    }

    private bool IsValidDeploymentAllySlot(int slotIndex)
    {
        return slotIndex >= 0
            && slotIndex < _allyRuntimeIdsByDeploymentSlot.Length
            && _allyRuntimeIdsByDeploymentSlot[slotIndex] >= 0
            && deploymentBoardArea != null;
    }

    private void SetDeploymentAllyPositionFromEvent(int slotIndex, BaseEventData eventData)
    {
        if (!(eventData is PointerEventData pointerEventData))
        {
            return;
        }

        Vector2 normalizedPosition = GetBoardNormalizedPosition(pointerEventData);
        normalizedPosition = BattleDeploymentPositionUtility.ClampToDeploymentHalf(normalizedPosition, true);
        _allyNormalizedPositionsByDeploymentSlot[slotIndex] = normalizedPosition;
        SetDeploymentBoardViewPosition(slotIndex, true, normalizedPosition);
    }

    private Vector2 GetBoardNormalizedPosition(PointerEventData pointerEventData)
    {
        Canvas canvas = deploymentBoardArea.GetComponentInParent<Canvas>();
        Camera eventCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                deploymentBoardArea,
                pointerEventData.position,
                eventCamera,
                out Vector2 localPoint
            )
        )
        {
            return Vector2.zero;
        }

        Rect rect = deploymentBoardArea.rect;
        float halfWidth = Mathf.Max(1f, rect.width * 0.5f);
        float halfHeight = Mathf.Max(1f, rect.height * 0.5f);
        Vector2 centeredPoint = localPoint - rect.center;
        return new Vector2(centeredPoint.x / halfWidth, centeredPoint.y / halfHeight);
    }

    private void SetDeploymentBoardViewActive(int slotIndex, bool isPlayerTeam, bool value)
    {
        RectTransform view = GetDeploymentBoardView(slotIndex, isPlayerTeam);
        if (view != null)
        {
            view.gameObject.SetActive(value);
        }
    }

    private void SetDeploymentBoardViewPosition(int slotIndex, bool isPlayerTeam, Vector2 normalizedPosition)
    {
        RectTransform view = GetDeploymentBoardView(slotIndex, isPlayerTeam);
        if (view == null || deploymentBoardArea == null)
        {
            return;
        }

        Rect rect = deploymentBoardArea.rect;
        Vector2 center = rect.center;
        Vector2 localPoint = new Vector2(
            center.x + normalizedPosition.x * rect.width * 0.5f,
            center.y + normalizedPosition.y * rect.height * 0.5f
        );
        Vector3 worldPoint = deploymentBoardArea.TransformPoint(localPoint);
        view.position = worldPoint;
    }

    private RectTransform GetDeploymentBoardView(int slotIndex, bool isPlayerTeam)
    {
        RectTransform view = GetArrayValue(
            isPlayerTeam ? deploymentBoardAllyViews : deploymentBoardEnemyViews,
            slotIndex
        );
        if (view != null)
        {
            return view;
        }

        Image fallbackImage = GetArrayValue(
            isPlayerTeam ? deploymentBoardAllyIcons : deploymentBoardEnemyIcons,
            slotIndex
        );
        if (fallbackImage == null)
        {
            return null;
        }

        RectTransform fallbackTransform = fallbackImage.rectTransform;
        if (deploymentBoardArea == null)
        {
            return fallbackTransform;
        }

        RectTransform current = fallbackTransform;
        while (current.parent is RectTransform parent)
        {
            if (parent == deploymentBoardArea)
            {
                return current;
            }

            current = parent;
        }

        return fallbackTransform;
    }

    private void OnAllyInfoSquadClicked(int teamIndex)
    {
        _selectedAllyInfoTeamIndex = Mathf.Clamp(teamIndex, 0, SquadManager.SquadTeamCount - 1);
        RenderAllyInfoPanel();
        RefreshAllySquadTabs();
    }

    private void SeedAllyDeploymentFromSelectedSquad()
    {
        IReadOnlyList<OwnedGladiatorData> allies = GetSelectedDeploymentSquadGladiators();
        if (allies == null)
        {
            return;
        }

        int count = Mathf.Min(allies.Count, _allyRuntimeIdsByDeploymentSlot.Length);
        for (int i = 0; i < count; i++)
        {
            if (allies[i] != null)
            {
                _allyRuntimeIdsByDeploymentSlot[i] = allies[i].RuntimeId;
                _allyNormalizedPositionsByDeploymentSlot[i] = BattleDeploymentPositionUtility.BuildDefaultPosition(
                    i,
                    count,
                    true
                );
            }
        }
    }

    private void SeedEnemyDeployment(BattleEncounterPreview encounter)
    {
        for (int i = 0; i < _enemyUnitIndicesByDeploymentSlot.Length; i++)
        {
            _enemyUnitIndicesByDeploymentSlot[i] = -1;
        }

        IReadOnlyList<BattleUnitSnapshot> enemies = encounter != null ? encounter.EnemyUnits : null;
        if (enemies == null)
        {
            return;
        }

        int count = Mathf.Min(enemies.Count, _enemyUnitIndicesByDeploymentSlot.Length);
        for (int i = 0; i < count; i++)
        {
            _enemyUnitIndicesByDeploymentSlot[i] = i;
            _enemyNormalizedPositionsByDeploymentSlot[i] =
                BattleDeploymentPositionUtility.BuildEnemyPlaceholderPosition(i);
        }
    }

    private void RefreshDeploymentStartButton()
    {
        if (deploymentStartButton != null)
        {
            deploymentStartButton.interactable = HasAnyAllyDeployment();
        }
    }

    private bool HasAnyAllyDeployment()
    {
        for (int i = 0; i < _allyRuntimeIdsByDeploymentSlot.Length; i++)
        {
            if (_allyRuntimeIdsByDeploymentSlot[i] >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<OwnedGladiatorData> GetSelectedDeploymentSquadGladiators()
    {
        return _squadManager != null
            ? _squadManager.GetAssignedGladiators(_deploymentSquadTeamIndex)
            : System.Array.Empty<OwnedGladiatorData>();
    }

    private OwnedGladiatorData FindSelectedDeploymentSquadGladiator(int runtimeId)
    {
        if (runtimeId < 0)
        {
            return null;
        }

        IReadOnlyList<OwnedGladiatorData> allies = GetSelectedDeploymentSquadGladiators();
        for (int i = 0; i < allies.Count; i++)
        {
            OwnedGladiatorData ally = allies[i];
            if (ally != null && ally.RuntimeId == runtimeId)
            {
                return ally;
            }
        }

        return null;
    }

    private BattleDeploymentPlan BuildCurrentDeploymentPlan()
    {
        return new BattleDeploymentPlan(
            _deploymentSquadTeamIndex,
            CopyIntArray(_allyRuntimeIdsByDeploymentSlot),
            CopyIntArray(_enemyUnitIndicesByDeploymentSlot),
            CopyVector2Array(_allyNormalizedPositionsByDeploymentSlot),
            CopyVector2Array(_enemyNormalizedPositionsByDeploymentSlot)
        );
    }

    private void ClearDeploymentState()
    {
        for (int i = 0; i < _allyRuntimeIdsByDeploymentSlot.Length; i++)
        {
            _allyRuntimeIdsByDeploymentSlot[i] = -1;
            _allyNormalizedPositionsByDeploymentSlot[i] = BattleDeploymentPositionUtility.BuildDefaultPosition(
                i,
                BattleTeamConstants.MaxUnitsPerTeam,
                true
            );
            SetImage(GetArrayValue(deploymentBoardAllyIcons, i), null);
            SetDeploymentBoardViewActive(i, true, false);
            _isAllyBoardIconPlacedBySlot[i] = false;
        }

        for (int i = 0; i < _enemyUnitIndicesByDeploymentSlot.Length; i++)
        {
            _enemyUnitIndicesByDeploymentSlot[i] = -1;
            _enemyNormalizedPositionsByDeploymentSlot[i] = BattleDeploymentPositionUtility.BuildDefaultPosition(
                i,
                BattleTeamConstants.MaxUnitsPerTeam,
                false
            );
            SetImage(GetArrayValue(deploymentBoardEnemyIcons, i), null);
            SetDeploymentBoardViewActive(i, false, false);
        }

        _draggingAllySlotIndex = -1;
    }

    private void BindAllySquadButtons()
    {
        if (allySquadButtons == null)
        {
            return;
        }

        for (int i = 0; i < allySquadButtons.Length; i++)
        {
            Button button = allySquadButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnAllyInfoSquadClicked(capturedIndex));
        }
    }

    private void RefreshAllySquadTabs()
    {
        if (allySquadButtons == null)
        {
            return;
        }

        for (int i = 0; i < allySquadButtons.Length; i++)
        {
            if (allySquadButtons[i] != null)
            {
                allySquadButtons[i].interactable = i < SquadManager.SquadTeamCount;
            }
        }

        MoveButtonsAroundBackground(allySquadButtons, allyBackground, _selectedAllyInfoTeamIndex);
    }

    private static int[] CopyIntArray(int[] source)
    {
        if (source == null)
        {
            return System.Array.Empty<int>();
        }

        int[] copy = new int[source.Length];
        System.Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static Vector2[] CopyVector2Array(Vector2[] source)
    {
        if (source == null)
        {
            return System.Array.Empty<Vector2>();
        }

        Vector2[] copy = new Vector2[source.Length];
        System.Array.Copy(source, copy, source.Length);
        return copy;
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

    private static void MoveButtonsAroundBackground(Button[] buttons, RectTransform background, int activeIndex)
    {
        if (buttons == null || background == null || activeIndex < 0 || activeIndex >= buttons.Length)
        {
            return;
        }

        Transform parent = background.parent;
        Button activeButton = buttons[activeIndex];
        if (parent == null || activeButton == null || activeButton.transform.parent != parent)
        {
            return;
        }

        List<Transform> orderedTransforms = new List<Transform>(buttons.Length + 1);
        int startIndex = background.GetSiblingIndex();

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.transform.parent != parent)
            {
                continue;
            }

            startIndex = Mathf.Min(startIndex, button.transform.GetSiblingIndex());
            if (i != activeIndex)
            {
                orderedTransforms.Add(button.transform);
            }
        }

        orderedTransforms.Add(background);
        orderedTransforms.Add(activeButton.transform);

        for (int i = 0; i < orderedTransforms.Count; i++)
        {
            orderedTransforms[i].SetSiblingIndex(Mathf.Clamp(startIndex + i, 0, parent.childCount - 1));
        }
    }

    private static void AddEventTriggerEntry(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> action
    )
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }

    private static void SetComponentActive(Component target, bool value)
    {
        if (target != null)
        {
            target.gameObject.SetActive(value);
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static T GetArrayValue<T>(T[] array, int index)
        where T : class
    {
        return array != null && index >= 0 && index < array.Length ? array[index] : null;
    }

    private static void SetImage(Image target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
        target.preserveAspect = true;
    }

    private static void SetRawImage(RawImage target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        if (sprite == null || sprite.texture == null)
        {
            target.texture = null;
            target.uvRect = new Rect(0f, 0f, 1f, 1f);
            target.enabled = false;
            return;
        }

        Rect textureRect = sprite.textureRect;
        target.texture = sprite.texture;
        target.uvRect = new Rect(
            textureRect.x / sprite.texture.width,
            textureRect.y / sprite.texture.height,
            textureRect.width / sprite.texture.width,
            textureRect.height / sprite.texture.height
        );
        target.enabled = true;
    }

    private static string FormatStat(float value)
    {
        return value.ToString("0.#");
    }

    public void OnEnemyPointerEntered(int enemyIndex)
    {
        ShowEnemyTooltip(enemyIndex);
    }

    public void OnEnemyPointerExited(int enemyIndex)
    {
        HideEnemyTooltip();
    }

    public void OnAllyPointerEntered(int allyIndex)
    {
        ShowAllyTooltip(allyIndex);
    }

    public void OnAllyPointerExited(int allyIndex)
    {
        HideEnemyTooltip();
    }
}
