public readonly struct GladiatorTacticalFeatures
{
    public readonly float AnchorDistanceRatio;
    public readonly float AnchorVisibility;
    public readonly float AnchorThreatToSelfRatio;
    public readonly float SelfThreatToAnchorRatio;
    public readonly float AnchorInSelfRange;
    public readonly float SelfInAnchorRange;
    public readonly float LeftLaneFreeRatio;
    public readonly float RightLaneFreeRatio;
    public readonly float AllyUnderFocusRatio;
    public readonly float EnemyClusterPressure;
    public readonly float AnchorAllySupportPressure;
    public readonly float AnchorEnemyFocusPressure;
    public readonly float AnchorEnemyIsolation;
    public readonly float AnchorEnemyRetreatSignal;

    public GladiatorTacticalFeatures(
        float anchorDistanceRatio,
        float anchorVisibility,
        float anchorThreatToSelfRatio,
        float selfThreatToAnchorRatio,
        float anchorInSelfRange,
        float selfInAnchorRange,
        float leftLaneFreeRatio,
        float rightLaneFreeRatio,
        float allyUnderFocusRatio,
        float enemyClusterPressure,
        float anchorAllySupportPressure,
        float anchorEnemyFocusPressure,
        float anchorEnemyIsolation,
        float anchorEnemyRetreatSignal
    )
    {
        AnchorDistanceRatio = anchorDistanceRatio;
        AnchorVisibility = anchorVisibility;
        AnchorThreatToSelfRatio = anchorThreatToSelfRatio;
        SelfThreatToAnchorRatio = selfThreatToAnchorRatio;
        AnchorInSelfRange = anchorInSelfRange;
        SelfInAnchorRange = selfInAnchorRange;
        LeftLaneFreeRatio = leftLaneFreeRatio;
        RightLaneFreeRatio = rightLaneFreeRatio;
        AllyUnderFocusRatio = allyUnderFocusRatio;
        EnemyClusterPressure = enemyClusterPressure;
        AnchorAllySupportPressure = anchorAllySupportPressure;
        AnchorEnemyFocusPressure = anchorEnemyFocusPressure;
        AnchorEnemyIsolation = anchorEnemyIsolation;
        AnchorEnemyRetreatSignal = anchorEnemyRetreatSignal;
    }
}
