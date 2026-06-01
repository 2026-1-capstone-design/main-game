using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// BattleRuntimeUnit은 전투 중 비주얼 렌더러다.
// 전투 상태(HP, 쿨다운, 행동 타입 등)는 State(BattleUnitCombatState)가 담당한다.
// TODO: 더 명확한 이름으로 BattleUnitActor를 검토한다. 이 타입은 전투 계산 모델이 아니라
// 씬에서 움직이고 애니메이션/UI/프리팹 표현을 반영하는 MonoBehaviour 경계다.
// prefab 구조: Root -> BattleRuntimeUnit -> Dot_ally / Dot_enemy / Dot_dead / StatusText
// - 아군이면 Dot_ally 활성, 적군이면 Dot_enemy 활성, 죽으면 팀 상관없이 Dot_dead 활성
// - StatusText는 항상 두 줄: 첫 줄 = 유닛 번호, 둘째 줄 = 현재 행동명
// - NameText는 유닛의 표시 이름(DisplayName)을 모델 상단 UI에 별도로 보여준다.
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
    private TextMeshProUGUI nameText;

    private bool _isStatusTextVisible = true;

    [SerializeField]
    private GameObject healthBarRoot;

    [SerializeField]
    private Image blueHealthBarFillImage;

    [FormerlySerializedAs("HPbar")]
    [SerializeField]
    private Image redHealthBarFillImage;

    public BattleUnitCoolBar attackCoolBar;

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

    public bool IsCastingSkill => State.IsCastingSkill;
    public bool ShouldUseAnimatorAttackRelease => Snapshot == null || Snapshot.DefaultDur;

    // ── 위치 (State 위임) ────────────────────────────────────────
    public Vector3 Position => State != null ? State.Position : transform.position;

    // 공격이 실제로 적에게 적중했을 때 발화한다. (target, actualDamage, wasKillingBlow)
    public event Action<BattleRuntimeUnit, float, bool> OnAttackLanded;

    public void RaiseAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill) =>
        OnAttackLanded?.Invoke(target, actualDamage, wasKill);

    private int _lastAttackTriggerFrame = -1;

    public bool HasReadySkill() =>
        State != null && State.GetSkill() != WeaponSkillId.None && SkillCooldownRemaining <= 0f;

    public void RaiseSkillActivated() => OnSkillActivated?.Invoke();

    public event Action OnSkillActivated;

    public void RaiseSkillFailed() => OnSkillFailed?.Invoke();

    public event Action OnSkillFailed;

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

    //공격 모션 맞추기 위한 기본 배속
    private float _normalAttackMotionSpeed = 1f;

    //스킬 모션 맞추기 위한 기본 배속
    private float _normalSkillMotionSpeed = 1f;

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
        State.OnRevived += HandleUnitRevived;
        State.OnActionTypeChanged += (_, _) => RefreshStatusText();
        State.OnAgentStrategyChanged += _ => RefreshStatusText();
        State.OnMovingStateChanged += isMoving => _myAnimation?.SetBool("isMoving", isMoving);
        State.OnIdleStateEntered += () => _myAnimation?.SetBool("isMoving", false);
        State.OnAttackTriggered += HandleAttackTriggered;

        State.SyncPosition(transform.position);
        State.SyncTransform(transform);
        State.ClearTargets();

        _myAnimation = transform.GetComponent<Animator>();

        IAnimationProvider provider = animationProvider ?? AnimationManager.Instance;
        EquipWeaponFromSnapShot(provider);
        EquipSkillFromSnapShot(provider);

        EquipSkinFromSnapshot();

        RefreshHPbar();

        RefreshNameText();
        if (attackCoolBar != null)
        {
            attackCoolBar.Setup(State, ResolveWeaponSkillIcon());
        }

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

        ApplyWeaponPrefabs(Snapshot.LeftWeaponPrefab, Snapshot.RightWeaponPrefab);

        if (_myAnimation != null && provider != null)
        {
            AnimatorOverrideController weaponMotion = provider.GetControllerByWeaponType(Snapshot.WeaponType);
            if (weaponMotion != null)
            {
                _myAnimation.runtimeAnimatorController = weaponMotion;
                _attackAnimationClipLength = GetAttackAnimationClipLength(weaponMotion);

                if (!Snapshot.DefaultDur && Snapshot.Duration > 0f)
                    _normalAttackMotionSpeed = _attackAnimationClipLength / Snapshot.Duration;
                else
                    _normalAttackMotionSpeed = 1f;
            }
        }

        HaveWeapon = Snapshot.WeaponType;
    }

    public void ApplyWeaponPrefabs(
        GameObject leftWeaponPrefab,
        GameObject rightWeaponPrefab,
        bool matchUnitLayer = false
    )
    {
        ClearSpawnedWeapon(ref _spawnedLeftWeapon);
        ClearSpawnedWeapon(ref _spawnedRightWeapon);

        if (leftWeaponPrefab != null && leftHandSocket != null)
        {
            _spawnedLeftWeapon = Instantiate(leftWeaponPrefab, leftHandSocket);
            if (matchUnitLayer)
            {
                SetLayerRecursively(_spawnedLeftWeapon, gameObject.layer);
            }
        }

        if (rightWeaponPrefab != null && rightHandSocket != null)
        {
            _spawnedRightWeapon = Instantiate(rightWeaponPrefab, rightHandSocket);
            if (matchUnitLayer)
            {
                SetLayerRecursively(_spawnedRightWeapon, gameObject.layer);
            }
        }
    }

    private static void ClearSpawnedWeapon(ref GameObject spawnedWeapon)
    {
        if (spawnedWeapon == null)
        {
            return;
        }

        Destroy(spawnedWeapon);
        spawnedWeapon = null;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
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
        _skillAnimationClipLength = skillAnimation != null ? skillAnimation.length : 1f;

        if (!Snapshot.SkillDefaultDur && Snapshot.SkillDuration > 0f)
            _normalSkillMotionSpeed = _skillAnimationClipLength / Snapshot.SkillDuration;
        else
            _normalSkillMotionSpeed = 1f;

        State.SetSkillInfo(skillId, cooltime, type, Snapshot.SkillDefaultDur, Snapshot.SkillDuration);

        if (_myAnimation != null && skillAnimation != null)
        {
            RuntimeAnimatorController current = _myAnimation.runtimeAnimatorController;
        }
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

    //공격 모션의 애니메이션 클립 길이 가져오기
    private float GetAttackAnimationClipLength(RuntimeAnimatorController controller)
    {
        if (controller == null)
            return 1f;

        // 1. 현재 컨트롤러가 오버라이드 컨트롤러인지 확인합니다.
        if (controller is AnimatorOverrideController overrideController)
        {
            // 2. 원본 클립의 이름을 '열쇠'로 사용하여, 현재 그 자리에 껴있는(덮어씌워진) 클립을 가져옵니다.
            AnimationClip currentAttackClip = overrideController["HumanM@AttackShield01"];

            if (currentAttackClip != null)
            {
                return currentAttackClip.length; // 새로운 무기 모션의 길이를 정확히 반환!
            }
        }
        else
        {
            // (예외 처리) 오버라이드가 안 된 순정 상태일 경우
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip.name == "HumanM@AttackShield01")
                    return clip.length;
            }
        }

        return 1f; // 못 찾으면 기본값
    }

    // ── 커스터마이즈 ─────────────────────────
    private void EquipSkinFromSnapshot()
    {
        if (Snapshot == null || Snapshot.CustomizeIndicates == null)
            return;

        Debug.Log("값은 들어옴");

        ApplySkinCustomization(Snapshot.CustomizeIndicates);
    }

    // 메인 메뉴의 3D 프리뷰도 전투 프리팹을 그대로 쓰므로, 전투 Snapshot 없이 스킨 파츠만 적용할 수 있게 열어둔다.
    public void ApplySkinCustomization(int[] indicates)
    {
        if (indicates == null || indicates.Length <= (int)SkinPart.Feet)
        {
            indicates = BuildDefaultSkinCustomization();
        }

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

    private static int[] BuildDefaultSkinCustomization()
    {
        int[] indicates = new int[(int)SkinPart.TotalCount];
        indicates[(int)SkinPart.FullHead] = -1;
        indicates[(int)SkinPart.Nose] = 0;
        indicates[(int)SkinPart.Hair] = 0;
        indicates[(int)SkinPart.Face] = 0;
        indicates[(int)SkinPart.Eyes] = 0;
        indicates[(int)SkinPart.Eyebrows] = 0;
        indicates[(int)SkinPart.Ears] = 0;
        indicates[(int)SkinPart.Chest] = 0;
        indicates[(int)SkinPart.Arms] = 0;
        indicates[(int)SkinPart.Belt] = 0;
        indicates[(int)SkinPart.Legs] = 0;
        indicates[(int)SkinPart.Feet] = 0;
        return indicates;
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

    private void HandleUnitRevived()
    {
        RefreshHPbar();
        RefreshVisualState();
    }

    // ── 공격 트리거 (OnAttackTriggered 이벤트 핸들러) ────────────
    private void HandleAttackTriggered()
    {
        //여기서 공속 비례한 속도 갱신
        //최종 속도 = (무기 duration 보정값) * (현재 스탯에 적용된 버프 포함 공격 속도)
        if (_myAnimation != null)
        {
            float finalAttackSpeed = _normalAttackMotionSpeed * AttackSpeed; // AttackSpeed는 State 위임 프로퍼티입니다.
            _myAnimation.SetFloat("AttackSpeed", finalAttackSpeed);
        }

        _myAnimation?.SetTrigger("attack");
        _lastAttackTriggerFrame = Time.frameCount;
        State.SetAttackState(true);
        if (!ShouldUseAnimatorAttackRelease)
            State.StartAttackingLock(GetAttackingLockDuration());
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

    public float GetAttackingLockDuration()
    {
        if (Snapshot == null || Snapshot.DefaultDur || Snapshot.Duration <= 0f)
            return 0f;

        return Snapshot.Duration / Mathf.Max(0.01f, AttackSpeed);
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
        if (_myAnimation != null)
        {
            float finalSkillSpeed = _normalSkillMotionSpeed * AttackSpeed;
            _myAnimation.SetFloat("SkillSpeed", finalSkillSpeed);
        }

        _myAnimation?.SetTrigger("skill");
        state.SetCastingSkillState(true);
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
    public void TickBufflCooldown(float deltaTime, IBattleEffectSink effects) =>
        State.TickBufflCooldown(deltaTime, effects);

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
    public float ApplyDamage(float damage) => State.ApplyDamage(damage);

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
        RefreshNameText();
    }

    private void RefreshNameText()
    {
        if (nameText == null)
        {
            return;
        }

        string displayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName;
        nameText.text = displayName;
        SetActive(nameText.gameObject, displayName.Length > 0 && !IsCombatDisabled && CurrentHealth > 0f);
    }

    private void RefreshStatusText()
    {
        if (statusText == null)
            return;
        statusText.text = $"{UnitNumber}\n{State.AgentStrategy}";
        SetActive(statusText.gameObject, _isStatusTextVisible);
    }

    public void SetDebugStatusTextVisible(bool isVisible)
    {
        _isStatusTextVisible = isVisible;
        if (statusText != null)
        {
            SetActive(statusText.gameObject, isVisible);
        }
    }

    private void RefreshHPbar()
    {
        bool isAlive = !IsCombatDisabled && CurrentHealth > 0f;
        SetActive(healthBarRoot, isAlive);
        if (!isAlive || MaxHealth <= 0f)
        {
            SetHealthFillActive(blueHealthBarFillImage, false);
            SetHealthFillActive(redHealthBarFillImage, false);
            return;
        }

        float ratio = Mathf.Clamp01(CurrentHealth / MaxHealth);
        SetHealthFillActive(blueHealthBarFillImage, IsPlayerOwned);
        SetHealthFillActive(redHealthBarFillImage, !IsPlayerOwned);
        ApplyHealthFillRatio(IsPlayerOwned ? blueHealthBarFillImage : redHealthBarFillImage, ratio);
    }

    private static void SetHealthFillActive(Image fillImage, bool isActive)
    {
        if (fillImage != null)
        {
            SetActive(fillImage.gameObject, isActive);
        }
    }

    private static void ApplyHealthFillRatio(Image fillImage, float ratio)
    {
        if (fillImage == null)
        {
            return;
        }

        fillImage.enabled = ratio > 0f;
        fillImage.fillAmount = ratio;

        RectTransform fillRect = fillImage.rectTransform;
        if (fillRect != null)
        {
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }
    }

    private Sprite ResolveWeaponSkillIcon()
    {
        if (Snapshot == null || Snapshot.WeaponSkillId == WeaponSkillId.None)
        {
            return null;
        }

        ContentDatabaseProvider provider = ContentDatabaseProvider.Instance;
        IReadOnlyList<WeaponSkillSO> skills = provider != null ? provider.WeaponSkills : null;
        if (skills == null)
        {
            return null;
        }

        for (int i = 0; i < skills.Count; i++)
        {
            WeaponSkillSO skill = skills[i];
            if (skill != null && skill.skillId == Snapshot.WeaponSkillId)
            {
                return skill.icon;
            }
        }

        return null;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
            target.SetActive(value);
    }
}
