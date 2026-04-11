using System.Collections.Generic;
using UnityEngine;

// BattleUnitCombatState: 순수 전투 상태 컨테이너 (MonoBehaviour 없음, Unity 씬 의존 없음)
// BattleRuntimeUnit이 소유하며, BattleSimulationManager가 이 객체를 통해 전투 상태를 읽고 쓴다.
// Animator, UI, Transform 등 비주얼 관련 로직은 포함하지 않는다.
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
    public float BaseAttack { get; private set; }
    public float BaseAttackSpeed { get; private set; }
    public float BaseMoveSpeed { get; private set; }
    public float BaseAttackRange { get; private set; }

    // ── 실효 스탯 (버프 반영 계산값) ──────────────────────────────
    public float Attack => Mathf.Max(0f, BaseAttack + GetBuffLevel(BuffType.AttackDamage) * 10f);
    public float AttackSpeed => Mathf.Max(0f, BaseAttackSpeed + GetBuffLevel(BuffType.AttackSpeed) * 0.2f);
    public float MoveSpeed => Mathf.Max(0f, BaseMoveSpeed + GetBuffLevel(BuffType.MoveSpeed) * 0.5f);
    public float AttackRange => Mathf.Max(0f, BaseAttackRange + GetBuffLevel(BuffType.AttackRange) * 0.5f);

    // ── 바디 반경 (분리/클램프 계산용) ────────────────────────────
    public float BodyRadius { get; private set; } = 50f;

    // ── 버프 ───────────────────────────────────────────────────────
    private readonly List<BuffType> _buffs = new List<BuffType>();
    private readonly List<int> _buffLevel = new List<int>();
    private readonly List<float> _buffCooldownRemaining = new List<float>();

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

    // ── 공격 쿨다운 ────────────────────────────────────────────────
    public float AttackCooldownRemaining { get; private set; }

    // ── 스킬 정보 / 스킬 쿨다운 ────────────────────────────────────
    public WeaponSkillId HaveSkill { get; private set; }
    public float SkillCooltime { get; private set; }
    public skillType SkillType { get; private set; }
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

        BodyRadius = 50f;

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

    // ── 버프 조회 (Attack/Speed 등 계산에 사용) ───────────────────
    public int GetBuffLevel(BuffType type)
    {
        int count = 0;
        for (int i = 0; i < _buffs.Count; i++)
        {
            if (_buffs[i] == type)
                count++;
        }
        return count;
    }
}
