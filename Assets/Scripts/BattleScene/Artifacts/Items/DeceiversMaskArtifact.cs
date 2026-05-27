using UnityEngine;

// 기만자 가면: 적의 타겟 우선순위에서 살짝 밀림 (1레벨)
// 수치 설정: 타겟팅 계산 시 본인의 타겟 점수를 100점 깎아서, 근처에 다른 아군이 있다면 적들이 다른 아군을 우선적으로 때리도록 유도합니다. (참고: 불온한 시선은 +1000점이었습니다)
public sealed class DeceiversMaskArtifact : ITargetingModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.DeceiversMask;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyTargetScore(BattleUnitCombatState owner, ref BattleTargetScore score)
    {
        if (score.Candidate != null && score.Candidate.State == owner && score.Requester.TeamId != owner.TeamId)
        {
            score.Value -= 100f;
        }
    }

    public bool CanBeTargeted(BattleUnitCombatState owner, BattleRuntimeUnit requester, BattleTargetingReason reason)
    {
        return true;
    }
}
