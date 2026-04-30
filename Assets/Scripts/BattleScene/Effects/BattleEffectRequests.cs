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
        IReadOnlyList<BattleRuntimeUnit> units,
        float battleTime,
        int battleTick
    )
    {
        Actor = actor;
        PrimaryTarget = primaryTarget;
        Snapshot = snapshot;
        Units = units;
        BattleTime = battleTime;
        BattleTick = battleTick;
    }
}

// 모든 피해 적용 요청이 통과하는 공통 DTO다.
// 기본 공격, 스킬, 장신구 피해가 같은 보정/기록 파이프라인을 사용하게 만든다.
public struct BattleDamageRequest
{
    public BattleRuntimeUnit Source;
    public BattleRuntimeUnit Target;
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
    public BattleRuntimeUnit Source;
    public BattleRuntimeUnit Target;
    public float Amount;
    public BattleEffectSourceKind SourceKind;
    public ArtifactId ArtifactId;
}

// 전투 효과가 실제 상태 변경을 요청하는 단일 진입점이다.
// 스킬과 장신구 구현체는 State를 직접 만지는 대신 이 sink를 호출한다.
public interface IBattleEffectSink
{
    void DealDamage(BattleDamageRequest request);
    void Heal(BattleHealRequest request);
    void ApplyBuff(BattleRuntimeUnit source, BattleRuntimeUnit target, BuffType type, int level, float duration);
    void AddKnockback(BattleRuntimeUnit target, Vector3 direction, float force);
}
