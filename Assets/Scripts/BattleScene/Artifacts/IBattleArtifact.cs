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
