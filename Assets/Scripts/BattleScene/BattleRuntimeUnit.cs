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

    public GameObject RuntimeRootObject => gameObject;

    public int UnitNumber { get; private set; }
    public bool IsEnemy { get; private set; }
    public BattleUnitSnapshot Snapshot { get; private set; }

    public string DisplayName { get; private set; }
    public int Level { get; private set; }

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public float Attack { get; private set; }
    public float AttackSpeed { get; private set; }
    [field: SerializeField]  public float MoveSpeed { get; private set; }
    [field: SerializeField]  public float AttackRange { get; private set; }

    public bool IsCombatDisabled { get; private set; }
    public string CurrentAction { get; private set; }
    [field: SerializeField]  public BattleActionType CurrentActionType { get; private set; }
    public float KeepBehaving { get; private set; }
    public float ActionTimer { get; private set; }

    public float BodyRadius { get; private set; } = 50f;
    public BattleRuntimeUnit CurrentTarget { get; private set; }
    public float AttackCooldownRemaining { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsAttacking { get; private set; }

    // 2D AnchoredPosition 대신 3D World Position을 사용합니다.
    public Vector3 Position => transform.position;

    [field: SerializeField]  public BattleParameterSet CurrentRawParameters { get; private set; }
    [field: SerializeField]  public BattleParameterSet CurrentModifiedParameters { get; private set; }
    [field: SerializeField]  public BattleActionScoreSet CurrentScores { get; private set; }

    public BattleRuntimeUnit PlannedTargetEnemy { get; private set; }
    public BattleRuntimeUnit PlannedTargetAlly { get; private set; }

    // 3D 평면 좌표
    public Vector3 PlannedDesiredPosition { get; private set; }
    public bool HasPlannedDesiredPosition { get; private set; }

    [field: SerializeField]  public BattleActionType TopScoredAction { get; private set; }
    [field: SerializeField]  public float TopScoredValue { get; private set; }



    [Header("Weapon Sockets")]
    [SerializeField] private Transform leftHandSocket;  
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private GameObject _spawnedLeftWeapon;
    [SerializeField] private GameObject _spawnedRightWeapon;
    [SerializeField] private Animator _myAnimation;

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
        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;

        TopScoredAction = BattleActionType.None;
        TopScoredValue = 0f;

        _myAnimation = this.transform.GetComponent<Animator>();
        EquipWeaponFromSnapShot();


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

        if(Snapshot.LeftWeaponPrefab != null && leftHandSocket != null)
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

        if(_myAnimation != null && AnimationManager.Instance != null)
        {
            AnimatorOverrideController weaponMotion = AnimationManager.Instance.GetControllerByWeaponType(Snapshot.WeaponType);
            if (weaponMotion != null)
                _myAnimation.runtimeAnimatorController = weaponMotion;
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

        if (_myAnimation != null)
            _myAnimation.SetBool("isMoving", IsMoving);

    }

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

    public void SetIdleState()
    {
        if(_myAnimation != null)
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
        if (placeholder == null) return;

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
        if (battlefieldCollider == null) return;

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
        if (IsCombatDisabled) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));

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
        if (statusText == null) return;
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