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
    private TMP_Text enemyLevelText1;

    [SerializeField]
    private TMP_Text enemyLevelText2;

    [SerializeField]
    private TMP_Text enemyLevelText3;

    [SerializeField]
    private TMP_Text enemyLevelText4;

    [SerializeField]
    private TMP_Text enemyLevelText5;

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

    [SerializeField]
    private Button easierDifficultyButton;

    [SerializeField]
    private Button harderDifficultyButton;

    [Header("Preparation Buttons")]
    [SerializeField]
    private Button startButton;

    [SerializeField]
    private Button backButton;

    private MainFlowManager _flow;
    private BattleManager _battleManager;
    private IReadOnlyList<BattleEncounterPreview> _currentEncounters;
    private readonly BattleUnitSnapshot[] _visibleEnemies = new BattleUnitSnapshot[5];
    private int _currentEncounterIndex = -1;
    private int _hoveredEnemyIndex = -1;
    private bool _initialized;
    private bool _enemyTooltipVisible;
    private bool _loggedMissingEnemyTooltipRoot;

    public void Initialize(MainFlowManager flow, BattleManager battleManager)
    {
        if (_initialized)
        {
            return;
        }

        _flow = flow;
        _battleManager = battleManager;

        BindButton(easierDifficultyButton, OnEasierDifficultyClicked);
        BindButton(harderDifficultyButton, OnHarderDifficultyClicked);
        BindButton(startButton, OnStartClicked);
        BindButton(backButton, OnBackClicked);
        BindEnemyHoverTargets();

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
        HideEnemyTooltip();
        ClearEnemySlots();

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
        RefreshDifficultyButtons();

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

    private void Update()
    {
        RefreshEnemyTooltipHoverFallback();

        if (_enemyTooltipVisible)
        {
            UpdateEnemyTooltipPosition();
        }
    }

    private void RefreshDifficultyButtons()
    {
        int orderIndex = GetCurrentDifficultyOrderIndex();

        if (easierDifficultyButton != null)
        {
            easierDifficultyButton.interactable = FindAvailableEncounterInDirection(orderIndex, -1) >= 0;
        }

        if (harderDifficultyButton != null)
        {
            harderDifficultyButton.interactable = FindAvailableEncounterInDirection(orderIndex, 1) >= 0;
        }
    }

    private void OnEasierDifficultyClicked()
    {
        int nextIndex = FindAvailableEncounterInDirection(GetCurrentDifficultyOrderIndex(), -1);
        if (nextIndex >= 0)
        {
            SetCurrentEncounter(nextIndex, true);
        }
    }

    private void OnHarderDifficultyClicked()
    {
        int nextIndex = FindAvailableEncounterInDirection(GetCurrentDifficultyOrderIndex(), 1);
        if (nextIndex >= 0)
        {
            SetCurrentEncounter(nextIndex, true);
        }
    }

    private int FindAvailableEncounterInDirection(int orderIndex, int direction)
    {
        if (orderIndex < 0 || direction == 0)
        {
            return -1;
        }

        for (int i = orderIndex + direction; i >= 0 && i < DifficultyOrder.Length; i += direction)
        {
            int encounterIndex = FindEncounterIndexByDifficulty(DifficultyOrder[i]);
            if (encounterIndex >= 0)
            {
                return encounterIndex;
            }
        }

        return -1;
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
        return new[] { enemyIcon1, enemyIcon2, enemyIcon3, enemyIcon4, enemyIcon5 };
    }

    private TMP_Text[] GetEnemyNameTexts()
    {
        return new[] { enemyNameText1, enemyNameText2, enemyNameText3, enemyNameText4, enemyNameText5 };
    }

    private TMP_Text[] GetEnemyLevelTexts()
    {
        return new[] { enemyLevelText1, enemyLevelText2, enemyLevelText3, enemyLevelText4, enemyLevelText5 };
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

    private void ShowEnemyTooltip(int enemyIndex)
    {
        if (enemyIndex < 0 || enemyIndex >= _visibleEnemies.Length)
        {
            return;
        }

        BattleUnitSnapshot enemy = _visibleEnemies[enemyIndex];
        if (enemy == null)
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

        Debug.Log($"Enemy hover 감지됨: {enemy.DisplayName}");

        if (enemyTooltipIcon != null)
        {
            enemyTooltipIcon.sprite = enemy.PortraitSprite;
            enemyTooltipIcon.enabled = enemy.PortraitSprite != null;
        }

        SetText(enemyTooltipLevelText, enemy.Level.ToString());
        SetText(enemyTooltipAttackText, FormatStat(enemy.Attack));
        SetText(enemyTooltipHealthText, FormatStat(enemy.MaxHealth));
        SetText(enemyTooltipAttackSpeedText, FormatStat(enemy.AttackSpeed));
        SetText(enemyTooltipMoveSpeedText, FormatStat(enemy.MoveSpeed));
        SetText(enemyTooltipRangeText, FormatStat(enemy.AttackRange));

        SetTooltipStatIconsActive(true);
        enemyTooltipRoot.gameObject.SetActive(true);
        enemyTooltipRoot.SetAsLastSibling();
        _hoveredEnemyIndex = enemyIndex;
        _enemyTooltipVisible = true;
        UpdateEnemyTooltipPosition();
    }

    private void HideEnemyTooltip()
    {
        _enemyTooltipVisible = false;
        _hoveredEnemyIndex = -1;

        if (enemyTooltipRoot != null)
        {
            enemyTooltipRoot.gameObject.SetActive(false);
        }
    }

    private void RefreshEnemyTooltipHoverFallback()
    {
        int enemyIndex = GetHoveredEnemyIndex();
        if (enemyIndex == _hoveredEnemyIndex)
        {
            return;
        }

        if (enemyIndex >= 0)
        {
            ShowEnemyTooltip(enemyIndex);
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
        if (_flow != null)
        {
            _flow.HandleBattleStartRequested();
        }
    }

    private void OnBackClicked()
    {
        if (_flow != null)
        {
            _flow.HandleBattlePreparationBackRequested();
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
}
