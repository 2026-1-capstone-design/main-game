using UnityEngine;

// 망자의 청동반지: 체력이 낮을수록 받는피해 감소(최대 효과는 잔존 체력 30%에서 피해 30% 감소)
// 수치 설정: 유닛의 체력 비율이 100%에서 30% 이하로 떨어지는 구간에 비례하여 0% ~ 30%의 받는 피해 감소가 연속적으로 적용되도록 계산했습니다 (1레벨 기준 최대 감소치 30%).
public sealed class BronzeRingOfTheDeadArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.BronzeRingOfTheDead;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Target == owner)
        {
            float hpRatio = owner.CurrentHealth / Mathf.Max(1f, owner.MaxHealth);

            // hpRatio가 1.0(100%)일 때 missingRatio는 0
            // hpRatio가 0.3(30%) 이하일 때 missingRatio는 1.0으로 고정
            float missingRatio = Mathf.Clamp01((1f - hpRatio) / 0.7f);
            float damageReduction = missingRatio * 0.3f;

            request.Amount *= (1f - damageReduction);
        }
    }
}
