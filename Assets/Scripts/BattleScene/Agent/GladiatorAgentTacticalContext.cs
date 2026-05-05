public readonly struct GladiatorAgentTacticalContext
{
    public readonly int PreviousTargetSlot;
    public readonly int TargetSlot;
    public readonly int PreviousStance;
    public readonly int Stance;
    public readonly float PreviousTargetDistance;
    public readonly float TargetDistance;
    public readonly float TargetEffectiveRange;
    public readonly float DistanceFromCenter;
    public readonly float PlayableRadius;
    public readonly bool HasLivingOpponent;
    public readonly bool HasAttackableOpponent;
    public readonly bool HasValidTarget;
    public readonly bool IsTargetOutOfAttackRange;
    public readonly bool IsAttackBlocked;

    public GladiatorAgentTacticalContext(
        int previousTargetSlot,
        int targetSlot,
        int previousStance,
        int stance,
        float previousTargetDistance,
        float targetDistance,
        float targetEffectiveRange,
        float distanceFromCenter,
        float playableRadius,
        bool hasLivingOpponent,
        bool hasAttackableOpponent,
        bool hasValidTarget,
        bool isTargetOutOfAttackRange,
        bool isAttackBlocked
    )
    {
        PreviousTargetSlot = previousTargetSlot;
        TargetSlot = targetSlot;
        PreviousStance = previousStance;
        Stance = stance;
        PreviousTargetDistance = previousTargetDistance;
        TargetDistance = targetDistance;
        TargetEffectiveRange = targetEffectiveRange;
        DistanceFromCenter = distanceFromCenter;
        PlayableRadius = playableRadius;
        HasLivingOpponent = hasLivingOpponent;
        HasAttackableOpponent = hasAttackableOpponent;
        HasValidTarget = hasValidTarget;
        IsTargetOutOfAttackRange = isTargetOutOfAttackRange;
        IsAttackBlocked = isAttackBlocked;
    }
}
