using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Image HPbar;
    [SerializeField] private Sprite AllybarSprite;
    [SerializeField] private Sprite EnemybarSprite;

    public GameObject RuntimeRootObject => gameObject;

    public int UnitNumber { get; private set; }
    public bool IsEnemy { get; private set; }
    public BattleUnitSnapshot Snapshot { get; private set; }

    public string DisplayName { get; private set; }
    public int Level { get; private set; }

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }

    public float BaseAttack;
    public float BaseAttackSpeed;
    public float BaseMoveSpeed;
    public float BaseAttackRange;

    public float Attack => Mathf.Max(0f, BaseAttack + GetBuffLevel(BuffType.AttackDamage) * 10f);
    public float AttackSpeed => Mathf.Max(0f, BaseAttackSpeed + GetBuffLevel(BuffType.AttackSpeed) * 0.2f);
    public float MoveSpeed => Mathf.Max(0f, BaseMoveSpeed + GetBuffLevel(BuffType.MoveSpeed) * 0.5f);
    public float AttackRange => Mathf.Max(0f, BaseAttackRange + GetBuffLevel(BuffType.AttackRange) * 0.5f);

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

    public Vector3 Position => transform.position;

    [field: SerializeField] public BattleParameterSet CurrentRawParameters { get; private set; }
    [field: SerializeField] public BattleParameterSet CurrentModifiedParameters { get; private set; }
    [field: SerializeField] public BattleActionScoreSet CurrentScores { get; private set; }

    public BattleRuntimeUnit PlannedTargetEnemy { get; private set; }
    public BattleRuntimeUnit PlannedTargetAlly { get; private set; }
    public Vector3 PlannedDesiredPosition { get; private set; }
    public bool HasPlannedDesiredPosition { get; private set; }

    [field: SerializeField] public BattleActionType TopScoredAction { get; private set; }
    [field: SerializeField] public float TopScoredValue { get; private set; }

    // ── TacticalIntent 오버라이드 상태 ──────────────────────
    /// <summary>현재 Intent 오버라이드가 활성화된 상태인지.</summary>
    public bool HasIntentOverride { get; private set; }
    public BattleActionType IntentOverrideActionType { get; private set; }
    public BattleRuntimeUnit IntentOverrideTarget { get; private set; }
    public SkillUsagePolicy IntentSkillPolicy { get; private set; }
    public PositioningStyle IntentPositioning { get; private set; }
    // ────────────────────────────────────────────────────────

    [Header("Weapon Sockets")]
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private GameObject _spawnedLeftWeapon;
    [SerializeField] private GameObject _spawnedRightWeapon;
    [SerializeField] private Animator _myAnimation;

    [Header("Weapon Skill")]
    [SerializeField] private WeaponSkillId HaveSkill;
    [SerializeField] private float skillCooltime;
    [SerializeField] private skillType _skillType;
    public float SkillCooldownRemaining { get; private set; }

    [Header("Buff")]
    [SerializeField] List<BuffType> Buffs = new List<BuffType>();
    [SerializeField] List<float> BuffCooldownRemaining = new List<float>();
    [SerializeField] List<int> BuffLevel = new List<int>();

    [Header("special Effect")]
    public Vector3 CurrentKnockback { get; private set; }

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
        BaseAttack = snapshot.Attack;
        BaseAttackSpeed = snapshot.AttackSpeed;
        BaseMoveSpeed = snapshot.MoveSpeed;
        BaseAttackRange = snapshot.AttackRange;

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
        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;

        TopScoredAction = BattleActionType.None;
        TopScoredValue = 0f;

        // Intent 오버라이드 초기화
        ClearIntentOverride();

        _myAnimation = this.transform.GetComponent<Animator>();
        EquipWeaponFromSnapShot();
        EquipSkillFromSnapShot();

        if (isEnemy)
            HPbar.sprite = EnemybarSprite;
        else
            HPbar.sprite = AllybarSprite;

        RefreshHPbar();

        string runtimeName = $"{(isEnemy ? "Enemy" : "Ally")}_{UnitNumber}_{DisplayName}";
        if (RuntimeRootObject != null)
            RuntimeRootObject.name = runtimeName;

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

    // ── TacticalIntent 오버라이드 API ────────────────────────

    /// <summary>
    /// TacticalIntentExecutor가 매 틱 호출.
    /// 이 유닛의 ActionType과 타겟을 Intent 기반으로 강제 지정.
    /// BattleSimulationManager의 CommitOrSwitchActions()보다 나중에 적용.
    /// </summary>
    public void SetIntentOverride(
        BattleActionType actionType,
        BattleRuntimeUnit targetEnemy,
        SkillUsagePolicy skillPolicy,
        PositioningStyle positioning)
    {
        HasIntentOverride = true;
        IntentOverrideActionType = actionType;
        IntentOverrideTarget = targetEnemy;
        IntentSkillPolicy = skillPolicy;
        IntentPositioning = positioning;

        // 즉시 ActionType에도 반영 (StatusText 등 UI 갱신 포함)
        SetCurrentActionType(actionType, $"[Intent]{actionType}");
    }

    /// <summary>Intent 오버라이드 해제 → AI 자율 행동으로 복귀.</summary>
    public void ClearIntentOverride()
    {
        HasIntentOverride = false;
        IntentOverrideActionType = BattleActionType.None;
        IntentOverrideTarget = null;
        IntentSkillPolicy = SkillUsagePolicy.OnCooldown;
        IntentPositioning = PositioningStyle.CloseQuarter;
    }

    // ────────────────────────────────────────────────────────

    private void EquipWeaponFromSnapShot()
    {
        if (Snapshot == null)
            return;

        if (Snapshot.LeftWeaponPrefab != null && leftHandSocket != null)
        {
            Debug.Log("왼손 무기 장착");
            _spawnedLeftWeapon = Instantiate(Snapshot.LeftWeaponPrefab, leftHandSocket);
            _spawnedLeftWeapon.transform.localPosition = Vector3.zero;
            _spawnedLeftWeapon.transform.localRotation = Quaternion.identity;
        }
        if (Snapshot.RightWeaponPrefab != null && rightHandSocket != null)
        {
            Debug.Log("오른손 무기 장착");
            _spawnedRightWeapon = Instantiate(Snapshot.RightWeaponPrefab, rightHandSocket);
            _spawnedRightWeapon.transform.localPosition = Vector3.zero;
            _spawnedRightWeapon.transform.localRotation = Quaternion.identity;
        }

        if (_myAnimation != null && AnimationManager.Instance != null)
        {
            AnimatorOverrideController weaponMotion = AnimationManager.Instance.GetControllerByWeaponType(Snapshot.WeaponType);
            if (weaponMotion != null)
                _myAnimation.runtimeAnimatorController = weaponMotion;
        }
    }

    void EquipSkillFromSnapShot()
    {
        if (Snapshot == null)
            return;

        HaveSkill = Snapshot.WeaponSkillId;
        AnimationClip skill_animation = AnimationManager.Instance.getAnimation(HaveSkill);
        skillCooltime = AnimationManager.Instance.getCooltime(HaveSkill);
        _skillType = AnimationManager.Instance.getSkillType(HaveSkill);

        RuntimeAnimatorController current = _myAnimation.runtimeAnimatorController;
        AnimatorOverrideController local = new AnimatorOverrideController(current);
        local["HumanM@MiningOneHand01_L - Ground"] = skill_animation;
        _myAnimation.runtimeAnimatorController = local;
    }

    public skillType getSkillType() => _skillType;
    public WeaponSkillId getSkill() => HaveSkill;

    public void SetAnimationSpeed(float speedMultiplier)
    {
        if (_myAnimation != null)
            _myAnimation.speed = speedMultiplier;
    }

    public void SetCurrentAction(string actionName)
    {
        CurrentAction = string.IsNullOrWhiteSpace(actionName) ? "Idle" : actionName;
        RefreshStatusText();
    }

    public void SetCurrentActionType(BattleActionType actionType, string displayName = null)
    {
        CurrentActionType = actionType;
        CurrentAction = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : (actionType == BattleActionType.None ? "Idle" : actionType.ToString());
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

    public void SetCurrentTarget(BattleRuntimeUnit target) => CurrentTarget = target;
    public void ClearCurrentTarget() => CurrentTarget = null;

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
        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;
        CurrentTarget = null;
    }

    public void TickAttackCooldown(float deltaTime)
    {
        AttackCooldownRemaining = Mathf.Max(0f, AttackCooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public void ClearAttackCooldown() => AttackCooldownRemaining = 0f;

    public void ResetAttackCooldown()
    {
        float cooldown = AttackSpeed > 0f ? 1f / AttackSpeed : float.MaxValue;
        AttackCooldownRemaining = Mathf.Max(0f, cooldown);
    }

    public void TickSkillCooldown(float deltaTime)
    {
        SkillCooldownRemaining = Mathf.Max(0f, SkillCooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    public void ClearSkillCooldown() => SkillCooldownRemaining = 0f;

    public void ResetSkillCooldown()
    {
        SkillCooldownRemaining = Mathf.Max(0f, skillCooltime);
    }

    public void TickBufflCooldown(float deltaTime)
    {
        for (int i = Buffs.Count - 1; i >= 0; i--)
        {
            BuffCooldownRemaining[i] = Mathf.Max(0f, BuffCooldownRemaining[i] - Mathf.Max(0f, deltaTime));
            if (BuffCooldownRemaining[i] <= 0f)
            {
                Buffs.RemoveAt(i);
                BuffLevel.RemoveAt(i);
                BuffCooldownRemaining.RemoveAt(i);
            }
        }
    }

    public void BuffApply(BuffType type, int level, float cool)
    {
        Buffs.Add(type);
        BuffLevel.Add(level);
        BuffCooldownRemaining.Add(cool);
    }

    public int BuffNum() => Buffs.Count;

    public int GetBuffLevel(BuffType type)
    {
        int count = 0;
        for (int i = 0; i < Buffs.Count; i++)
            if (Buffs[i] == type)
                count++;
        return count;
    }

    public void AddKnockback(Vector3 forceDirection, float forcePower)
    {
        Vector3 force = forceDirection.normalized * forcePower;
        force.y = 0f;
        CurrentKnockback += force;
    }

    public void TickKnockback(float deltaTime, float friction = 10f)
    {
        if (CurrentKnockback.sqrMagnitude > 0.01f)
        {
            SetPosition(Position + CurrentKnockback * deltaTime);
            CurrentKnockback = Vector3.Lerp(CurrentKnockback, Vector3.zero, friction * deltaTime);
        }
        else
        {
            CurrentKnockback = Vector3.zero;
        }
    }

    public void SetMovementState(bool isMoving)
    {
        IsMoving = isMoving;
        if (isMoving)
            IsAttacking = false;
        if (_myAnimation != null)
            _myAnimation.SetBool("isMoving", IsMoving);
    }

    public void SetAttackState(bool isAttacking)
    {
        if (isAttacking && !IsAttacking)
        {
            if (_myAnimation != null)
                _myAnimation.SetTrigger("attack");

            BattleRuntimeUnit faceTarget = PlannedTargetEnemy ?? CurrentTarget;
            if (faceTarget != null)
                FaceTarget(faceTarget.Position);
        }

        IsAttacking = isAttacking;

        if (isAttacking)
        {
            IsMoving = false;
            if (_myAnimation != null)
                _myAnimation.SetBool("isMoving", false);
        }
    }

    public void SetSkillState()
    {
        _myAnimation.SetTrigger("skill");
        BattleRuntimeUnit faceTarget = PlannedTargetEnemy ?? CurrentTarget;
        if (faceTarget != null)
            FaceTarget(faceTarget.Position);
    }

    public void SetIdleState()
    {
        if (_myAnimation != null)
            _myAnimation.SetBool("isMoving", false);
        IsMoving = false;
        IsAttacking = false;
    }

    public void SetPosition(Vector3 newPosition) => transform.position = newPosition;

    public void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    public void PlaceOnBattlefieldPlaceholder(Transform placeholder, Transform battlefield)
    {
        if (placeholder == null)
            return;
        if (battlefield != null)
            transform.SetParent(battlefield, false);
        transform.position = placeholder.position;
        transform.rotation = placeholder.rotation;
    }

    public void ClampInsideBattlefield(BoxCollider battlefieldCollider)
    {
        if (battlefieldCollider == null)
            return;
        Vector3 pos = transform.position;
        Bounds bounds = battlefieldCollider.bounds;

        pos.x = Mathf.Clamp(pos.x, bounds.min.x + BodyRadius, bounds.max.x - BodyRadius);
        pos.z = Mathf.Clamp(pos.z, bounds.min.z + BodyRadius, bounds.max.z - BodyRadius);
        transform.position = pos;
    }

    public void ApplyDamage(float damage)
    {
        if (IsCombatDisabled)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));
        RefreshHPbar();

        if (CurrentHealth <= 0f)
        {
            IsCombatDisabled = true;
            CurrentHealth = 0f;
            AttackCooldownRemaining = 0f;
            CurrentAction = "Disabled";
            CurrentActionType = BattleActionType.None;

            ClearIntentOverride(); // 사망 시 Intent 오버라이드도 제거

            if (_myAnimation != null)
            {
                _myAnimation.SetBool("isMoving", false);
                _myAnimation.SetTrigger("die");
            }

            SetIdleState();
            ClearExecutionPlan();
            RefreshVisualState();
        }
    }

    public void ApplyHeal(float heal)
    {
        if (IsCombatDisabled)
            return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + Mathf.Max(0f, heal), 0f, MaxHealth);
        RefreshHPbar();
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
            return;
        // 캐릭터 타입과 번호만 표시 (Ally 1-6, Enemy 1-6)
        string prefix = IsEnemy ? "E" : "A";
        int displayNumber = IsEnemy ? UnitNumber - 6 : UnitNumber;
        statusText.text = $"{prefix}{displayNumber}";
    }

    private void RefreshHPbar()
    {
        if (HPbar == null)
            return;
        HPbar.fillAmount = CurrentHealth / MaxHealth;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
            target.SetActive(value);
    }
}
