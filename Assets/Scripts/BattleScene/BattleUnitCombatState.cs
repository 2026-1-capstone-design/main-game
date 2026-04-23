using System;
using System.Collections.Generic;
using UnityEngine;

// BattleUnitCombatState: 순수 전투 상태 컨테이너 (MonoBehaviour 없음, Unity 씬 의존 없음)
// BattleRuntimeUnit이 소유하며, BattleSimulationManager가 이 객체를 통해 전투 상태를 읽고 쓴다.
// Animator, UI, Transform 등 비주얼 관련 로직은 포함하지 않는다.
[Serializable]
public sealed class BattleUnitCombatState
{
    // ── 유닛 정체성 ────────────────────────────────────────────────
    public int UnitNumber { get; private set; }
    public bool IsEnemy { get; private set; }
    public string DisplayName { get; private set; }
    public int Level { get; private set; }

    // ── 체력 / 전투불능 ────────────────────────────────────────────
    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsCombatDisabled { get; private set; }

    // ── 기본 스탯 (스냅샷에서 읽어온 원본값) ──────────────────────
    [SerializeField] public float BaseAttack;
    [SerializeField] public float BaseAttackSpeed;
    [SerializeField] public float BaseMoveSpeed;
    [SerializeField] public float BaseAttackRange;

    // ── 실효 스탯 (버프 반영 계산값) ──────────────────────────────
    [SerializeField] public float Attack => Mathf.Max(0f, BaseAttack + GetBuffLevel(BuffType.AttackDamage) * 10f);
    [SerializeField] public float AttackSpeed => Mathf.Max(0f, BaseAttackSpeed + GetBuffLevel(BuffType.AttackSpeed) * 0.2f);
    [SerializeField] public float MoveSpeed => Mathf.Max(0f, BaseMoveSpeed + GetBuffLevel(BuffType.MoveSpeed) * 0.5f);
    [SerializeField] public float AttackRange => Mathf.Max(0f, BaseAttackRange + GetBuffLevel(BuffType.AttackRange) * 0.5f);

    // ── 바디 반경 (분리/클램프 계산용) ────────────────────────────
    public float BodyRadius { get; private set; }

    public void SetBodyRadius(float bodyRadius)
    {
        BodyRadius = Mathf.Max(0f, bodyRadius);
    }

    // ── 버프 ───────────────────────────────────────────────────────
    [SerializeField] private List<BuffType> _buffs = new List<BuffType>();
    [SerializeField] private List<int> _buffLevel = new List<int>();
    [SerializeField] private List<float> _buffCooldownRemaining = new List<float>();

    public bool IsStunned => GetBuffLevel(BuffType.Stun) > 0;
    public bool HasTaunt => GetBuffLevel(BuffType.Taunt) > 0;

    // ── 넉백 ───────────────────────────────────────────────────────
    public Vector3 CurrentKnockback { get; private set; }

    // ── 파라미터 / 점수 ────────────────────────────────────────────
    public BattleParameterSet CurrentRawParameters { get; private set; }
    public BattleParameterSet CurrentModifiedParameters { get; private set; }
    public BattleActionScoreSet CurrentScores { get; private set; }
    public BattleActionType TopScoredAction { get; private set; }
    public float TopScoredValue { get; private set; }

    // ── 행동 / 결정 상태 ──────────────────────────────────────────
    public BattleActionType CurrentActionType { get; private set; }
    public string CurrentAction { get; private set; }
    public float KeepBehaving { get; private set; }
    public float ActionTimer { get; private set; }

    // ── 스킬 정보 세터 ─────────────────────────────────────────────
    public void SetSkillInfo(WeaponSkillId skillId, float cooltime, skillType type)
    {
        HaveSkill = skillId;
        SkillCooltime = cooltime;
        SkillType = type;
    }

    // ── 스킬 쿨다운 ────────────────────────────────────────────────
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
        SkillCooldownRemaining = Mathf.Max(0f, SkillCooltime);
    }

    public WeaponSkillId GetSkill() => HaveSkill;
    public skillType GetSkillType() => SkillType;

    // ── 체력/사망 이벤트 ──────────────────────────────────────────
    // BattleRuntimeUnit이 구독하여 HPbar 갱신, 사망 처리를 담당한다.
    public event Action<float> OnHealthChanged;   // float = newHealth
    public event Action OnDied;

    // ── 체력/사망 메서드 ──────────────────────────────────────────
    public void ApplyDamage(float damage)
    {
        if (IsCombatDisabled)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, damage));
        OnHealthChanged?.Invoke(CurrentHealth);

        if (CurrentHealth <= 0f)
        {
            IsCombatDisabled = true;
            CurrentHealth = 0f;
            AttackCooldownRemaining = 0f;
            CurrentAction = "Disabled";
            CurrentActionType = BattleActionType.None;
            SetIdleState();
            OnDied?.Invoke();
        }
    }

    public void ApplyHeal(float heal)
    {
        if (IsCombatDisabled)
            return;

        CurrentHealth = Mathf.Clamp(CurrentHealth + Mathf.Max(0f, heal), 0f, MaxHealth);
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    // ── 이동/공격 플래그 이벤트 ──────────────────────────────────
    // BattleRuntimeUnit이 구독하여 Animator를 업데이트한다.
    public event Action<bool> OnMovingStateChanged;   // bool = isMoving
    public event Action OnAttackTriggered;             // 공격 애니메이션 트리거 시점
    public event Action OnIdleStateEntered;            // 아이들 전환 시점

    // ── 이동/공격 플래그 세터 ─────────────────────────────────────
    public void SetMovementState(bool isMoving)
    {
        IsMoving = isMoving;
        if (isMoving)
            IsAttacking = false;
        OnMovingStateChanged?.Invoke(IsMoving);
    }

    public void SetAttackState(bool isAttacking)
    {
        bool wasAttacking = IsAttacking;
        IsAttacking = isAttacking;
        if (isAttacking)
        {
            IsMoving = false;
            if (!wasAttacking)
                OnAttackTriggered?.Invoke();
        }
    }

    public void SetIdleState()
    {
        IsMoving = false;
        IsAttacking = false;
        OnIdleStateEntered?.Invoke();
    }

    // ── 실행 플랜 위치 세터 ────────────────────────────────────────
    public void SetExecutionPlanPosition(Vector3 desiredPosition, bool hasDesiredPosition)
    {
        PlannedDesiredPosition = desiredPosition;
        HasPlannedDesiredPosition = hasDesiredPosition;
    }

    public void ClearExecutionPlanPosition()
    {
        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;
    }

    // ── 행동/결정 상태 이벤트 ─────────────────────────────────────
    // BattleRuntimeUnit이 구독하여 StatusText를 갱신한다.
    public event Action<BattleActionType, string> OnActionTypeChanged;

    // ── 행동/결정 상태 세터 ────────────────────────────────────────
    public void SetCurrentActionType(BattleActionType actionType, string displayName = null)
    {
        CurrentActionType = actionType;
        CurrentAction = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : (actionType == BattleActionType.None ? "Idle" : actionType.ToString());
        OnActionTypeChanged?.Invoke(CurrentActionType, CurrentAction);
    }

    public void SetCurrentAction(string actionName)
    {
        CurrentAction = string.IsNullOrWhiteSpace(actionName) ? "Idle" : actionName;
        OnActionTypeChanged?.Invoke(CurrentActionType, CurrentAction);
    }

    public void SetDecisionState(float keepBehaving, float actionTimer)
    {
        KeepBehaving = keepBehaving;
        ActionTimer = Mathf.Max(0f, actionTimer);
    }

    // ── 공격 쿨다운 ────────────────────────────────────────────────
    public float AttackCooldownRemaining { get; private set; }

    // ── 스킬 정보 / 스킬 쿨다운 ────────────────────────────────────
    [SerializeField] public WeaponSkillId HaveSkill;
    public float SkillCooltime { get; private set; }
    [SerializeField] public skillType SkillType { get; private set; }
    public float SkillCooldownRemaining { get; private set; }

    // ── 실행 플랜 위치 / 이동-공격 플래그 ─────────────────────────
    public Vector3 PlannedDesiredPosition { get; private set; }
    public bool HasPlannedDesiredPosition { get; private set; }
    public bool IsMoving { get; private set; }
    public bool IsAttacking { get; private set; }

    // ── 생성자 ─────────────────────────────────────────────────────
    public BattleUnitCombatState(BattleUnitSnapshot snapshot, int unitNumber, bool isEnemy)
    {
        UnitNumber = unitNumber;
        IsEnemy = isEnemy;

        DisplayName = snapshot.DisplayName;
        Level = snapshot.Level;

        MaxHealth = snapshot.MaxHealth;
        CurrentHealth = snapshot.CurrentHealth;
        IsCombatDisabled = false;

        BaseAttack = snapshot.Attack;
        BaseAttackSpeed = snapshot.AttackSpeed;
        BaseMoveSpeed = snapshot.MoveSpeed;
        BaseAttackRange = snapshot.AttackRange;

        BodyRadius = 50f;   // SetBodyRadius로 SimManager가 덮어쓴다

        CurrentKnockback = Vector3.zero;

        CurrentRawParameters = default;
        CurrentModifiedParameters = default;
        CurrentScores = default;
        TopScoredAction = BattleActionType.None;
        TopScoredValue = 0f;

        CurrentActionType = BattleActionType.None;
        CurrentAction = "Idle";
        KeepBehaving = 0f;
        ActionTimer = 0f;

        AttackCooldownRemaining = 0f;

        HaveSkill = snapshot.WeaponSkillId;
        SkillCooltime = 0f;
        SkillType = skillType.None;
        SkillCooldownRemaining = 0f;

        PlannedDesiredPosition = Vector3.zero;
        HasPlannedDesiredPosition = false;
        IsMoving = false;
        IsAttacking = false;
    }

    // ── 공격 쿨다운 ────────────────────────────────────────────────
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

    // ── 파라미터 / 점수 세터 ──────────────────────────────────────
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

    // ── 넉백 ───────────────────────────────────────────────────────
    public void AddKnockback(Vector3 forceDirection, float forcePower)
    {
        Vector3 force = forceDirection.normalized * forcePower;
        force.y = 0f;
        CurrentKnockback += force;
    }

    // 넉백 속도를 deltaTime만큼 감소시키고, 이동해야 할 positionDelta를 반환한다.
    // BattleRuntimeUnit이 반환값을 transform에 직접 적용한다.
    public Vector3 ConsumeKnockbackDelta(float deltaTime, float friction = 10f)
    {
        if (CurrentKnockback.sqrMagnitude <= 0.01f)
        {
            CurrentKnockback = Vector3.zero;
            return Vector3.zero;
        }

        Vector3 delta = CurrentKnockback * deltaTime;
        CurrentKnockback = Vector3.Lerp(CurrentKnockback, Vector3.zero, friction * deltaTime);
        return delta;
    }

    // ── 버프 시스템 ────────────────────────────────────────────────
    public void BuffApply(BuffType type, int level, float cool)
    {
        _buffs.Add(type);
        _buffLevel.Add(level);
        _buffCooldownRemaining.Add(cool);
    }

    public void TickBufflCooldown(float deltaTime)
    {
        for (int i = _buffs.Count - 1; i >= 0; i--)
        {
            _buffCooldownRemaining[i] = Mathf.Max(0f, _buffCooldownRemaining[i] - Mathf.Max(0f, deltaTime));

            if (_buffCooldownRemaining[i] <= 0f)
            {
                _buffs.RemoveAt(i);
                _buffLevel.RemoveAt(i);
                _buffCooldownRemaining.RemoveAt(i);
            }
        }

        Debuff(deltaTime);
    }

    private void Debuff(float deltaTime)
    {
        int totalBleedLevel = 0;

        totalBleedLevel = GetBuffLevel(BuffType.BleedDamage);

        if(totalBleedLevel > 0 && !IsCombatDisabled)
        {
            ApplyDamage(totalBleedLevel * 5);
        }
    }



    public int BuffNum() => _buffs.Count;

    public int GetBuffLevel(BuffType type)
    {
        int count = 0;
        for (int i = 0; i < _buffs.Count; i++)
        {
            if (_buffs[i] == type)
                count += _buffLevel[i];
        }
        return count;
    }
}
