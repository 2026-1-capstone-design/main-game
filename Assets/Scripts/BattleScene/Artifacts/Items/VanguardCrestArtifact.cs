using System.Collections.Generic;
using UnityEngine;

// 돌격대 문장: 범위 5 내에 아군보다 상대가 많을수록 공격력 증가(1명 차이당 Level*8)
// 수치 설정: 범위는 반경 5.0f(거리 제곱 25f)로 고정하였으며, 차이나는 적 1명당 레벨 곱하기 8의 고정 수치 데미지가 더해지도록 설정했습니다 (1명 차이 기준 1Lv: +8, 2Lv: +16, 3Lv: +24).
public sealed class VanguardCrestArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.VanguardCrest;

    private int _level;
    private IReadOnlyList<BattleRuntimeUnit> _units;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
        _units = context.Units;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source != owner || _units == null)
            return;

        int allyCount = 0;
        int enemyCount = 0;
        float radiusSqr = 25f;

        for (int i = 0; i < _units.Count; i++)
        {
            BattleRuntimeUnit unit = _units[i];
            if (unit == null || unit.State == null || unit.IsCombatDisabled || unit.State == owner)
                continue;

            if ((unit.Position - owner.Position).sqrMagnitude <= radiusSqr)
            {
                if (unit.TeamId == owner.TeamId)
                    allyCount++;
                else
                    enemyCount++;
            }
        }

        int difference = enemyCount - allyCount;
        if (difference > 0)
        {
            request.Amount += difference * _level * 8f;
        }
    }
}
