using UnityEngine;

// 거인의 머리장식: 전투 참여 아군이 적을수록 공격력, 체력 기하급수적으로 증가 (1, 2레벨)
// 수치 설정: 출전한 최대 인원(5명)을 기준으로, 빈자리 1명당 그 수치의 제곱에 비례하여 스탯이 오르도록 계산했습니다. 출전 아군이 본인 1명일 때 최대치(1Lv: 80%, 2Lv: 160%)의 체력과 피해량 증가 효과를 얻습니다.
public sealed class GiantsHeaddressArtifact : IBattleStartArtifactEffect, IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.GiantsHeaddress;

    private int _level;
    private int _missingAllies;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;

        int allyCount = 0;
        for (int i = 0; i < context.Units.Count; i++)
        {
            if (context.Units[i] != null && context.Units[i].TeamId == owner.TeamId)
            {
                allyCount++;
            }
        }

        // 5인 파티를 기준으로 부족한 인원수 계산 (최소 0)
        _missingAllies = Mathf.Max(0, 5 - allyCount);
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        if (_missingAllies > 0)
        {
            int hpBuffPercent = _missingAllies * _missingAllies * 5 * _level;

            effects.ApplyStatus(
                new BattleStatusRequest
                {
                    Source = owner,
                    Target = owner,
                    Type = BattleStatusType.HP,
                    Level = hpBuffPercent,
                    Duration = 9999f,
                    IsDebuff = false,
                    IsDispelAllowed = false,
                }
            );
        }
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source == owner && _missingAllies > 0)
        {
            float attackBonus = (_missingAllies * _missingAllies * 0.05f) * _level;
            request.Amount *= 1f + attackBonus;
        }
    }
}
