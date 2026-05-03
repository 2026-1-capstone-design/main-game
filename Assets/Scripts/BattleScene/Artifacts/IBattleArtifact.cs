// 장신구 콘텐츠 식별자다.
// 개별 ID 값은 장신구 목록 구현 단계에서 추가한다.
public enum ArtifactId
{
    None = 0,
}

// 전투 중 장신구 효과 구현체의 공통 계약이다.
// Initialize는 소유자와 초기 전투 문맥을 캐싱해야 하는 효과를 위한 준비 단계다.
public interface IBattleArtifact
{
    ArtifactId ArtifactId { get; }
    void Initialize(BattleUnitCombatState owner, in BattleEffectContext context);
}

// 전투 시작 시 한 번 실행되는 장신구 훅이다.
// 시작 버프, 시작 치유처럼 첫 tick 전에 적용되어야 하는 효과가 구현한다.
public interface IBattleStartArtifactEffect : IBattleArtifact
{
    void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects);
}

// 피해 요청을 최종 적용 전에 수정하는 장신구 훅이다.
// 배율, 조건부 추가 피해, 피해 감소처럼 BattleDamageRequest만으로 끝나는 효과가 구현한다.
public interface IDamageModifierArtifact : IBattleArtifact
{
    void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request);
}

public interface ITargetingModifierArtifact : IBattleArtifact
{
    void ModifyTargetScore(BattleUnitCombatState owner, ref BattleTargetScore score);
    bool CanBeTargeted(BattleUnitCombatState owner, BattleRuntimeUnit requester, BattleTargetingReason reason);
}

public interface IMovementModifierArtifact : IBattleArtifact
{
    void ModifyMoveSpeed(BattleUnitCombatState owner, ref BattleMoveRequest request);
    bool CanIgnoreForcedMovement(BattleUnitCombatState owner, in BattleForcedMovementRequest request);
}

public interface IDamageReactionArtifact : IBattleArtifact
{
    void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects);
}

public interface IHealReactionArtifact : IBattleArtifact
{
    void AfterHeal(BattleUnitCombatState owner, in BattleHealResult result, IBattleEffectSink effects);
}

public interface IKillReactionArtifact : IBattleArtifact
{
    void OnUnitKilled(BattleUnitCombatState owner, in BattleKillEvent killEvent, IBattleEffectSink effects);
}

public interface ISkillCastReactionArtifact : IBattleArtifact
{
    void OnSkillCast(BattleUnitCombatState owner, in BattleSkillCastEvent skillCastEvent, IBattleEffectSink effects);
}

// 다른 장신구 효과를 복제하거나 이전받는 특수 장신구 훅이다.
public interface IArtifactCopyEffect : IBattleArtifact
{
    void CopyFrom(BattleRuntimeUnit owner, BattleRuntimeUnit source, IBattleArtifact copiedEffect);
}

// 기본 공격 대상 선택을 장신구가 강제로 바꿀 수 있게 하는 훅이다.
public interface IAttackRetargetArtifact : IBattleArtifact
{
    bool TryOverrideBasicAttackTarget(
        BattleRuntimeUnit owner,
        BattleFieldSnapshot snapshot,
        out BattleRuntimeUnit target
    );
}

// 위치 히스토리를 사용하는 장신구가 매 틱 과거 위치를 조회할 수 있게 하는 훅이다.
public interface IPositionHistoryArtifact : IBattleArtifact
{
    void TickWithPositionHistory(
        BattleRuntimeUnit owner,
        BattlePositionHistory history,
        in BattleEffectContext context,
        IBattleEffectSink effects
    );
}
