using NUnit.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// BattleRuntimeUnit은 전투 중 실시간 상태 holder다.
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

        // 유닛 위치/클램프 기준은 스크립트가 붙은 자기 자신이 아니라 부모 Root RectTransform(3D: BoxCollider).
    // BattleRuntimeUnit 스크립트는 자기 부모 Root를 컨테이너로 사용한다.
    public Vector3 Position => transform.position;

    // RAW 파라미터: 매 tick마다 글로벌 상태로 계산된 원본 9개 파라미터
    // MOD 파라미터: 현재 행동의 currentActionParameterPercents를 적용해 왜곡된 파라미터 (행동 선택 입력값)
    [field: SerializeField] public BattleParameterSet CurrentRawParameters { get; private set; }
    [field: SerializeField] public BattleParameterSet CurrentModifiedParameters { get; private set; }
    [field: SerializeField] public BattleActionScoreSet CurrentScores { get; private set; }

    public BattleRuntimeUnit PlannedTargetEnemy { get; private set; }
    public BattleRuntimeUnit PlannedTargetAlly { get; private set; }

    // 3D 평면 좌표
    public Vector3 PlannedDesiredPosition { get; private set; }
    public bool HasPlannedDesiredPosition { get; private set; }

    [field: SerializeField] public BattleActionType TopScoredAction { get; private set; }
    [field: SerializeField] public float TopScoredValue { get; private set; }



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


    //시각적으로 보이도록 장착
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

    //스킬 관리
    void EquipSkillFromSnapShot()
    {
        if (Snapshot == null)
            return;

        HaveSkill = Snapshot.WeaponSkillId;
        AnimationClip skill_animation = AnimationManager.Instance.getAnimation(HaveSkill);
        skillCooltime = AnimationManager.Instance.getCooltime(HaveSkill);
        _skillType = AnimationManager.Instance.getSkillType(HaveSkill);

        //현재 runtime Animation을 덮어 씌우면 모든 스킬이 바뀜, 복사한 후 바꾸고, 그걸 줘야함
        RuntimeAnimatorController current = _myAnimation.runtimeAnimatorController;

        AnimatorOverrideController local;
        local = new AnimatorOverrideController(current);

        local["HumanM@MiningOneHand01_L - Ground"] = skill_animation;

        _myAnimation.runtimeAnimatorController = local;
    }

    public skillType getSkillType()
    {
        return _skillType;
    }
    public WeaponSkillId getSkill()
    {
        return HaveSkill;
    }



    //애니메이션 속도 조절 필요하므로, 이걸 SimulationManager에서 판정
    public void SetAnimationSpeed(float speedMultiplier)
    {
        if (_myAnimation != null)
        {
            _myAnimation.speed = speedMultiplier;
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
        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;
        CurrentTarget = null;
    }

    //공격 틱
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

    //스킬 틱
    public void TickSkillCooldown(float deltaTime)
    {
        SkillCooldownRemaining = Mathf.Max(0f, SkillCooldownRemaining - Mathf.Max(0f, deltaTime));
    }
    public void ClearSkillCooldown()
    {
        SkillCooldownRemaining = 0f;
    }
    public void ResetSkillCooldown()
    {
        float cooldown = skillCooltime;
        SkillCooldownRemaining = Mathf.Max(0f, cooldown);
    }


    //버프 틱
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

    public int BuffNum()
    {
        return Buffs.Count;
    }


    public int GetBuffLevel(BuffType type)
    {
        int count = 0;
        for (int i = 0; i < Buffs.Count; i++)
        {
            if (Buffs[i] == type)
                count++;
        }
        return count;
    }




    public void AddKnockback(Vector3 forceDirection, float forcePower)
    {
        Vector3 force = forceDirection.normalized * forcePower;
        force.y = 0f;
        CurrentKnockback += force;
    }

    //매틱마다 넉백
    public void TickKnockback(float deltaTime, float friction = 10f)
    {
        if (CurrentKnockback.sqrMagnitude > 0.01f)
        {
            SetPosition(Position + CurrentKnockback * deltaTime);

            CurrentKnockback = Vector3.Lerp(CurrentKnockback, Vector3.zero, friction * deltaTime);      //부드럽게 감소
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
        {
            IsAttacking = false;
        }

        if (_myAnimation != null)
            _myAnimation.SetBool("isMoving", IsMoving);

    }

    //state지만, 실질적으로 때림이 가능할 때 호출
    public void SetAttackState(bool isAttacking)
    {
        if (isAttacking && !IsAttacking)
        {
            if (_myAnimation != null)
            {
                _myAnimation.SetTrigger("attack");
            }

            if (PlannedTargetEnemy != null)
            {
                FaceTarget(PlannedTargetEnemy.Position);
            }
            else if (CurrentTarget != null)
            {
                FaceTarget(CurrentTarget.Position);
            }

        }

        IsAttacking = isAttacking;

        if (isAttacking)
        {
            IsMoving = false;
            if (_myAnimation != null)
                _myAnimation.SetBool("isMoving", false);
        }
    }




    //스킬 사용시 호출
    public void SetSkillState()
    {
        _myAnimation.SetTrigger("skill");
        if (PlannedTargetEnemy != null)
            FaceTarget(PlannedTargetEnemy.Position);
        else if (CurrentTarget != null)
            FaceTarget(CurrentTarget.Position);
    }


    public void SetIdleState()
    {
        if (_myAnimation != null)
            _myAnimation.SetBool("isMoving", false);

        IsMoving = false;
        IsAttacking = false;
    }

    // 3D 월드 좌표 설정 / + 회전
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


    // 깔끔해진 3D 스폰 배치 코드
    public void PlaceOnBattlefieldPlaceholder(Transform placeholder, Transform battlefield)
    {
        if (placeholder == null)
            return;

        if (battlefield != null)
        {
            transform.SetParent(battlefield, false);
        }

        transform.position = placeholder.position;
        transform.rotation = placeholder.rotation;
    }

    // BoxCollider 기반의 3D 평면 클램핑 시스템
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

        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, MaxHealth);
        CurrentHealth = Mathf.Max(0f, CurrentHealth + Mathf.Max(0f, heal));
        RefreshHPbar();
    }




    // 아군/적/사망 표시와 행동 텍스트를 BattleRuntimeUnit이 직접 갱신한다.
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
        if (HPbar == null)
            return;
        HPbar.fillAmount = CurrentHealth / MaxHealth;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }
}
