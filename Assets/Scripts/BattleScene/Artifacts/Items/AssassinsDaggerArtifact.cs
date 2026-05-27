using System.Collections.Generic;
using UnityEngine;

// 암살 비수: 자신을 공격하는 적이 1명 이하일 때 공격력 % 증가 (1, 2, 3레벨)
// 수치 설정: 본인을 현재 타겟으로 삼고 있는 적 유닛을 계산하여, 1명 이하일 경우 레벨당 15%의 추가 피해(1Lv: 15%, 2Lv: 30%, 3Lv: 45%)를 곱연산으로 주도록 설정했습니다.
public sealed class AssassinsDaggerArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.AssassinsDagger;

    private int _level;
    private IReadOnlyList<BattleRuntimeUnit> _units;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
        _units = context.Units;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source != owner || _units == null) return;

        int targetingEnemies = 0;

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit unit = _units[i];
            if (unit == null || unit.State == null || unit.IsCombatDisabled || unit.TeamId == owner.TeamId)
                continue;

            if (unit.State.CurrentTarget == owner)
            {
                targetingEnemies++;
            }
        }

        if (targetingEnemies <= 1)
        {
            request.Amount *= 1f + (_level * 0.15f);
        }
    }
}
