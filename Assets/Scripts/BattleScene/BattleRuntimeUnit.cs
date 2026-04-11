using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleRuntimeUnit은 전투 중 비주얼 렌더러다.
// 전투 상태(HP, 쿨다운, 행동 타입 등)는 State(BattleUnitCombatState)가 담당한다.
// prefab 구조: Root -> BattleRuntimeUnit -> Dot_ally / Dot_enemy / Dot_dead / StatusText
// - 아군이면 Dot_ally 활성, 적군이면 Dot_enemy 활성, 죽으면 팀 상관없이 Dot_dead 활성
// - StatusText는 항상 두 줄: 첫 줄 = 유닛 번호, 둘째 줄 = 현재 행동명
// 스폰 시에는 Root 프리팹 전체를 instantiate하고, GetComponentInChildren<BattleRuntimeUnit>(true)로 내부 컴포넌트를 찾는다.
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

    // ── 순수 전투 상태 (Animator/UI 없음) ─────────────────────────
    public BattleUnitCombatState State { get; private set; }

    public GameObject RuntimeRootObject => gameObject;

    // ── 정체성 프로퍼티 (State 위임) ──────────────────────────────
    public int UnitNumber => State.UnitNumber;
    public bool IsEnemy => State.IsEnemy;
    public BattleUnitSnapshot Snapshot { get; private set; }

    public string DisplayName => State.DisplayName;
    public int Level => State.Level;

    // ── 체력 (State 위임) ─────────────────────────────────────────
    public float MaxHealth => State.MaxHealth;
    public float CurrentHealth => State.CurrentHealth;
    public bool IsCombatDisabled => State.IsCombatDisabled;

    // ── 스탯 (State 위임) ─────────────────────────────────────────
    public float BaseAttack => State.BaseAttack;
    public float BaseAttackSpeed => State.BaseAttackSpeed;
    public float BaseMoveSpeed => State.BaseMoveSpeed;
    public float BaseAttackRange => State.BaseAttackRange;

    public float Attack => State.Attack;
    public float AttackSpeed => State.AttackSpeed;
    public float MoveSpeed => State.MoveSpeed;
    public float AttackRange => State.AttackRange;

    // ── 행동/결정 상태 (State 위임) ───────────────────────────────
    public string CurrentAction => State.CurrentAction;
    public BattleActionType CurrentActionType => State.CurrentActionType;
    public float KeepBehaving => State.KeepBehaving;
    public float ActionTimer => State.ActionTimer;

    // ── 쿨다운 (State 위임) ────────────────────────────────────────
    public float BodyRadius => State.BodyRadius;
    public float AttackCooldownRemaining => State.AttackCooldownRemaining;
    public float SkillCooldownRemaining => State.SkillCooldownRemaining;

    // ── 이동/공격 플래그 (State 위임) ─────────────────────────────
    public bool IsMoving => State.IsMoving;
    public bool IsAttacking => State.IsAttacking;

    // ── 위치 (transform 직접) ─────────────────────────────────────
    // 유닛 위치/클램프 기준은 스크립트가 붙은 자기 자신이 아니라 부모 Root(3D: BoxCollider).
    public Vector3 Position => transform.position;

    // ── 파라미터 / 점수 (State 위임) ──────────────────────────────
    public BattleParameterSet CurrentRawParameters => State.CurrentRawParameters;
    public BattleParameterSet CurrentModifiedParameters => State.CurrentModifiedParameters;
    public BattleActionScoreSet CurrentScores => State.CurrentScores;
    public BattleActionType TopScoredAction => State.TopScoredAction;
    public float TopScoredValue => State.TopScoredValue;

    // ── 실행 플랜 (BRU 참조는 BRU가 직접 보유 — 순환 의존 방지) ──
    public BattleRuntimeUnit PlannedTargetEnemy { get; private set; }
    public BattleRuntimeUnit PlannedTargetAlly { get; private set; }
    public BattleRuntimeUnit CurrentTarget { get; private set; }

    public Vector3 PlannedDesiredPosition => State.PlannedDesiredPosition;
    public bool HasPlannedDesiredPosition => State.HasPlannedDesiredPosition;

    // ── 넉백 (State 위임) ─────────────────────────────────────────
    public Vector3 CurrentKnockback => State.CurrentKnockback;

    [Header("Weapon Sockets")]
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private GameObject _spawnedLeftWeapon;
    [SerializeField] private GameObject _spawnedRightWeapon;
    [SerializeField] private Animator _myAnimation;

    // animationProvider가 null이면 AnimationManager.Instance로 폴백한다.
    public void Initialize(BattleUnitSnapshot snapshot, int unitNumber, bool isEnemy,
        IAnimationProvider animationProvider = null)
    {
        if (snapshot == null)
        {
            Debug.LogError("[BattleRuntimeUnit] Initialize received null snapshot.", this);
            return;
        }

        Snapshot = snapshot;

        // ── State 생성 및 이벤트 구독 ────────────────────────────
        State = new BattleUnitCombatState(snapshot, unitNumber, isEnemy);
        State.OnHealthChanged += _ => RefreshHPbar();
        State.OnDied += HandleUnitDied;
        State.OnActionTypeChanged += (_, _) => RefreshStatusText();
        State.OnMovingStateChanged += isMoving => _myAnimation?.SetBool("isMoving", isMoving);
        State.OnIdleStateEntered += () => _myAnimation?.SetBool("isMoving", false);
        State.OnAttackTriggered += HandleAttackTriggered;

        PlannedTargetEnemy = null;
        PlannedTargetAlly = null;
        CurrentTarget = null;

        _myAnimation = transform.GetComponent<Animator>();

        IAnimationProvider provider = animationProvider ?? AnimationManager.Instance;
        EquipWeaponFromSnapShot(provider);
        EquipSkillFromSnapShot(provider);

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

    // ── 무기/스킬 장착 ────────────────────────────────────────────
    private void EquipWeaponFromSnapShot(IAnimationProvider provider)
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

        if (_myAnimation != null && provider != null)
        {
            AnimatorOverrideController weaponMotion = provider.GetControllerByWeaponType(Snapshot.WeaponType);
            if (weaponMotion != null)
                _myAnimation.runtimeAnimatorController = weaponMotion;
        }
    }

    private void EquipSkillFromSnapShot(IAnimationProvider provider)
    {
        if (Snapshot == null)
            return;

        if (provider == null)
        {
            Debug.LogWarning("[BattleRuntimeUnit] IAnimationProvider is null — skipping skill setup.", this);
            return;
        }

        WeaponSkillId skillId = Snapshot.WeaponSkillId;
        AnimationClip skillAnimation = provider.getAnimation(skillId);
        float cooltime = provider.getCooltime(skillId);
        skillType type = provider.getSkillType(skillId);

        State.SetSkillInfo(skillId, cooltime, type);

        if (_myAnimation != null && skillAnimation != null)
        {
            RuntimeAnimatorController current = _myAnimation.runtimeAnimatorController;
            AnimatorOverrideController local = new AnimatorOverrideController(current);
            local["HumanM@MiningOneHand01_L - Ground"] = skillAnimation;
            _myAnimation.runtimeAnimatorController = local;
        }
    }

    // ── 사망 처리 (OnDied 이벤트 핸들러) ─────────────────────────
    private void HandleUnitDied()
    {
        PlannedTargetEnemy = null;
        PlannedTargetAlly = null;
        CurrentTarget = null;

        if (_myAnimation != null)
        {
            _myAnimation.SetBool("isMoving", false);
            _myAnimation.SetTrigger("die");
        }

        RefreshVisualState();
    }

    // ── 공격 트리거 (OnAttackTriggered 이벤트 핸들러) ────────────
    private void HandleAttackTriggered()
    {
        _myAnimation?.SetTrigger("attack");

        if (PlannedTargetEnemy != null)
            FaceTarget(PlannedTargetEnemy.Position);
        else if (CurrentTarget != null)
            FaceTarget(CurrentTarget.Position);
    }

    // ── 애니메이션 속도 ────────────────────────────────────────────
    public void SetAnimationSpeed(float speedMultiplier)
    {
        if (_myAnimation != null)
            _myAnimation.speed = speedMultiplier;
    }

    // ── 스킬 실행 비주얼 ──────────────────────────────────────────
    public void SetSkillState()
    {
        _myAnimation?.SetTrigger("skill");
        if (PlannedTargetEnemy != null)
            FaceTarget(PlannedTargetEnemy.Position);
        else if (CurrentTarget != null)
            FaceTarget(CurrentTarget.Position);
    }

    // ── State 세터 위임 (SimManager 호출 진입점) ──────────────────

    public void SetBodyRadius(float bodyRadius) => State.SetBodyRadius(bodyRadius);
    public void ClearCurrentTarget() { CurrentTarget = null; }
    public void SetCurrentTarget(BattleRuntimeUnit target) { CurrentTarget = target; }

    public void SetCurrentParameters(BattleParameterSet raw, BattleParameterSet modified)
        => State.SetCurrentParameters(raw, modified);

    public void SetCurrentScores(BattleActionScoreSet scores)
        => State.SetCurrentScores(scores);

    public void SetCurrentActionType(BattleActionType actionType, string displayName = null)
        => State.SetCurrentActionType(actionType, displayName);

    public void SetCurrentAction(string actionName)
        => State.SetCurrentAction(actionName);

    public void SetDecisionState(float keepBehaving, float actionTimer)
        => State.SetDecisionState(keepBehaving, actionTimer);

    public void SetExecutionPlan(BattleActionExecutionPlan plan)
    {
        PlannedTargetEnemy = plan.TargetEnemy;
        PlannedTargetAlly = plan.TargetAlly;
        CurrentTarget = plan.TargetEnemy;
        State.SetExecutionPlanPosition(plan.DesiredPosition, plan.HasDesiredPosition);
    }

    public void ClearExecutionPlan()
    {
        PlannedTargetEnemy = null;
        PlannedTargetAlly = null;
        CurrentTarget = null;
        State.ClearExecutionPlanPosition();
    }

    // ── 쿨다운 위임 ────────────────────────────────────────────────
    public void TickAttackCooldown(float deltaTime) => State.TickAttackCooldown(deltaTime);
    public void ClearAttackCooldown() => State.ClearAttackCooldown();
    public void ResetAttackCooldown() => State.ResetAttackCooldown();

    public void TickSkillCooldown(float deltaTime) => State.TickSkillCooldown(deltaTime);
    public void ClearSkillCooldown() => State.ClearSkillCooldown();
    public void ResetSkillCooldown() => State.ResetSkillCooldown();

    public WeaponSkillId getSkill() => State.GetSkill();
    public skillType getSkillType() => State.GetSkillType();

    // ── 버프 위임 ─────────────────────────────────────────────────
    public void TickBufflCooldown(float deltaTime) => State.TickBufflCooldown(deltaTime);
    public void BuffApply(BuffType type, int level, float cool) => State.BuffApply(type, level, cool);
    public int BuffNum() => State.BuffNum();
    public int GetBuffLevel(BuffType type) => State.GetBuffLevel(type);

    // ── 넉백 위임 ─────────────────────────────────────────────────
    public void AddKnockback(Vector3 forceDirection, float forcePower)
        => State.AddKnockback(forceDirection, forcePower);

    public void TickKnockback(float deltaTime, float friction = 10f)
    {
        Vector3 delta = State.ConsumeKnockbackDelta(deltaTime, friction);
        if (delta.sqrMagnitude > 0f)
            SetPosition(Position + delta);
    }

    // ── 체력 위임 ─────────────────────────────────────────────────
    public void ApplyDamage(float damage) => State.ApplyDamage(damage);
    public void ApplyHeal(float heal) => State.ApplyHeal(heal);

    // ── 이동/공격 상태 위임 ───────────────────────────────────────
    public void SetMovementState(bool isMoving) => State.SetMovementState(isMoving);
    public void SetAttackState(bool isAttacking) => State.SetAttackState(isAttacking);
    public void SetIdleState() => State.SetIdleState();

    // ── 위치/회전 (Transform 직접) ────────────────────────────────
    public void SetPosition(Vector3 newPosition)
    {
        transform.position = newPosition;
    }

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

        float minX = bounds.min.x + BodyRadius;
        float maxX = bounds.max.x - BodyRadius;
        float minZ = bounds.min.z + BodyRadius;
        float maxZ = bounds.max.z - BodyRadius;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        transform.position = pos;
    }

    // ── 배치 ──────────────────────────────────────────────────────

    // ── 비주얼 갱신 (이벤트 구독 또는 내부 호출) ─────────────────
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
        string actionLine = string.IsNullOrWhiteSpace(CurrentAction) ? "Idle" : CurrentAction;
        statusText.text = $"{UnitNumber}\n{actionLine}";
    }

    private void RefreshHPbar()
    {
        if (HPbar == null || MaxHealth <= 0f)
            return;
        HPbar.fillAmount = CurrentHealth / MaxHealth;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
            target.SetActive(value);
    }
}
