using UnityEngine;

// 불온한 시선: 적에게 더 많이 노려진다. (1레벨)
public sealed class OminousGazeArtifact : ITargetingModifierArtifact
{
    public ArtifactId ArtifactId => ArtifactId.OminousGaze;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void ModifyTargetScore(BattleUnitCombatState owner, ref BattleTargetScore score)
    {
        if (score.Candidate != null && score.Candidate.State == owner && score.Requester.TeamId != owner.TeamId)
        {
            score.Value += 1000f;
        }
    }

    public bool CanBeTargeted(BattleUnitCombatState owner, BattleRuntimeUnit requester, BattleTargetingReason reason)
    {
        return true;
    }
}
