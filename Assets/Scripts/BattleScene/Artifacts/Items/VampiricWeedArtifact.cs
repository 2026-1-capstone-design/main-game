using UnityEngine;

// 흡혈초: 본인이 주는 데미지의 % 흡혈
// 수치 설정: 적에게 실제로 입힌 최종 피해량(데미지 감소 로직 등이 모두 적용된 후의 실제 깎인 체력값)을 기준으로 레벨당 8%(1Lv: 8%, 2Lv: 16%, 3Lv: 24%)를 본인의 체력으로 즉시 회복하도록 설정했습니다.
public sealed class VampiricWeedArtifact : IDamageReactionArtifact
{
    public ArtifactId ArtifactId => ArtifactId.VampiricWeed;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void AfterDamage(BattleUnitCombatState owner, in BattleDamageResult result, IBattleEffectSink effects)
    {
        if (result.Source == owner && result.FinalAmount > 0f)
        {
            float healAmount = result.FinalAmount * (_level * 0.08f);

            effects.Heal(new BattleHealRequest
            {
                Source = owner,
                Target = owner,
                Amount = healAmount,
                SourceKind = BattleEffectSourceKind.Artifact,
                ArtifactId = this.ArtifactId
            });
        }
    }
}
