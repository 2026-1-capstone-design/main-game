using System.Collections.Generic;
using UnityEngine;

// 효과가 어디서 발생했는지 구분한다.
// 장신구/스킬별 조건 분기와 전투 로그 확장을 위한 공통 출처 값이다.
public enum BattleEffectSourceKind
{
    BasicAttack,
    Skill,
    Artifact,
    Status,
}

// 피해가 어떤 형태로 적용되는지 구분한다.
// 단일 피해와 광역 피해처럼 같은 수치라도 보정 규칙이 달라질 수 있는 지점을 분리한다.
public enum BattleDamageKind
{
    Direct,
    Area,
    DamageOverTime,
}

// 스킬이 어떤 대상 정책을 기대하는지 외부에서 알 수 있게 하는 메타데이터다.
// 실제 대상 탐색은 현재 전투 계획과 BattleEffectContext를 통해 수행한다.
public enum BattleSkillTargetPolicy
{
    None,
    Self,
    PlannedEnemy,
    PlannedAlly,
    AreaAroundSelf,
    AreaAroundTarget,
}

// 스킬과 장신구 효과 실행 시점의 읽기 전용 전투 문맥이다.
// 효과 구현체가 전역 매니저를 직접 찾지 않고 필요한 대상/스냅샷/시간만 받도록 한다.
public readonly struct BattleEffectContext
{
    public BattleRuntimeUnit Actor { get; }
    public BattleRuntimeUnit PrimaryTarget { get; }
    public BattleFieldSnapshot Snapshot { get; }
    public IReadOnlyList<BattleRuntimeUnit> Units { get; }
    public float BattleTime { get; }
    public int BattleTick { get; }

    public BattleEffectContext(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit primaryTarget,
        BattleFieldSnapshot snapshot,
        IReadOnlyList<BattleRuntimeUnit> unitViews,
        float battleTime,
        int battleTick
    )
    {
        Actor = actor;
        PrimaryTarget = primaryTarget;
        Snapshot = snapshot;
        Units = unitViews;
        BattleTime = battleTime;
        BattleTick = battleTick;
    }
}

// 모든 피해 적용 요청이 통과하는 공통 DTO다.
// 기본 공격, 스킬, 장신구 피해가 같은 보정/기록 파이프라인을 사용하게 만든다.
public struct BattleDamageRequest
{
    public BattleUnitCombatState Source;
    public BattleUnitCombatState Target;
    public float Amount;
    public BattleEffectSourceKind SourceKind;
    public BattleDamageKind DamageKind;
    public WeaponSkillId SkillId;
    public ArtifactId ArtifactId;
    public bool IsBasicAttack;
    public bool IsSkill;
    public bool IsArea;
}

// 치유 효과 요청 DTO다.
// 현재는 즉시 치유만 담지만, 출처와 장신구 ID를 포함해 후속 보정 훅을 붙일 수 있다.
public struct BattleHealRequest
{
    public BattleUnitCombatState Source;
    public BattleUnitCombatState Target;
    public float Amount;
    public BattleEffectSourceKind SourceKind;
    public ArtifactId ArtifactId;
}

// 파티클, 사운드, 모션 같은 표현 계층 요청이다.
// 수치 판정은 State 기반으로 처리하고, 표현이 필요한 경우에만 런타임 뷰를 함께 전달한다.
public readonly struct BattleVisualEffectRequest
{
    public BattleEffectSourceKind SourceKind { get; }
    public WeaponSkillId SkillId { get; }
    public ArtifactId ArtifactId { get; }
    public BattleRuntimeUnit SourceView { get; }
    public BattleRuntimeUnit TargetView { get; }
    public string EffectKey { get; }

    public BattleVisualEffectRequest(
        BattleEffectSourceKind sourceKind,
        WeaponSkillId skillId,
        ArtifactId artifactId,
        BattleRuntimeUnit sourceView,
        BattleRuntimeUnit targetView,
        string effectKey
    )
    {
        SourceKind = sourceKind;
        SkillId = skillId;
        ArtifactId = artifactId;
        SourceView = sourceView;
        TargetView = targetView;
        EffectKey = effectKey;
    }
}

// 전투 효과가 실제 상태 변경을 요청하는 단일 진입점이다.
// 스킬과 장신구 구현체는 State를 직접 만지는 대신 이 sink를 호출한다.
public interface IBattleEffectSink
{
    // 피해를 적용하고 피해 보정, 전투 결과 기록, 피해/킬 반응 훅을 함께 처리한다.
    void DealDamage(BattleDamageRequest request);

    // 치유를 적용하고 치유 후 반응 훅을 호출한다.
    void Heal(BattleHealRequest request);

    // BattleStatusRequest 기반 신규 상태를 적용한다.
    void ApplyStatus(BattleStatusRequest request);

    // 기존 BuffType 호출부를 유지하기 위한 호환 API다.
    // 내부에서는 BattleStatusRequest로 변환되어 신규 상태 저장소에 들어간다.
    void ApplyBuff(
        BattleUnitCombatState source,
        BattleUnitCombatState target,
        BuffType type,
        int level,
        float duration
    );

    // 필터 조건에 맞는 버프/디버프를 즉시 제거한다.
    void Dispel(BattleUnitCombatState target, BattleDispelFilter filter);

    // 필터 조건에 맞는 상태들의 남은 지속시간을 지정값으로 갱신한다.
    void RefreshStatuses(BattleUnitCombatState target, BattleStatusFilter filter, float duration);

    // 전투불능 상태의 유닛을 지정 체력으로 복귀시킨다.
    void Revive(BattleUnitCombatState target, float health);

    // 넉백 힘을 누적한다. 강제 이동 무시 정책이 있으면 적용 전에 차단될 수 있다.
    void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force);

    // 대상 유닛을 지정 월드 좌표로 이동시키고 전장 경계 안으로 보정한다.
    void Teleport(BattleUnitCombatState target, Vector3 destination);

    // source 쪽으로 target을 끌어오되 source와 stopDistance만큼 거리를 남긴다.
    void PullTo(BattleUnitCombatState source, BattleUnitCombatState target, float stopDistance);

    // target을 전장 가장자리로 밀어내고 필요하면 짧은 둔화 상태를 부여한다.
    void PushToArenaEdge(BattleUnitCombatState source, BattleUnitCombatState target, float slowDuration);

    // 수치 판정과 분리된 시각/청각 표현 요청을 전달한다.
    void PlayVisual(BattleVisualEffectRequest request);
}
