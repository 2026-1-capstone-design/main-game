using System.Collections.Generic;
using UnityEngine;

// 거인 학살자: 체력이 가장 높은 적 두 명에게 추가 % 피해
// 수치 설정: 공격 시점에 살아있는 적들 중 '최대 체력'이 가장 높은 상위 2명을 판별하고, 해당 적들을 공격할 때만 레벨당 15%의 추가 피해(1Lv: 15%, 2Lv: 30%, 3Lv: 45%)를 입히도록 설정했습니다.
public sealed class GiantSlayerArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.GiantSlayer;

    private int _level;
    private IReadOnlyList<BattleRuntimeUnit> _units;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
        _units = context.Units;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source == owner && request.Target != null && _units != null)
        {
            float highestHp = -1f;
            float secondHighestHp = -1f;
            BattleUnitCombatState top1 = null;
            BattleUnitCombatState top2 = null;

            for (int i = 0; i < _units.Count; i++)
            {
                BattleRuntimeUnit unit = _units[i];
                if (unit == null || unit.State == null || unit.TeamId == owner.TeamId || unit.IsCombatDisabled)
                    continue;

                float maxHp = unit.State.MaxHealth;
                if (maxHp > highestHp)
                {
                    secondHighestHp = highestHp;
                    top2 = top1;

                    highestHp = maxHp;
                    top1 = unit.State;
                }
                else if (maxHp > secondHighestHp)
                {
                    secondHighestHp = maxHp;
                    top2 = unit.State;
                }
            }

            if (request.Target == top1 || request.Target == top2)
            {
                request.Amount *= 1f + (_level * 0.15f);
            }
        }
    }
}
