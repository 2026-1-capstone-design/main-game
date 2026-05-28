using UnityEngine;

public sealed class GreenAmberArtifact : IBattleStartArtifactEffect
{
    // 프로젝트에 추가하신 실제 ID로 변경해주세요.
    public ArtifactId ArtifactId => ArtifactId.GreenAmber;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        // 레벨당 10% 증가 (1레벨: 10, 2레벨: 20, 3레벨: 30)
        int healthBonusPercent = _level * 10;

        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = owner,
                Target = owner,
                // 프로젝트의 최대 체력 퍼센트 증가 버프 타입으로 이름을 맞춰주세요.
                Type = BattleStatusType.HP,
                Level = healthBonusPercent, // Level 자체가 1당 1%로 동작하도록 전달
                Duration = 9999f, // 무한 지속 (또는 프로젝트 내 영구 지속 상수 사용)
                IsDebuff = false,
                IsDispelAllowed = false, // 해제 불가능한 고유 효과로 설정
            }
        );
    }
}
