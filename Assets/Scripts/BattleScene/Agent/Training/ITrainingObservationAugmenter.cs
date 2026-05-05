public interface ITrainingObservationAugmenter
{
    GladiatorTacticalFeatures BuildFeatures(
        GladiatorObservationContext observationContext,
        BattleTrainingContext trainingContext
    );
}
