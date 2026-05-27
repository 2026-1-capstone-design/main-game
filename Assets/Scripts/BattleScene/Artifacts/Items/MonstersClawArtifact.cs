using UnityEngine;

// 괴수의 발톱: 체력이 50% 이하인 적에게 주는 데미지 증가
// 수치 설정: 적 체력 50% 이하 조건 만족 시 레벨당 15%의 피해량이 곱연산으로 증가하도록 설정했습니다 (1Lv: 15%, 2Lv: 30%, 3Lv: 45%).
public sealed class MonstersClawArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.MonstersClaw;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source != owner || request.Target == null) return;

        float hpRatio = request.Target.CurrentHealth / Mathf.Max(1f, request.Target.MaxHealth);

        if (hpRatio <= 0.5f)
        {
            request.Amount *= 1f + (_level * 0.15f);
        }
    }
}
