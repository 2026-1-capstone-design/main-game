using UnityEngine;

// 부서진 왕관: 전투 시작 후 20초간 공격력 소폭 % 증가
// 수치 설정: 버프 지속시간은 20초로 고정하였으며, 적용되는 동안 레벨당 10%의 피해량이 증가하도록 설정했습니다 (1Lv: 10%, 2Lv: 20%, 3Lv: 30%).
public sealed class BrokenCrownArtifact : IBattleStartArtifactEffect
{
    public ArtifactId ArtifactId => ArtifactId.BrokenCrown;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.GrantTemporaryArtifact(context.Actor, new BrokenCrownBuffArtifact(_level), 20f, context);
    }
}

public sealed class BrokenCrownBuffArtifact : IDamageModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.None;
    private int _level;

    public BrokenCrownBuffArtifact(int level)
    {
        _level = level;
    }

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context) { }

    public void ModifyDamage(BattleUnitCombatState owner, ref BattleDamageRequest request)
    {
        if (request.Source == owner)
        {
            request.Amount *= 1f + (_level * 0.1f);
        }
    }
}
