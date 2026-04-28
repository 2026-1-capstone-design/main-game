using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/ML-Agents/Battle Inference Config")]
public sealed class BattleMlAgentInferenceConfig : ScriptableObject
{
    public ModelAsset model;
    public string behaviorName = "GladiatorSmooth";
    public int contractVersion = GladiatorActionSchema.ContractVersion;
    public int expectedContinuousActions = GladiatorActionSchema.ContinuousSize;
    public int expectedObservationSize = GladiatorObservationSchema.TotalSize;
    public BattleMlControlledSide controlledSide = BattleMlControlledSide.HostileTeam;
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
            behaviorName = "GladiatorSmooth";
        }

        contractVersion = GladiatorActionSchema.ContractVersion;
        expectedContinuousActions = GladiatorActionSchema.ContinuousSize;
        expectedObservationSize = GladiatorObservationSchema.TotalSize;
    }
}
