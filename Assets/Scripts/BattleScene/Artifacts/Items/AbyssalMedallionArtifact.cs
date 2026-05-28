using UnityEngine;

// 심연의 메달: 적이 가진 디버프 개수마다 주는 피해 % 증가(최대 디버프 5개)
// 수치 설정: 타겟이 보유한 '디버프' 판정의 상태 이상 1개당 레벨별로 5%의 추가 피해를 주도록 설정했으며, 과도한 중첩을 막기 위해 최대 5개(최대 증가량 1Lv: 25%, 2Lv: 50%, 3Lv: 75%)까지만 반영되도록 제한했습니다.
public sealed class AbyssalMedallionArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.AbyssalMedallion;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source == owner && request.Target != null)
        {
            int debuffCount = 0;
            var statuses = request.Target.ActiveStatuses;

            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsDebuff)
                {
                    debuffCount++;
                }
            }

            int clampedDebuffCount = Mathf.Min(5, debuffCount);
            if (clampedDebuffCount > 0)
            {
                request.Amount *= 1f + (clampedDebuffCount * _level * 0.05f);
            }
        }
    }
}
