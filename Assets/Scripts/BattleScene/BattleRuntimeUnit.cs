using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleRuntimeUnit : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLog = false;

    [Header("Visuals")]
    [SerializeField] private GameObject dotAlly;
    [SerializeField] private GameObject dotEnemy;
    [SerializeField] private GameObject dotDead;
    [SerializeField] private TMP_Text statusText;

    private RectTransform _rectTransform;
    private RectTransform _containerRectTransform;

    // 실제 전투 위치/배치/클램프는 Root 기준으로 처리한다.
    // 현재 스크립트는 Root의 자식 BattleRuntimeUnit에 붙어 있으므로,
    // 부모 Root의 RectTransform을 컨테이너로 잡는다.
    public RectTransform UiRectTransform => _containerRectTransform != null ? _containerRectTransform : _rectTransform;
    public GameObject RuntimeRootObject => UiRectTransform != null ? UiRectTransform.gameObject : gameObject;

    public int UnitNumber { get; private set; }
    public bool IsEnemy { get; private set; }
    public BattleUnitSnapshot Snapshot { get; private set; }

    public string DisplayName { get; private set; }
    public int Level { get; private set; }

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public float Attack { get; private set; }
    public float AttackSpeed { get; private set; }
    public float MoveSpeed { get; private set; }
    public float AttackRange { get; private set; }

    public bool IsCombatDisabled { get; private set; }
    public string CurrentAction { get; private set; }
    public BattleActionType CurrentActionType { get; private set; }
    public float KeepBehaving { get; private set; }
    public float ActionTimer { get; private set; }

    public float BodyRadius { get; private set; } = 50f;
    public BattleRuntimeUnit CurrentTarget { get; private set; }
    public float AttackCooldownRemaining { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsAttacking { get; private set; }

    public Vector2 AnchoredPosition => UiRectTransform != null ? UiRectTransform.anchoredPosition : Vector2.zero;

    // 나중에 "보정 전 값이 뭐였는지" 확인해야 할 가능성이 높아서 비교용으로 둘 다 유지함
    public BattleParameterSet CurrentRawParameters { get; private set; }
    public BattleParameterSet CurrentModifiedParameters { get; private set; }
    public BattleActionScoreSet CurrentScores { get; private set; }

    public BattleRuntimeUnit PlannedTargetEnemy { get; private set; }
    public BattleRuntimeUnit PlannedTargetAlly { get; private set; }
    public Vector2 PlannedDesiredPosition { get; private set; }
    public bool HasPlannedDesiredPosition { get; private set; }

    public BattleActionType TopScoredAction { get; private set; }
    public float TopScoredValue { get; private set; }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        if (_rectTransform == null)
        {
            Debug.LogError("[BattleRuntimeUnit] RectTransform is missing on BattleRuntimeUnit child.", this);
        }

        _containerRectTransform = transform.parent as RectTransform;

        if (_containerRectTransform == null)
        {
            // 혹시 구조가 바뀌어도 완전히 죽지 않게 fallback
            _containerRectTransform = _rectTransform;
            Debug.LogWarning("[BattleRuntimeUnit] Parent Root RectTransform not found. Falling back to self RectTransform.", this);
        }
    }

    public void Initialize(BattleUnitSnapshot snapshot, int unitNumber, bool isEnemy)
    {
        if (snapshot == null)
        {
            Debug.LogError("[BattleRuntimeUnit] Initialize received null snapshot.", this);
            return;
        }

        Snapshot = snapshot;
        UnitNumber = unitNumber;
        IsEnemy = isEnemy;

        DisplayName = snapshot.DisplayName;
        Level = snapshot.Level;

        MaxHealth = snapshot.MaxHealth;
        CurrentHealth = snapshot.CurrentHealth;
        Attack = snapshot.Attack;
        AttackSpeed = snapshot.AttackSpeed;
        MoveSpeed = snapshot.MoveSpeed;
        AttackRange = snapshot.AttackRange;

        IsCombatDisabled = false;
        CurrentAction = "Idle";
        CurrentActionType = BattleActionType.None;
        KeepBehaving = 0f;
        ActionTimer = 0f;

        BodyRadius = 50f;
        CurrentTarget = null;
        AttackCooldownRemaining = 0f;
        IsMoving = false;
        IsAttacking = false;

        CurrentRawParameters = default;
        CurrentModifiedParameters = default;
        CurrentScores = default;

        PlannedTargetEnemy = null;
        PlannedTargetAlly = null;
        PlannedDesiredPosition = Vector2.zero;
        HasPlannedDesiredPosition = false;

        TopScoredAction = BattleActionType.None;
        TopScoredValue = 0f;

        string runtimeName = $"{(isEnemy ? "Enemy" : "Ally")}_{UnitNumber}_{DisplayName}";
        if (RuntimeRootObject != null)
        {
            RuntimeRootObject.name = runtimeName;
        }

        RefreshVisualState();

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleRuntimeUnit] Initialized. UnitNumber={UnitNumber}, Name={DisplayName}, " +
                $"Team={(isEnemy ? "Enemy" : "Ally")}, HP={CurrentHealth:0.##}/{MaxHealth:0.##}",
                this
            );
        }
    }

    public void SetCurrentAction(string actionName)
    {
        CurrentAction = string.IsNullOrWhiteSpace(actionName) ? "Idle" : actionName;
        RefreshStatusText();
    }

    public void SetCurrentActionType(BattleActionType actionType, string displayName = null)
    {
        CurrentActionType = actionType;

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            CurrentAction = displayName;
        }
        else
        {
            CurrentAction = actionType == BattleActionType.None ? "Idle" : actionType.ToString();
        }

        RefreshStatusText();
    }

    public void SetDecisionState(float keepBehaving, float actionTimer)
    {
        KeepBehaving = keepBehaving;
        ActionTimer = Mathf.Max(0f, actionTimer);
    }

    public void SetBodyRadius(float bodyRadius)
    {
        BodyRadius = Mathf.Max(0f, bodyRadius);
    }

    public void SetCurrentTarget(BattleRuntimeUnit target)
    {
        CurrentTarget = target;
    }

    public void ClearCurrentTarget()
    {
        CurrentTarget = null;
    }

    public void SetCurrentParameters(BattleParameterSet rawParameters, BattleParameterSet modifiedParameters)
    {
        CurrentRawParameters = rawParameters;
        CurrentModifiedParameters = modifiedParameters;
    }

    public void SetCurrentScores(BattleActionScoreSet scores)
    {
        CurrentScores = scores;
        scores.GetBestAction(out BattleActionType bestAction, out float bestScore);
        TopScoredAction = bestAction;
        TopScoredValue = bestScore;
    }

    public void SetExecutionPlan(BattleActionExecutionPlan plan)
    {
        PlannedTargetEnemy = plan.TargetEnemy;
        PlannedTargetAlly = plan.TargetAlly;
        PlannedDesiredPosition = plan.DesiredPosition;
        HasPlannedDesiredPosition = plan.HasDesiredPosition;
        CurrentTarget = plan.TargetEnemy;
    }

    public void ClearExecutionPlan()
    {
        PlannedTargetEnemy = null;
        PlannedTargetAlly = null;
        PlannedDesiredPosition = Vector2.zero;
        HasPlannedDesiredPosition = false;
        CurrentTarget = null;
    }

    public void TickAttackCooldown(float deltaTime)
    {
        AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public void ClearAttackCooldown()
    {
        AttackCooldownRemaining = 0f;
    }

    public void ResetAttackCooldown()
    {
        float cooldown = AttackSpeed > 0f ? 1f / AttackSpeed : float.MaxValue;
        AttackCooldownRemaining = Mathf.Max(0f, cooldown);
    }

    public void SetMovementState(bool isMoving)
    {
        IsMoving = isMoving;

        if (isMoving)
        {
            IsAttacking = false;
        }
    }

    public void SetAttackState(bool isAttacking)
    {
        IsAttacking = isAttacking;

        if (isAttacking)
        {
            IsMoving = false;
        }
    }

    public void SetIdleState()
    {
        IsMoving = false;
        IsAttacking = false;
    }

    public void SetAnchoredPosition(Vector2 anchoredPosition)
    {
        if (UiRectTransform == null)
        {
            return;
        }

        UiRectTransform.anchoredPosition = anchoredPosition;
    }

    public void PlaceOnBattlefieldPlaceholder(RectTransform placeholder, RectTransform battlefieldRect)
    {
        if (UiRectTransform == null)
        {
            Debug.LogError("[BattleRuntimeUnit] Cannot place UI unit because Root RectTransform is missing.", this);
            return;
        }

        if (placeholder == null)
        {
            Debug.LogError("[BattleRuntimeUnit] placeholder is null.", this);
            return;
        }

        if (battlefieldRect == null)
        {
            Debug.LogError("[BattleRuntimeUnit] battlefieldRect is null.", this);
            return;
        }

        UiRectTransform.SetParent(battlefieldRect, false);

        Bounds placeholderBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(battlefieldRect, placeholder);

        UiRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        UiRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        UiRectTransform.pivot = new Vector2(0.5f, 0.5f);
        UiRectTransform.sizeDelta = placeholderBounds.size;
        UiRectTransform.anchoredPosition = placeholderBounds.center;
        UiRectTransform.localScale = Vector3.one;
        UiRectTransform.localRotation = Quaternion.identity;

        // 자식 BattleRuntimeUnit는 Root를 꽉 채우게 둔다.
        if (_rectTransform != null && _rectTransform != UiRectTransform)
        {
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;
            _rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    public void ClampInsideBattlefield(RectTransform battlefieldRect)
    {
        if (UiRectTransform == null || battlefieldRect == null)
        {
            return;
        }

        Vector2 pos = UiRectTransform.anchoredPosition;
        Rect fieldRect = battlefieldRect.rect;
        Rect unitRect = UiRectTransform.rect;

        float halfFieldWidth = fieldRect.width * 0.5f;
        float halfFieldHeight = fieldRect.height * 0.5f;
        float halfUnitWidth = Mathf.Max(unitRect.width * 0.5f, BodyRadius);
        float halfUnitHeight = Mathf.Max(unitRect.height * 0.5f, BodyRadius);

        pos.x = Mathf.Clamp(pos.x, -halfFieldWidth + halfUnitWidth, halfFieldWidth - halfUnitWidth);
        pos.y = Mathf.Clamp(pos.y, -halfFieldHeight + halfUnitHeight, halfFieldHeight - halfUnitHeight);

        UiRectTransform.anchoredPosition = pos;
    }

    public void ApplyDamage(float damage)
    {
        if (IsCombatDisabled)
        {
            return;
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));

        if (CurrentHealth <= 0f)
        {
            IsCombatDisabled = true;
            CurrentHealth = 0f;
            AttackCooldownRemaining = 0f;
            CurrentAction = "Disabled";
            CurrentActionType = BattleActionType.None;
            SetIdleState();
            ClearExecutionPlan();
            RefreshVisualState();
        }
    }

    private void RefreshVisualState()
    {
        bool isDead = IsCombatDisabled || CurrentHealth <= 0f;

        SetActive(dotAlly, !isDead && !IsEnemy);
        SetActive(dotEnemy, !isDead && IsEnemy);
        SetActive(dotDead, isDead);

        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        string actionLine = string.IsNullOrWhiteSpace(CurrentAction) ? "Idle" : CurrentAction;
        statusText.text = $"{UnitNumber}\n{actionLine}";
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }
}