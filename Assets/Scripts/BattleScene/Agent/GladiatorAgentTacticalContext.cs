public readonly struct GladiatorAgentTacticalContext
{
    public readonly float PreviousNearestOpponentDistance;
    public readonly float NearestOpponentDistance;
    public readonly float DistanceFromCenter;
    public readonly float PlayableRadius;
    public readonly bool ShouldRetreat;
    public readonly bool HasLivingOpponent;
    public readonly bool HasAttackableOpponent;
    public readonly bool HasValidTarget;
    public readonly bool IsTargetOutOfAttackRange;
    public readonly bool IsAttackBlocked;
    public readonly bool CanRequestSkill;

    public GladiatorAgentTacticalContext(
        float previousNearestOpponentDistance,
        float nearestOpponentDistance,
        float distanceFromCenter,
        float playableRadius,
        bool shouldRetreat,
        bool hasLivingOpponent,
        bool hasAttackableOpponent,
        bool hasValidTarget,
        bool isTargetOutOfAttackRange,
        bool isAttackBlocked,
        bool canRequestSkill
    )
    {
        PreviousNearestOpponentDistance = previousNearestOpponentDistance;
        NearestOpponentDistance = nearestOpponentDistance;
        DistanceFromCenter = distanceFromCenter;
        PlayableRadius = playableRadius;
        ShouldRetreat = shouldRetreat;
        HasLivingOpponent = hasLivingOpponent;
        HasAttackableOpponent = hasAttackableOpponent;
        HasValidTarget = hasValidTarget;
        IsTargetOutOfAttackRange = isTargetOutOfAttackRange;
        IsAttackBlocked = isAttackBlocked;
        CanRequestSkill = canRequestSkill;
    }
}
