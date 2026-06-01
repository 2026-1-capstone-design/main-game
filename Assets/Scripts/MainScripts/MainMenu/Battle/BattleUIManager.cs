using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleUIManager : MonoBehaviour
{
    private enum TooltipDetailTab
    {
        Personality,
        Weapon,
        WeaponSkill,
    }

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
    private RawImage enemyIcon1;

    [SerializeField]
    private RawImage enemyIcon2;

    [SerializeField]
    private RawImage enemyIcon3;

    [SerializeField]
    private RawImage enemyIcon4;

    [SerializeField]
    private RawImage enemyIcon5;

    [SerializeField]
    private RawImage enemyIcon6;

    [SerializeField]
    private GladiatorModelPreviewView[] enemyIconModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

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
    private RawImage[] allyImages = new RawImage[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private GladiatorModelPreviewView[] allyImageModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private TMP_Text[] allyNameTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] allyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private Image[] allyWeaponImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private WeaponModelPreviewView[] allyWeaponPreviewViews = new WeaponModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private Image[] allyArtifactImages = new Image[BattleTeamConstants.MaxUnitsPerTeam * 3];

    [Header("Enemy Tooltip")]
    [SerializeField]
    private RectTransform enemyTooltipRoot;

    [SerializeField]
    private Vector2 enemyTooltipOffset = new Vector2(16f, -8f);

    [SerializeField]
    private RawImage enemyTooltipIcon;

    [SerializeField]
    private GladiatorModelPreviewView enemyTooltipModelPreviewView;

    [SerializeField]
    private TMP_Text enemyTooltipPersonalityNameText;

    [SerializeField]
    private TMP_Text enemyTooltipPersonalityText;

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

    [Header("Enemy Tooltip Health Bar")]
    [SerializeField]
    private GameObject enemyTooltipHealthBarRoot;

    [SerializeField]
    private Image enemyTooltipHealthBarBlackBackground;

    [SerializeField]
    private Image enemyTooltipHealthBarRedFillImage;

    [SerializeField]
    private TMP_Text enemyTooltipHealthBarText;

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

    [Header("Enemy Tooltip Details")]
    [SerializeField]
    private Button enemyTooltipPersonalityDetailImage;

    [SerializeField]
    private Button enemyTooltipWeaponImageIcon;

    [SerializeField]
    private Button enemyTooltipWeaponSkillImageIcon;

    [SerializeField]
    private TMP_Text enemyTooltipSelectedTitleText;

    [SerializeField]
    private TMP_Text enemyTooltipSelectedDetailText;

    [Header("Preparation Buttons")]
    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button backButton;

    [Header("Pre Battle Panel")]
    [SerializeField]
    private GameObject preBattlePanelRoot;

    [SerializeField]
    private TMP_Text preBattleEnemyTeamNameText;

    [SerializeField]
    private TMP_Text preBattleAllyTeamNameText;

    [SerializeField]
    private TMP_Text preBattleMatchDayText;

    [SerializeField]
    private RawImage preBattleIconRawImage;

    [SerializeField]
    private string[] preBattleEnemyTeamNames = new string[0];

    [Header("Deployment Panel")]
    [SerializeField]
    private GameObject deploymentPanelRoot;

    [SerializeField]
    private RectTransform deploymentBoardArea;

    [SerializeField, Min(0.01f)]
    private float deploymentBattlefieldRadius = 28f;

    [SerializeField]
    private Button deploymentStartButton;

    [SerializeField]
    private Button deploymentBackButton;

    [SerializeField]
    private RawImage[] deployEnemyIcons = new RawImage[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private GladiatorModelPreviewView[] deployEnemyModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private Image[] deployEnemyMaskImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] deployEnemyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RawImage[] deployAllyIcons = new RawImage[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private GladiatorModelPreviewView[] deployAllyModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private Image[] deployAllyMaskImages = new Image[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private TMP_Text[] deployAllyLevelTexts = new TMP_Text[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RectTransform[] deploymentBoardEnemyViews = new RectTransform[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RawImage[] deploymentBoardEnemyIcons = new RawImage[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private GladiatorModelPreviewView[] deploymentBoardEnemyModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private DeploymentAttackRangeRing[] deploymentBoardEnemyAttackRangeRings = new DeploymentAttackRangeRing[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private RectTransform[] deploymentBoardAllyViews = new RectTransform[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private RawImage[] deploymentBoardAllyIcons = new RawImage[BattleTeamConstants.MaxUnitsPerTeam];

    [SerializeField]
    private GladiatorModelPreviewView[] deploymentBoardAllyModelPreviewViews = new GladiatorModelPreviewView[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

    [SerializeField]
    private DeploymentAttackRangeRing[] deploymentBoardAllyAttackRangeRings = new DeploymentAttackRangeRing[
        BattleTeamConstants.MaxUnitsPerTeam
    ];

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
    private readonly Vector3[] _deploymentBoardViewWorldCorners = new Vector3[4];
    private int _currentEncounterIndex = -1;
    private int _selectedAllyInfoTeamIndex;
    private int _deploymentSquadTeamIndex;
    private int _hoveredEnemyIndex = -1;
    private int _hoveredAllyIndex = -1;
    private int _draggingAllySlotIndex = -1;
    private bool _initialized;
    private bool _enemyTooltipVisible;
    private bool _loggedMissingEnemyTooltipRoot;
    private BattleUnitSnapshot _selectedTooltipUnit;
    private TooltipDetailTab _activeTooltipDetailTab;
    private ContentDatabaseProvider _contentDatabaseProvider;
    private Vector3 _enemyTooltipHealthBarRedFillBaseScale = Vector3.one;
    private bool _hasEnemyTooltipHealthBarRedFillBaseScale;

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
        CacheModelPreviewViews();
        BindEnemyHoverTargets();
        BindAllyHoverTargets();
        BindDeploymentAllyDragTargets();
        BindEnemyTooltipDetailButtons();
        ConfigureEnemyTooltipHealthBarFillImage();

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
        HidePreBattlePanel();
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
        HidePreBattlePanel();
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

    public string SelectEnemyTeamNameForBattle()
    {
        int nameCount = preBattleEnemyTeamNames != null ? preBattleEnemyTeamNames.Length : 0;
        if (nameCount == 0)
        {
            return "적 검투사단";
        }

        int startIndex = Random.Range(0, nameCount);
        for (int offset = 0; offset < nameCount; offset++)
        {
            string candidate = preBattleEnemyTeamNames[(startIndex + offset) % nameCount];
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return "적 검투사단";
    }

    public void ShowPreBattlePanel(BattleStartPayload payload)
    {
        SetActive(preBattlePanelRoot, true);
        if (preBattlePanelRoot != null)
        {
            preBattlePanelRoot.transform.SetAsLastSibling();
        }

        SetText(preBattleEnemyTeamNameText, payload != null ? payload.EnemyTeamName : string.Empty);
        SetText(preBattleAllyTeamNameText, payload != null ? payload.AllyTeamName : string.Empty);
        SetText(preBattleMatchDayText, payload != null ? $"DAY {payload.CurrentDay} MATCH" : string.Empty);
    }

    public void HidePreBattlePanel()
    {
        SetActive(preBattlePanelRoot, false);
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
        RawImage[] enemyIcons = GetEnemyIcons();
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
                SetUnitPreview(
                    enemyIcons[i],
                    GetArrayValue(enemyIconModelPreviewViews, i),
                    enemy,
                    hasEnemy ? enemy.PortraitSprite : null
                );
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
        RawImage[] enemyIcons = GetEnemyIcons();
        TMP_Text[] enemyNameTexts = GetEnemyNameTexts();
        TMP_Text[] enemyLevelTexts = GetEnemyLevelTexts();
        HideEnemyTooltip();

        for (int i = 0; i < enemyIcons.Length; i++)
        {
            _visibleEnemies[i] = null;

            if (enemyIcons[i] != null)
            {
                SetUnitPreview(enemyIcons[i], GetArrayValue(enemyIconModelPreviewViews, i), null, null);
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
            selectedSquadNameText.text =
                _squadManager != null ? _squadManager.GetTeamName(_selectedAllyInfoTeamIndex) : string.Empty;
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

            SetUnitPreview(
                GetArrayValue(allyImages, i),
                GetArrayValue(allyImageModelPreviewViews, i),
                snapshot,
                snapshot != null ? snapshot.PortraitSprite : null
            );
            SetText(GetArrayValue(allyNameTexts, i), snapshot != null ? snapshot.DisplayName : string.Empty);
            SetText(GetArrayValue(allyLevelTexts, i), snapshot != null ? $"Lv.{snapshot.Level}" : string.Empty);
            SetWeaponPreview(
                GetArrayValue(allyWeaponImages, i),
                GetArrayValue(allyWeaponPreviewViews, i),
                ally?.EquippedWeapon
            );
            SetAllyArtifactImages(i, ally);
        }
    }

    private void ClearAllySlots()
    {
        HideEnemyTooltip();
        for (int i = 0; i < _visibleAllies.Length; i++)
        {
            _visibleAllies[i] = null;
            SetUnitPreview(GetArrayValue(allyImages, i), GetArrayValue(allyImageModelPreviewViews, i), null, null);
            SetText(GetArrayValue(allyNameTexts, i), string.Empty);
            SetText(GetArrayValue(allyLevelTexts, i), string.Empty);
            SetWeaponPreview(GetArrayValue(allyWeaponImages, i), GetArrayValue(allyWeaponPreviewViews, i), null);
            SetAllyArtifactImages(i, null);
        }
    }

    private void SetAllyArtifactImages(int allyIndex, OwnedGladiatorData ally)
    {
        for (int slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            int imageIndex = (allyIndex * 3) + slotIndex;
            Sprite icon = ally?.GetEquippedArtifact(slotIndex)?.Artifact?.icon;
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

    private RawImage[] GetEnemyIcons()
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
        BindUnitHoverTargets(GetEnemyIcons(), isEnemy: true);
        BindUnitHoverTargets(deployEnemyIcons, isEnemy: true);
        BindUnitHoverTargets(deploymentBoardEnemyIcons, isEnemy: true);
    }

    private void BindAllyHoverTargets()
    {
        BindUnitHoverTargets(allyImages, isEnemy: false);
        BindUnitHoverTargets(deployAllyIcons, isEnemy: false);
        BindUnitHoverTargets(deploymentBoardAllyIcons, isEnemy: false);
    }

    private void BindUnitHoverTargets(RawImage[] targets, bool isEnemy)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            RawImage target = targets[i];
            if (target == null)
            {
                continue;
            }

            target.raycastTarget = true;
            BindUnitHoverTarget(target, i, isEnemy);
        }
    }

    private void BindUnitHoverTarget(RawImage target, int unitIndex, bool isEnemy)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.RemoveAll(entry =>
            entry.eventID == EventTriggerType.PointerEnter || entry.eventID == EventTriggerType.PointerExit
        );

        if (isEnemy)
        {
            AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnEnemyPointerEntered(unitIndex));
            AddEventTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnEnemyPointerExited(unitIndex));
        }
        else
        {
            AddEventTriggerEntry(trigger, EventTriggerType.PointerEnter, _ => OnAllyPointerEntered(unitIndex));
            AddEventTriggerEntry(trigger, EventTriggerType.PointerExit, _ => OnAllyPointerExited(unitIndex));
        }
    }

    private void BindDeploymentAllyDragTargets()
    {
        if (deploymentBoardAllyIcons == null)
        {
            return;
        }

        for (int i = 0; i < deploymentBoardAllyIcons.Length; i++)
        {
            RawImage allyIcon = deploymentBoardAllyIcons[i];
            RectTransform boardView = GetDeploymentBoardView(i, true);
            if (boardView != null)
            {
                Graphic[] graphics = boardView.GetComponentsInChildren<Graphic>(true);
                for (int j = 0; j < graphics.Length; j++)
                {
                    if (graphics[j] == null)
                    {
                        continue;
                    }

                    graphics[j].raycastTarget = true;
                    BindDeploymentAllyDragTarget(graphics[j], i);
                }
            }

            if (allyIcon != null)
            {
                allyIcon.raycastTarget = true;
                BindDeploymentAllyDragTarget(allyIcon, i);
            }
        }
    }

    private void BindDeploymentAllyDragTarget(Graphic dragTarget, int slotIndex)
    {
        EventTrigger trigger = dragTarget.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = dragTarget.gameObject.AddComponent<EventTrigger>();
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

        _selectedTooltipUnit = unit;

        if (enemyTooltipIcon != null)
        {
            SetUnitPreview(enemyTooltipIcon, enemyTooltipModelPreviewView, unit, unit.PortraitSprite);
        }

        SetText(enemyTooltipPersonalityNameText, BuildTooltipUnitNameText(unit));
        SetText(enemyTooltipPersonalityText, ResolvePersonalityName(unit));
        SetText(enemyTooltipLevelText, unit.Level.ToString());
        SetText(enemyTooltipAttackText, FormatStat(unit.Attack));
        SetText(enemyTooltipAttackSpeedText, FormatStat(unit.AttackSpeed));
        SetText(enemyTooltipMoveSpeedText, FormatStat(unit.MoveSpeed));
        SetText(enemyTooltipRangeText, FormatStat(unit.AttackRange));
        RefreshEnemyTooltipHealthBar(unit);
        RefreshEnemyTooltipDetailIcons(unit);
        RefreshEnemyTooltipSelectedDetail(_activeTooltipDetailTab);

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
        _selectedTooltipUnit = null;

        if (enemyTooltipRoot != null)
        {
            enemyTooltipRoot.gameObject.SetActive(false);
        }

        SetUnitPreview(enemyTooltipIcon, enemyTooltipModelPreviewView, null, null);
        SetText(enemyTooltipSelectedTitleText, string.Empty);
        SetText(enemyTooltipSelectedDetailText, string.Empty);
        RefreshEnemyTooltipHealthBar(null);
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
        else if (!IsPointerInsideEnemyTooltip())
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
        int hoveredIndex = GetHoveredUnitIndex(screenPosition, _visibleEnemies, GetEnemyIcons());
        if (hoveredIndex >= 0)
        {
            return hoveredIndex;
        }

        hoveredIndex = GetHoveredUnitIndex(screenPosition, _visibleEnemies, deployEnemyIcons);
        if (hoveredIndex >= 0)
        {
            return hoveredIndex;
        }

        return GetHoveredUnitIndex(screenPosition, _visibleEnemies, deploymentBoardEnemyIcons);
    }

    private int GetHoveredAllyIndex()
    {
        if (Mouse.current == null)
        {
            return -1;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        int hoveredIndex = GetHoveredUnitIndex(screenPosition, _visibleAllies, allyImages);
        if (hoveredIndex >= 0)
        {
            return hoveredIndex;
        }

        hoveredIndex = GetHoveredUnitIndex(screenPosition, _visibleAllies, deployAllyIcons);
        if (hoveredIndex >= 0)
        {
            return hoveredIndex;
        }

        return GetHoveredUnitIndex(screenPosition, _visibleAllies, deploymentBoardAllyIcons);
    }

    private static int GetHoveredUnitIndex(
        Vector2 screenPosition,
        BattleUnitSnapshot[] visibleUnits,
        RawImage[] targetImages
    )
    {
        if (visibleUnits == null || targetImages == null)
        {
            return -1;
        }

        int count = Mathf.Min(targetImages.Length, visibleUnits.Length);
        for (int i = 0; i < count; i++)
        {
            RawImage targetImage = targetImages[i];
            if (targetImage == null || !targetImage.gameObject.activeInHierarchy || visibleUnits[i] == null)
            {
                continue;
            }

            RectTransform rectTransform = targetImage.rectTransform;
            Canvas canvas = targetImage.GetComponentInParent<Canvas>();
            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsPointerInsideEnemyTooltip()
    {
        if (enemyTooltipRoot == null || Mouse.current == null || !enemyTooltipRoot.gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas canvas = enemyTooltipRoot.GetComponentInParent<Canvas>();
        Camera eventCamera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            enemyTooltipRoot,
            Mouse.current.position.ReadValue(),
            eventCamera
        );
    }

    private void RefreshEnemyTooltipHealthBar(BattleUnitSnapshot unit)
    {
        float currentHealth = unit != null ? Mathf.Max(0f, unit.CurrentHealth) : 0f;
        float maxHealth = unit != null ? Mathf.Max(0f, unit.MaxHealth, unit.CurrentHealth) : 0f;
        bool hasHealth = unit != null && maxHealth > 0f;

        SetActive(enemyTooltipHealthBarRoot, hasHealth);
        if (enemyTooltipHealthBarBlackBackground != null)
        {
            enemyTooltipHealthBarBlackBackground.enabled = hasHealth;
        }

        SetComponentActive(enemyTooltipHealthText, false);
        SetComponentActive(enemyTooltipHealthIcon, false);

        if (!hasHealth)
        {
            SetEnemyTooltipHealthRatio(0f);
            SetText(enemyTooltipHealthBarText, string.Empty);
            return;
        }

        SetEnemyTooltipHealthRatio(Mathf.Clamp01(currentHealth / maxHealth));
        SetText(enemyTooltipHealthBarText, $"{FormatHealth(currentHealth)}/{FormatHealth(maxHealth)}");
    }

    private void SetEnemyTooltipHealthRatio(float ratio)
    {
        if (enemyTooltipHealthBarRedFillImage == null)
        {
            return;
        }

        enemyTooltipHealthBarRedFillImage.enabled = ratio > 0f;
        enemyTooltipHealthBarRedFillImage.fillAmount = ratio;

        if (!_hasEnemyTooltipHealthBarRedFillBaseScale)
        {
            CacheEnemyTooltipHealthBarRedFillBaseScale();
        }

        Transform fillTransform = enemyTooltipHealthBarRedFillImage.transform;
        Vector3 scale = _enemyTooltipHealthBarRedFillBaseScale;
        scale.x *= ratio;
        fillTransform.localScale = scale;
    }

    private void ConfigureEnemyTooltipHealthBarFillImage()
    {
        if (enemyTooltipHealthBarRedFillImage == null)
        {
            return;
        }

        enemyTooltipHealthBarRedFillImage.type = Image.Type.Filled;
        enemyTooltipHealthBarRedFillImage.fillMethod = Image.FillMethod.Horizontal;
        enemyTooltipHealthBarRedFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        CacheEnemyTooltipHealthBarRedFillBaseScale();

        RectTransform fillRect = enemyTooltipHealthBarRedFillImage.rectTransform;
        if (fillRect != null)
        {
            Vector2 pivot = fillRect.pivot;
            pivot.x = 0f;
            fillRect.pivot = pivot;
        }
    }

    private void CacheEnemyTooltipHealthBarRedFillBaseScale()
    {
        if (enemyTooltipHealthBarRedFillImage == null)
        {
            _enemyTooltipHealthBarRedFillBaseScale = Vector3.one;
            _hasEnemyTooltipHealthBarRedFillBaseScale = false;
            return;
        }

        _enemyTooltipHealthBarRedFillBaseScale = enemyTooltipHealthBarRedFillImage.transform.localScale;
        _hasEnemyTooltipHealthBarRedFillBaseScale = true;
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

    private void BindEnemyTooltipDetailButtons()
    {
        BindButton(enemyTooltipPersonalityDetailImage, ShowEnemyTooltipPersonalityDetail);
        BindButton(enemyTooltipWeaponImageIcon, ShowEnemyTooltipWeaponDetail);
        BindButton(enemyTooltipWeaponSkillImageIcon, ShowEnemyTooltipWeaponSkillDetail);
    }

    private void ShowEnemyTooltipPersonalityDetail()
    {
        RefreshEnemyTooltipSelectedDetail(TooltipDetailTab.Personality);
    }

    private void ShowEnemyTooltipWeaponDetail()
    {
        RefreshEnemyTooltipSelectedDetail(TooltipDetailTab.Weapon);
    }

    private void ShowEnemyTooltipWeaponSkillDetail()
    {
        RefreshEnemyTooltipSelectedDetail(TooltipDetailTab.WeaponSkill);
    }

    private void RefreshEnemyTooltipDetailIcons(BattleUnitSnapshot unit)
    {
        SetButtonSprite(enemyTooltipWeaponImageIcon, unit != null ? unit.WeaponIconSprite : null);

        WeaponSkillSO skill = ResolveWeaponSkill(unit);
        SetButtonSprite(enemyTooltipWeaponSkillImageIcon, skill != null ? skill.icon : null);

        if (enemyTooltipPersonalityDetailImage != null)
        {
            enemyTooltipPersonalityDetailImage.interactable = unit != null && unit.Personality != null;
        }
    }

    private void RefreshEnemyTooltipSelectedDetail(TooltipDetailTab tab)
    {
        _activeTooltipDetailTab = tab;
        BattleUnitSnapshot unit = _selectedTooltipUnit;
        if (unit == null)
        {
            SetText(enemyTooltipSelectedTitleText, string.Empty);
            SetText(enemyTooltipSelectedDetailText, string.Empty);
            return;
        }

        switch (tab)
        {
            case TooltipDetailTab.Personality:
                SetText(enemyTooltipSelectedTitleText, ResolvePersonalityName(unit));
                SetText(enemyTooltipSelectedDetailText, ResolvePersonalityDetailText(unit.Personality));
                break;
            case TooltipDetailTab.Weapon:
                WeaponSO weapon = ResolveWeapon(unit);
                SetText(enemyTooltipSelectedTitleText, ResolveWeaponName(unit, weapon));
                SetText(enemyTooltipSelectedDetailText, BuildWeaponDetailText(weapon));
                break;
            case TooltipDetailTab.WeaponSkill:
                WeaponSkillSO skill = ResolveWeaponSkill(unit);
                SetText(enemyTooltipSelectedTitleText, skill != null ? skill.skillName : "스킬 없음");
                SetText(enemyTooltipSelectedDetailText, skill != null ? skill.description : string.Empty);
                break;
        }
    }

    private void SetTooltipStatIconsActive(bool value)
    {
        SetComponentActive(enemyTooltipLevelIcon, value);
        SetComponentActive(enemyTooltipAttackIcon, value);

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
        SetAllySquadButtonsActive(value);
        SetComponentActive(startButton, value);
        SetComponentActive(backButton, value);
    }

    private void SetAllySquadButtonsActive(bool value)
    {
        if (allySquadButtons == null)
        {
            return;
        }

        for (int i = 0; i < allySquadButtons.Length; i++)
        {
            SetComponentActive(allySquadButtons[i], value);
        }
    }

    private void RefreshDeploymentAllyList()
    {
        IReadOnlyList<OwnedGladiatorData> allies = GetSelectedDeploymentSquadGladiators();
        for (int i = 0; i < BattleTeamConstants.MaxUnitsPerTeam; i++)
        {
            OwnedGladiatorData ally = allies != null && i < allies.Count ? allies[i] : null;
            BattleUnitSnapshot allySnapshot =
                ally != null ? BattleUnitSnapshot.FromOwnedGladiator(ally, BattleTeamIds.Player) : null;
            _visibleAllies[i] = allySnapshot;
            SetUnitPreview(
                GetArrayValue(deployAllyIcons, i),
                GetArrayValue(deployAllyModelPreviewViews, i),
                allySnapshot,
                allySnapshot != null ? allySnapshot.PortraitSprite : null
            );
            SetComponentActive(GetArrayValue(deployAllyMaskImages, i), ally != null);
            SetText(GetArrayValue(deployAllyLevelTexts, i), ally != null ? $"Lv.{ally.Level}" : string.Empty);
            SetUnitPreview(
                GetArrayValue(deploymentBoardAllyIcons, i),
                GetArrayValue(deploymentBoardAllyModelPreviewViews, i),
                allySnapshot,
                allySnapshot != null ? allySnapshot.PortraitSprite : null
            );
            SetDeploymentBoardViewActive(i, true, ally != null);
            RefreshDeploymentBoardAttackRange(i, true, allySnapshot);
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
            _visibleEnemies[i] = enemy;
            SetUnitPreview(
                GetArrayValue(deployEnemyIcons, i),
                GetArrayValue(deployEnemyModelPreviewViews, i),
                enemy,
                enemy != null ? enemy.PortraitSprite : null
            );
            SetComponentActive(GetArrayValue(deployEnemyMaskImages, i), enemy != null);
            SetText(GetArrayValue(deployEnemyLevelTexts, i), enemy != null ? $"Lv.{enemy.Level}" : string.Empty);
            SetUnitPreview(
                GetArrayValue(deploymentBoardEnemyIcons, i),
                GetArrayValue(deploymentBoardEnemyModelPreviewViews, i),
                enemy,
                enemy != null ? enemy.PortraitSprite : null
            );
            SetDeploymentBoardViewActive(i, false, enemy != null);
            RefreshDeploymentBoardAttackRange(i, false, enemy);
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

    private void RefreshDeploymentBoardAttackRange(int slotIndex, bool isPlayerTeam, BattleUnitSnapshot unit)
    {
        DeploymentAttackRangeRing attackRangeRing = GetArrayValue(
            isPlayerTeam ? deploymentBoardAllyAttackRangeRings : deploymentBoardEnemyAttackRangeRings,
            slotIndex
        );
        if (attackRangeRing == null)
        {
            return;
        }

        bool visible = unit != null && deploymentBoardArea != null && unit.AttackRange > 0f;
        attackRangeRing.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        attackRangeRing.raycastTarget = false;
        Rect boardRect = deploymentBoardArea.rect;
        float battlefieldRadius = Mathf.Max(0.01f, deploymentBattlefieldRadius);
        float displayedAttackRange = Mathf.Max(2f, unit.AttackRange);
        float normalizedRadius = displayedAttackRange / battlefieldRadius;
        RectTransform rangeRect = attackRangeRing.rectTransform;
        rangeRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, boardRect.width * normalizedRadius);
        rangeRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, boardRect.height * normalizedRadius);
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
        view.position = GetPivotPositionForRectCenter(view, worldPoint);
    }

    private Vector3 GetPivotPositionForRectCenter(RectTransform rectTransform, Vector3 targetCenterWorldPosition)
    {
        if (rectTransform == null)
        {
            return targetCenterWorldPosition;
        }

        rectTransform.GetWorldCorners(_deploymentBoardViewWorldCorners);
        Vector3 currentCenter = (_deploymentBoardViewWorldCorners[0] + _deploymentBoardViewWorldCorners[2]) * 0.5f;
        Vector3 pivotToCenter = currentCenter - rectTransform.position;
        return targetCenterWorldPosition - pivotToCenter;
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

        RawImage fallbackImage = GetArrayValue(
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
                _isAllyBoardIconPlacedBySlot[i] = true;
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
                i < encounter.EnemyDeploymentNormalizedPositions.Count
                    ? encounter.EnemyDeploymentNormalizedPositions[i]
                    : BattleDeploymentPositionUtility.BuildDefaultPosition(i, count, false);
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
            SetUnitPreview(
                GetArrayValue(deploymentBoardAllyIcons, i),
                GetArrayValue(deploymentBoardAllyModelPreviewViews, i),
                null,
                null
            );
            SetDeploymentBoardViewActive(i, true, false);
            RefreshDeploymentBoardAttackRange(i, true, null);
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
            SetUnitPreview(
                GetArrayValue(deploymentBoardEnemyIcons, i),
                GetArrayValue(deploymentBoardEnemyModelPreviewViews, i),
                null,
                null
            );
            SetDeploymentBoardViewActive(i, false, false);
            RefreshDeploymentBoardAttackRange(i, false, null);
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

    private void CacheModelPreviewViews()
    {
        RawImage[] enemyIcons = GetEnemyIcons();
        CacheModelPreviewViewArray(enemyIcons, ref enemyIconModelPreviewViews);
        CacheModelPreviewViewArray(allyImages, ref allyImageModelPreviewViews);
        CacheModelPreviewViewArray(deployEnemyIcons, ref deployEnemyModelPreviewViews);
        CacheModelPreviewViewArray(deployAllyIcons, ref deployAllyModelPreviewViews);
        CacheModelPreviewViewArray(deploymentBoardEnemyIcons, ref deploymentBoardEnemyModelPreviewViews);
        CacheModelPreviewViewArray(deploymentBoardAllyIcons, ref deploymentBoardAllyModelPreviewViews);
        CacheImageWeaponPreviewViewArray(allyWeaponImages, ref allyWeaponPreviewViews);

        if (enemyTooltipModelPreviewView == null && enemyTooltipIcon != null)
        {
            enemyTooltipModelPreviewView = enemyTooltipIcon.GetComponentInChildren<GladiatorModelPreviewView>(true);
        }
    }

    private static void CacheModelPreviewViewArray(RawImage[] images, ref GladiatorModelPreviewView[] previews)
    {
        if (images == null)
        {
            return;
        }

        if (previews == null || previews.Length != images.Length)
        {
            System.Array.Resize(ref previews, images.Length);
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (previews[i] == null && images[i] != null)
            {
                previews[i] = images[i].GetComponentInChildren<GladiatorModelPreviewView>(true);
            }
        }
    }

    private static void CacheImageWeaponPreviewViewArray(Image[] images, ref WeaponModelPreviewView[] previews)
    {
        if (images == null)
        {
            return;
        }

        if (previews == null || previews.Length != images.Length)
        {
            System.Array.Resize(ref previews, images.Length);
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (previews[i] == null && images[i] != null)
            {
                previews[i] = images[i].GetComponentInChildren<WeaponModelPreviewView>(true);
            }
        }
    }

    private static void SetUnitPreview(
        RawImage fallbackImage,
        GladiatorModelPreviewView modelPreviewView,
        BattleUnitSnapshot unit,
        Sprite fallbackSprite
    )
    {
        GameObject modelPrefab =
            unit != null && unit.GladiatorClass != null ? unit.GladiatorClass.previewModelPrefab : null;
        bool useModelPreview = modelPreviewView != null && modelPrefab != null;

        if (modelPreviewView != null)
        {
            if (useModelPreview)
            {
                modelPreviewView.Show(
                    modelPrefab,
                    unit.CustomizeIndicates,
                    unit.LeftWeaponPrefab,
                    unit.RightWeaponPrefab
                );
            }
            else
            {
                modelPreviewView.Clear();
            }
        }

        if (fallbackImage == null)
        {
            return;
        }

        if (useModelPreview)
        {
            if (!modelPreviewView.UsesTargetImage(fallbackImage))
            {
                SetRawImage(fallbackImage, null);
            }

            return;
        }

        SetRawImage(fallbackImage, fallbackSprite);
    }

    private static void SetWeaponPreview(
        Image fallbackImage,
        WeaponModelPreviewView modelPreviewView,
        OwnedWeaponData weapon
    )
    {
        if (modelPreviewView != null)
        {
            modelPreviewView.Clear();
        }

        SetImage(fallbackImage, weapon?.Weapon?.icon);
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

    private WeaponSO ResolveWeapon(BattleUnitSnapshot unit)
    {
        if (unit == null || string.IsNullOrWhiteSpace(unit.WeaponName))
        {
            return null;
        }

        ContentDatabaseProvider provider = ResolveContentDatabaseProvider();
        IReadOnlyList<WeaponSO> weapons = provider != null ? provider.Weapons : null;
        if (weapons == null)
        {
            return null;
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSO weapon = weapons[i];
            if (weapon != null && weapon.weaponName == unit.WeaponName)
            {
                return weapon;
            }
        }

        return null;
    }

    private WeaponSkillSO ResolveWeaponSkill(BattleUnitSnapshot unit)
    {
        if (unit == null || unit.WeaponSkillId == WeaponSkillId.None)
        {
            return null;
        }

        ContentDatabaseProvider provider = ResolveContentDatabaseProvider();
        IReadOnlyList<WeaponSkillSO> skills = provider != null ? provider.WeaponSkills : null;
        if (skills == null)
        {
            return null;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            WeaponSkillSO skill = skills[i];
            if (skill != null && skill.skillId == unit.WeaponSkillId)
            {
                return skill;
            }
        }

        return null;
    }

    private ContentDatabaseProvider ResolveContentDatabaseProvider()
    {
        if (_contentDatabaseProvider == null)
        {
            _contentDatabaseProvider = ContentDatabaseProvider.Instance;
        }

        return _contentDatabaseProvider;
    }

    private static void SetButtonSprite(Button button, Sprite sprite)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
        {
            image = button.targetGraphic as Image;
        }

        if (image == null)
        {
            button.interactable = sprite != null;
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
        button.interactable = sprite != null;
    }

    private static string BuildTooltipUnitNameText(BattleUnitSnapshot unit)
    {
        if (unit == null)
        {
            return string.Empty;
        }

        const string nameColor = "#FFFFFF";
        return $"<color={nameColor}>{unit.DisplayName}</color>";
    }

    private static string ResolvePersonalityName(BattleUnitSnapshot unit)
    {
        return unit != null && unit.Personality != null && !string.IsNullOrWhiteSpace(unit.Personality.personalityName)
            ? unit.Personality.personalityName
            : "성격 없음";
    }

    private static string ResolvePersonalityDetailText(PersonalitySO personality)
    {
        if (personality == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(personality.dialogPersonalityDescription))
        {
            return personality.dialogPersonalityDescription;
        }

        if (!string.IsNullOrWhiteSpace(personality.detailText))
        {
            return personality.detailText;
        }

        return string.IsNullOrWhiteSpace(personality.description) ? string.Empty : personality.description;
    }

    private static string ResolveWeaponName(BattleUnitSnapshot unit, WeaponSO weapon)
    {
        if (weapon != null && !string.IsNullOrWhiteSpace(weapon.weaponName))
        {
            return weapon.weaponName;
        }

        return unit != null && !string.IsNullOrWhiteSpace(unit.WeaponName) ? unit.WeaponName : "무기 없음";
    }

    private static string BuildWeaponDetailText(WeaponSO weapon)
    {
        if (weapon == null)
        {
            return string.Empty;
        }

        return $"체력 : {FormatSignedStat(weapon.baseHealthBonus)}\n"
            + $"공격력 : {FormatSignedStat(weapon.baseAttackBonus)}\n"
            + $"공격속도 : {FormatSignedStat(weapon.baseAttackSpeedBonus)}\n"
            + $"이동속도 : {FormatSignedStat(weapon.baseMoveSpeedBonus)}\n"
            + $"사거리 : {FormatSignedStat(weapon.baseAttackRangeBonus)}";
    }

    private static string FormatSignedStat(float value)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return "0";
        }

        return value > 0f ? $"+{value:0.#}" : value.ToString("0.#");
    }

    private static string FormatStat(float value)
    {
        return value.ToString("0.#");
    }

    private static string FormatHealth(float value)
    {
        return Mathf.RoundToInt(value).ToString();
    }

    public void OnEnemyPointerEntered(int enemyIndex)
    {
        ShowEnemyTooltip(enemyIndex);
    }

    public void OnEnemyPointerExited(int enemyIndex)
    {
        if (!IsPointerInsideEnemyTooltip())
        {
            HideEnemyTooltip();
        }
    }

    public void OnAllyPointerEntered(int allyIndex)
    {
        ShowAllyTooltip(allyIndex);
    }

    public void OnAllyPointerExited(int allyIndex)
    {
        if (!IsPointerInsideEnemyTooltip())
        {
            HideEnemyTooltip();
        }
    }
}
