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

    private GameObject _runtimeRootObject;
    public GameObject RuntimeRootObject => _runtimeRootObject != null ? _runtimeRootObject : gameObject;

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

    // ── 위치 (State 위임) ────────────────────────────────────────
    public Vector3 Position => State != null ? State.Position : transform.position;

    // ── ML-Agents 외부 제어 ───────────────────────────────────────
    // true면 BattleSimulationManager의 AI 파이프라인(CommitOrSwitch, BuildPlan)을 스킵한다.
    public bool IsExternallyControlled { get; private set; }

    // 공격이 실제로 적에게 적중했을 때 발화한다. (target, wasKillingBlow)
    public event Action<BattleRuntimeUnit, bool> OnAttackLanded;

    public void RaiseAttackLanded(BattleRuntimeUnit target, bool wasKill)
        => OnAttackLanded?.Invoke(target, wasKill);
    public Vector3 ExternalMoveDirection { get; private set; }
    public float ExternalRotationDelta { get; private set; }

    public void SetExternallyControlled(bool value) => IsExternallyControlled = value;

    public void SetExternalMovement(Vector3 worldDirection, float rotationDeltaDegPerSec)
    {
        ExternalMoveDirection = worldDirection;
        ExternalRotationDelta = rotationDeltaDegPerSec;
    }

    public void SetExternalAttackTarget(BattleRuntimeUnit target)
    {
        State.SetPlannedTargets(target != null ? target.State : null, null);
    }

    public void Rotate(float deltaAngleDeg)
    {
        transform.Rotate(0f, deltaAngleDeg, 0f, Space.World);
    }

    public void SetRuntimeRootObject(GameObject runtimeRootObject)
    {
        _runtimeRootObject = runtimeRootObject;
    }

    // ── 파라미터 / 점수 (State 위임) ──────────────────────────────
    public BattleParameterSet CurrentRawParameters => State.CurrentRawParameters;
    public BattleParameterSet CurrentModifiedParameters => State.CurrentModifiedParameters;
    public BattleActionScoreSet CurrentScores => State.CurrentScores;
    public BattleActionType TopScoredAction => State.TopScoredAction;
    public float TopScoredValue => State.TopScoredValue;

    // ── 실행 플랜 타겟 (State 위임) ───────────────────────────────
    public BattleUnitCombatState PlannedTargetEnemy => State.PlannedTargetEnemy;
    public BattleUnitCombatState PlannedTargetAlly => State.PlannedTargetAlly;
    public BattleUnitCombatState CurrentTarget => State.CurrentTarget;

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
    private float _attackAnimationClipLength = -1f;
    private float _skillAnimationClipLength = 0.5f;

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

        State.SyncPosition(transform.position);
        State.ClearTargets();

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
            {
                _myAnimation.runtimeAnimatorController = weaponMotion;
                _attackAnimationClipLength = -1f;
            }
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
        _skillAnimationClipLength = skillAnimation != null ? skillAnimation.length : 0.5f;

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
        State.ClearTargets();

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
        State.StartAttackLock(GetAttackAnimationDuration());

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

    private float GetAttackAnimationDuration()
    {
        if (_myAnimation == null)
            return 0.5f;

        if (_attackAnimationClipLength <= 0f)
        {
            RuntimeAnimatorController controller = _myAnimation.runtimeAnimatorController;
            AnimationClip[] clips = controller != null ? controller.animationClips : null;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null)
                        continue;
                    if (clip.name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _attackAnimationClipLength = clip.length;
                        break;
                    }
                }
            }
        }

        float baseLength = _attackAnimationClipLength > 0f ? _attackAnimationClipLength : 0.5f;
        return baseLength / Mathf.Max(0.01f, _myAnimation.speed);
    }

    public float GetSkillAnimationDuration()
    {
        if (_myAnimation == null)
            return _skillAnimationClipLength;
        return Mathf.Max(0f, _skillAnimationClipLength) / Mathf.Max(0.01f, _myAnimation.speed);
    }

    // ── 스킬 실행 비주얼 ──────────────────────────────────────────
    public void SetSkillState(float animationDuration)
    {
        _myAnimation?.SetTrigger("skill");
        State.StartSkillLock(animationDuration);
        if (PlannedTargetEnemy != null)
            FaceTarget(PlannedTargetEnemy.Position);
        else if (CurrentTarget != null)
            FaceTarget(CurrentTarget.Position);
    }

    // ── State 세터 위임 (SimManager 호출 진입점) ──────────────────

    public void SetBodyRadius(float bodyRadius) => State.SetBodyRadius(bodyRadius);
    public void ClearCurrentTarget() => State.SetCurrentTarget(null);
    public void SetCurrentTarget(BattleRuntimeUnit target) => State.SetCurrentTarget(target != null ? target.State : null);

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
        State.SetPlannedTargets(plan.TargetEnemy, plan.TargetAlly);
        State.SetExecutionPlanPosition(plan.DesiredPosition, plan.HasDesiredPosition);
    }

    public void ClearExecutionPlan()
    {
        State.ClearTargets();
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
        State?.SyncPosition(newPosition);
    }

    public void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    public void PlaceAt(Vector3 worldPos, Transform battlefield)
    {
        if (battlefield != null)
            transform.SetParent(battlefield, false);

        transform.position = worldPos;
        transform.rotation = Quaternion.identity;
        State?.SyncPosition(transform.position);
    }

    public void ClampInsideBattlefield(BoxCollider battlefieldCollider)
    {
        if (battlefieldCollider == null)
            return;

        Vector3 center = battlefieldCollider.bounds.center;
        float arenaRadius = Mathf.Min(battlefieldCollider.bounds.extents.x,
                                      battlefieldCollider.bounds.extents.z) - BodyRadius;

        Vector3 pos = transform.position;
        Vector3 flat = new Vector3(pos.x - center.x, 0f, pos.z - center.z);
        if (flat.magnitude > arenaRadius)
        {
            flat = flat.normalized * arenaRadius;
            pos.x = center.x + flat.x;
            pos.z = center.z + flat.z;
        }
        SetPosition(pos);
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
