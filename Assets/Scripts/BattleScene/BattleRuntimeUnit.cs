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
    [SerializeField]
    private bool verboseLog = false;

    [Header("Visuals")]
    [SerializeField]
    private GameObject dotAlly;

    [SerializeField]
    private GameObject dotEnemy;

    [SerializeField]
    private GameObject dotDead;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private Image HPbar;

    [SerializeField]
    private Sprite AllybarSprite;

    [SerializeField]
    private Sprite EnemybarSprite;

    // ── 순수 전투 상태 (Animator/UI 없음) ─────────────────────────
    [Header("Runtime State (Debug)")]
    [SerializeField]
    private BattleUnitCombatState state;

    [SerializeField]
    public BattleUnitCombatState State => state;

    private GameObject _runtimeRootObject;
    public GameObject RuntimeRootObject => _runtimeRootObject != null ? _runtimeRootObject : gameObject;

    // ── 정체성 프로퍼티 (State 위임) ──────────────────────────────
    public int UnitNumber => State.UnitNumber;
    public BattleTeamId TeamId => State.TeamId;
    public bool IsPlayerOwned { get; private set; }
    public bool IsEnemy => !IsPlayerOwned;
    public BattleUnitSnapshot Snapshot { get; private set; }

    public string DisplayName => State.DisplayName;
    public int Level => State.Level;

    // ── 체력 (State 위임) ─────────────────────────────────────────
    [SerializeField]
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

    public void RaiseAttackLanded(BattleRuntimeUnit target, bool wasKill) => OnAttackLanded?.Invoke(target, wasKill);

    private int _lastAttackTriggerFrame = -1;

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
    [SerializeField]
    private Transform leftHandSocket;

    [SerializeField]
    private Transform rightHandSocket;

    [SerializeField]
    private GameObject _spawnedLeftWeapon;

    [SerializeField]
    private GameObject _spawnedRightWeapon;

    [SerializeField]
    private Animator _myAnimation;

    [SerializeField]
    private WeaponType HaveWeapon;

    //customize
    [Header("Skin Part Roots")]
    [SerializeField]
    private Transform rootFullHead; // HEADS 폴더 연결

    [SerializeField]
    private Transform rootNose; // NOSES 폴더 연결

    [SerializeField]
    private Transform rootHair; // HAIRS 폴더 연결

    [SerializeField]
    private Transform rootFaceHair; // FACE HAIRS 폴더 연결

    [SerializeField]
    private Transform rootEyes; // EYES 폴더 연결

    [SerializeField]
    private Transform rootEyebrows; // EYEBROWS 폴더 연결

    [SerializeField]
    private Transform rootEars; // EARS 폴더 연결

    [SerializeField]
    private Transform rootChest; // CHESTS 폴더 연결

    [SerializeField]
    private Transform rootArms; // ARMS 폴더 연결

    [SerializeField]
    private Transform rootBelt; // BELTS 폴더 연결

    [SerializeField]
    private Transform rootLegs; // LEGS 폴더 연결

    [SerializeField]
    private Transform rootFeet; // FEET 폴더 연결
    private float _attackAnimationClipLength = -1f;
    private float _skillAnimationClipLength = 0.5f;

    // animationProvider가 null이면 AnimationManager.Instance로 폴백한다.
    public void Initialize(
        BattleUnitSnapshot snapshot,
        int unitNumber,
        BattleTeamId teamId,
        bool isPlayerOwned,
        IAnimationProvider animationProvider = null
    )
    {
        if (snapshot == null)
        {
            Debug.LogError("[BattleRuntimeUnit] Initialize received null snapshot.", this);
            return;
        }

        Snapshot = snapshot;
        IsPlayerOwned = isPlayerOwned;

        // ── State 생성 및 이벤트 구독 ────────────────────────────
        state = new BattleUnitCombatState(snapshot, unitNumber, teamId);
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

        EquipSkinFromSnapshot();

        if (!isPlayerOwned)
            HPbar.sprite = EnemybarSprite;
        else
            HPbar.sprite = AllybarSprite;

        RefreshHPbar();

        string runtimeName = $"{(isPlayerOwned ? "Player" : "Hostile")}_{UnitNumber}_{DisplayName}";
        if (RuntimeRootObject != null)
            RuntimeRootObject.name = runtimeName;

        RefreshVisualState();

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleRuntimeUnit] Initialized. UnitNumber={UnitNumber}, Name={DisplayName}, "
                    + $"TeamId={TeamId.Value}, IsPlayerOwned={IsPlayerOwned}, HP={CurrentHealth:0.##}/{MaxHealth:0.##}",
                this
            );
        }
    }

    // ── 무기/스킬 장착 ────────────────────────────────────────────
    private void EquipWeaponFromSnapShot(IAnimationProvider provider)
    {
        if (Snapshot == null)
            return;

        //넣을 떄 주석 처리 필요 -> 위치 이미 할당됨
        if (Snapshot.LeftWeaponPrefab != null && leftHandSocket != null)
        {
            Debug.Log("왼손 무기 장착");
            _spawnedLeftWeapon = Instantiate(Snapshot.LeftWeaponPrefab, leftHandSocket);
            //_spawnedLeftWeapon.transform.localPosition = Vector3.zero;
            //_spawnedLeftWeapon.transform.localRotation = Quaternion.identity;
        }
        if (Snapshot.RightWeaponPrefab != null && rightHandSocket != null)
        {
            Debug.Log("오른손 무기 장착");
            _spawnedRightWeapon = Instantiate(Snapshot.RightWeaponPrefab, rightHandSocket);
            //_spawnedRightWeapon.transform.localPosition = Vector3.zero;
            //_spawnedRightWeapon.transform.localRotation = Quaternion.identity;
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

        HaveWeapon = Snapshot.WeaponType;
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

            if (current is AnimatorOverrideController existingOverride)
            {
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                existingOverride.GetOverrides(overrides);
                local.ApplyOverrides(overrides);
            }

            local["HumanM@MiningOneHand01_L - Ground"] = skillAnimation;
            _myAnimation.runtimeAnimatorController = local;
        }
    }

    // ── 커스터마이즈 ─────────────────────────
    private void EquipSkinFromSnapshot()
    {
        if (Snapshot == null || Snapshot.CustomizeIndicates == null)
            return;

        Debug.Log("값은 들어옴");

        int[] indicates = Snapshot.CustomizeIndicates;

        // 1. 머리 및 세부 얼굴 파츠 토글
        ActivateSpecificSkinPart(rootFullHead, indicates[(int)SkinPart.FullHead]);
        ActivateSpecificSkinPart(rootNose, indicates[(int)SkinPart.Nose]);
        ActivateSpecificSkinPart(rootHair, indicates[(int)SkinPart.Hair]);
        ActivateSpecificSkinPart(rootFaceHair, indicates[(int)SkinPart.Face]);
        ActivateSpecificSkinPart(rootEyes, indicates[(int)SkinPart.Eyes]);
        ActivateSpecificSkinPart(rootEyebrows, indicates[(int)SkinPart.Eyebrows]);
        ActivateSpecificSkinPart(rootEars, indicates[(int)SkinPart.Ears]);

        // 2. 공통 바디 파츠 토글
        ActivateSpecificSkinPart(rootChest, indicates[(int)SkinPart.Chest]);
        ActivateSpecificSkinPart(rootArms, indicates[(int)SkinPart.Arms]);
        ActivateSpecificSkinPart(rootBelt, indicates[(int)SkinPart.Belt]);
        ActivateSpecificSkinPart(rootLegs, indicates[(int)SkinPart.Legs]);
        ActivateSpecificSkinPart(rootFeet, indicates[(int)SkinPart.Feet]);
    }

    private void ActivateSpecificSkinPart(Transform parentRoot, int targetIndex)
    {
        if (parentRoot == null)
            return;

        // 부모안 모든 파츠 확인
        for (int i = 0; i < parentRoot.childCount; i++)
        {
            // targetIndex가 -1이면 모든 자식의 활성화 상태가 false가 됩니다. (즉, 안 입음)
            // i와 targetIndex가 같을 때만 true가 되어 해당 옷이 나타납니다.
            parentRoot.GetChild(i).gameObject.SetActive(i == targetIndex);
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
        _lastAttackTriggerFrame = Time.frameCount;
        State.SetAttackState(true);
        State.SetMovementState(false);

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

    public bool IsAttackAnimationPlaying()
    {
        if (_myAnimation == null)
            return false;

        // SetTrigger("attack")는 Animator가 Update 이후 평가한다.
        // 한 Unity frame 안에서 시뮬레이션 tick이 여러 번 돌 수 있으므로,
        // 다음 tick이 Animator의 attack1 진입보다 먼저 실행될 수 있다.
        if (_lastAttackTriggerFrame == Time.frameCount)
            return true;

        var info = _myAnimation.GetCurrentAnimatorStateInfo(0);
        if (info.IsName("attack1") && info.normalizedTime < 1f)
            return true;

        // idle → attack1 트랜지션 중에는 현재 상태가 아직 attack1이 아니므로 목적지도 확인
        if (_myAnimation.IsInTransition(0))
        {
            var nextInfo = _myAnimation.GetNextAnimatorStateInfo(0);
            if (nextInfo.IsName("attack1"))
                return true;
        }

        return false;
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
        if (PlannedTargetEnemy != null)
            FaceTarget(PlannedTargetEnemy.Position);
        else if (CurrentTarget != null)
            FaceTarget(CurrentTarget.Position);
    }

    // ── State 세터 위임 (SimManager 호출 진입점) ──────────────────

    public void SetBodyRadius(float bodyRadius) => State.SetBodyRadius(bodyRadius);

    public void ClearCurrentTarget() => State.SetCurrentTarget(null);

    public void SetCurrentTarget(BattleRuntimeUnit target) =>
        State.SetCurrentTarget(target != null ? target.State : null);

    public void SetCurrentParameters(BattleParameterSet raw, BattleParameterSet modified) =>
        State.SetCurrentParameters(raw, modified);

    public void SetCurrentScores(BattleActionScoreSet scores) => State.SetCurrentScores(scores);

    public void SetCurrentActionType(BattleActionType actionType, string displayName = null) =>
        State.SetCurrentActionType(actionType, displayName);

    public void SetCurrentAction(string actionName) => State.SetCurrentAction(actionName);

    public void SetDecisionState(float keepBehaving, float actionTimer) =>
        State.SetDecisionState(keepBehaving, actionTimer);

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
    public void AddKnockback(Vector3 forceDirection, float forcePower) =>
        State.AddKnockback(forceDirection, forcePower);

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

    /*
        public void ClampInsideBattlefield(SphereCollider battlefieldCollider)
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
    */
    public void ClampInsideBattlefield(SphereCollider sphereCollider)
    {
        if (sphereCollider == null)
            return;

        // 1. 원의 중심과 반지름을 가져옵니다.
        Vector3 center = sphereCollider.transform.position;
        // 반지름에서 유닛의 반지름(BodyRadius)만큼 뺀 값이 실제 한계선입니다.
        float maxRadius = (sphereCollider.radius * sphereCollider.transform.lossyScale.x) - BodyRadius;

        // 2. 중심에서 유닛까지의 방향과 거리를 계산합니다.
        Vector3 offset = transform.position - center;
        offset.y = 0; // 높이는 무시 (평면 전투 기준)
        float distance = offset.magnitude;

        // 3. 거리가 반지름보다 멀어지면 위치를 강제로 조정합니다.
        if (distance > maxRadius)
        {
            Vector3 clampedPosition = center + (offset.normalized * maxRadius);
            clampedPosition.y = transform.position.y;
            SetPosition(clampedPosition);
        }
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
