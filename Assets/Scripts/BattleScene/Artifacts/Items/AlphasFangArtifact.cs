using UnityEngine;

// 알파의 송곳니: 상대 체력이 낮을수록 % 추가뎀
// 수치 설정: 적의 잃은 체력 100%를 기준으로 레벨당 최대 20%의 추가 피해 비율이 곱해지도록 설정했습니다 (1Lv: 최대 20%, 2Lv: 최대 40%, 3Lv: 최대 60%).
public sealed class AlphasFangArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.AlphasFang;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source != owner || request.Target == null) return;

        float hpRatio = request.Target.CurrentHealth / Mathf.Max(1f, request.Target.MaxHealth);
        float lostHpRatio = Mathf.Clamp01(1f - hpRatio);

        request.Amount *= 1f + (lostHpRatio * (_level * 0.2f));
    }
}
