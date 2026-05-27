using UnityEngine;

// 독수리 깃펜: 적 처치 시 7초간 공격속도와 이동속도 크게 증가 (1, 2, 3레벨)
// 수치 설정: 적을 처치할 때마다 7초간 이동속도와 공격속도를 레벨당 3(기존 장신구들 대비 큰 수치)만큼 즉시 부여하는 버프를 걸어주도록 설정했습니다 (1Lv: 3, 2Lv: 6, 3Lv: 9).
public sealed class EagleQuillArtifact : IKillReactionArtifact
{
    public ArtifactId ArtifactId => ArtifactId.EagleQuill;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnUnitKilled(BattleUnitCombatState owner, in BattleKillEvent killEvent, IBattleEffectSink effects)
    {
        if (killEvent.Killer == owner)
        {
            effects.ApplyStatus(
                new BattleStatusRequest
                {
                    Source = owner,
                    Target = owner,
                    Type = BattleStatusType.MoveSpeed,
                    Level = _level * 3,
                    Duration = 7f,
                    IsDebuff = false,
                    IsDispelAllowed = true,
                }
            );

            effects.ApplyStatus(
                new BattleStatusRequest
                {
                    Source = owner,
                    Target = owner,
                    Type = BattleStatusType.AttackSpeed,
                    Level = _level * 3,
                    Duration = 7f,
                    IsDebuff = false,
                    IsDispelAllowed = true,
                }
            );
        }
    }
}
