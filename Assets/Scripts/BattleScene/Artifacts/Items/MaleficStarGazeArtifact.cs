using UnityEngine;

// 흉성의 시선: 받는 피해가 증가하고, 공격력이 증가한다. (1, 2, 3레벨)
public sealed class MaleficStarGazeArtifact : IBattleStartArtifactEffect
{
    public ArtifactId ArtifactId => ArtifactId.MaleficStarGaze;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = owner,
                Target = owner,
                Type = BattleStatusType.DamageTakenPercent,
                Level = _level * 15,
                Duration = 9999f,
                IsDebuff = true,
                IsDispelAllowed = false,
            }
        );

        effects.ApplyStatus(
            new BattleStatusRequest
            {
                Source = owner,
                Target = owner,
                Type = BattleStatusType.AttackDamage,
                Level = _level * 2,
                Duration = 9999f,
                IsDebuff = false,
                IsDispelAllowed = false,
            }
        );
    }
}
