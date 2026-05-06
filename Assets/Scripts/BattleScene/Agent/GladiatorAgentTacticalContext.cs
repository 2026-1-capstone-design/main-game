public readonly struct GladiatorAgentTacticalContext
{
    public readonly int PreviousRole;
    public readonly int Role;
    public readonly int AnchorKind;
    public readonly int PreviousTargetSlot;
    public readonly int TargetSlot;
    public readonly int PreviousStance;
    public readonly int Stance;
    public readonly int AnchorCommitmentSteps;
    public readonly int RoleCommitmentSteps;
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
    public readonly bool BrokeCommitmentEarly;
    public readonly bool CompletedRoleWindow;
    public readonly bool CompletedAnchorWindow;

    public GladiatorAgentTacticalContext(
        int previousRole,
        int role,
        int anchorKind,
        int previousTargetSlot,
        int targetSlot,
        int previousStance,
        int stance,
        int anchorCommitmentSteps,
        int roleCommitmentSteps,
        float previousTargetDistance,
        float targetDistance,
        float targetEffectiveRange,
        float distanceFromCenter,
        float playableRadius,
        bool hasLivingOpponent,
        bool hasAttackableOpponent,
        bool hasValidTarget,
        bool isTargetOutOfAttackRange,
        bool isAttackBlocked,
        bool brokeCommitmentEarly,
        bool completedRoleWindow,
        bool completedAnchorWindow
    )
    {
        PreviousRole = previousRole;
        Role = role;
        AnchorKind = anchorKind;
        PreviousTargetSlot = previousTargetSlot;
        TargetSlot = targetSlot;
        PreviousStance = previousStance;
        Stance = stance;
        AnchorCommitmentSteps = anchorCommitmentSteps;
        RoleCommitmentSteps = roleCommitmentSteps;
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
        BrokeCommitmentEarly = brokeCommitmentEarly;
        CompletedRoleWindow = completedRoleWindow;
        CompletedAnchorWindow = completedAnchorWindow;
    }
}
