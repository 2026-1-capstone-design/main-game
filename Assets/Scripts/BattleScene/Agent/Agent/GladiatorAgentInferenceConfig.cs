using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/ML-Agents/Battle Inference Config")]
public sealed class GladiatorAgentInferenceConfig : ScriptableObject
{
    public ModelAsset model;
    public string behaviorName = "GladiatorBehavior";
    public int contractVersion = GladiatorActionSchema.ContractVersion;
    public int expectedContinuousActions = GladiatorActionSchema.ContinuousSize;
    public int expectedObservationSize = GladiatorObservationSchema.TotalSize;
    public GladiatorControlledSide controlledSide = GladiatorControlledSide.HostileTeam;
    public int decisionPeriod = 1;
    public bool takeActionsBetweenDecisions = true;
    public InferenceDevice inferenceDevice = InferenceDevice.Default;
    public bool deterministicInference;
    public int maxAgentCount = BattleTeamConstants.MaxUnitsPerTeam * 2;

    private void OnValidate()
    {
        decisionPeriod = Mathf.Max(1, decisionPeriod);
        maxAgentCount = Mathf.Clamp(maxAgentCount, 0, BattleTeamConstants.MaxUnitsPerTeam * 2);
        if (string.IsNullOrWhiteSpace(behaviorName))
        {
            behaviorName = "GladiatorBehavior";
        }

        contractVersion = GladiatorActionSchema.ContractVersion;
        expectedContinuousActions = GladiatorActionSchema.ContinuousSize;
        expectedObservationSize = GladiatorObservationSchema.TotalSize;
    }
}
