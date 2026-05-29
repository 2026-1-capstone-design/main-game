using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

// GladiatorAgent prefab에 저장된 ML-Agents 설정이 현재 코드의 observation/action 계약과 맞는지 검증하고,
// 전투마다 달라지는 TeamId/DecisionStep만 런타임에 주입한다.
public static class GladiatorAgentContract
{
    private static readonly int[] ExpectedDiscreteBranches =
    {
        GladiatorActionSchema.CommandBranchSize,
        GladiatorActionSchema.StrategyBranchSize,
        GladiatorActionSchema.AnchorActionBranchSize,
    };

    public static bool TryApplyRuntimeOverrides(
        GladiatorAgent agent,
        BattleRuntimeUnit unit,
        int agentIndex,
        bool requireModel,
        Object logContext,
        string logPrefix
    )
    {
        if (
            !TryGetRequiredComponents(
                agent,
                out BehaviorParameters behaviorParameters,
                out DecisionRequester decisionRequester,
                logContext,
                logPrefix
            )
        )
        {
            return false;
        }

        if (!ValidatePrefabContract(agent, behaviorParameters, decisionRequester, requireModel, logContext, logPrefix))
        {
            return false;
        }

        behaviorParameters.TeamId = unit != null ? unit.TeamId.GetHashCode() : 0;
        decisionRequester.DecisionStep = agentIndex % Mathf.Max(1, decisionRequester.DecisionPeriod);
        return true;
    }

    public static bool ValidatePrefab(GladiatorAgent agent, bool requireModel, Object logContext, string logPrefix)
    {
        return TryGetRequiredComponents(
                agent,
                out BehaviorParameters behaviorParameters,
                out DecisionRequester decisionRequester,
                logContext,
                logPrefix
            )
            && ValidatePrefabContract(
                agent,
                behaviorParameters,
                decisionRequester,
                requireModel,
                logContext,
                logPrefix
            );
    }

    private static bool TryGetRequiredComponents(
        GladiatorAgent agent,
        out BehaviorParameters behaviorParameters,
        out DecisionRequester decisionRequester,
        Object logContext,
        string logPrefix
    )
    {
        behaviorParameters = null;
        decisionRequester = null;
        if (agent == null)
        {
            Debug.LogError($"{logPrefix} Agent prefab is not assigned.", logContext);
            return false;
        }

        behaviorParameters = agent.GetComponent<BehaviorParameters>();
        decisionRequester = agent.GetComponent<DecisionRequester>();
        if (behaviorParameters == null)
        {
            Debug.LogError($"{logPrefix} Agent prefab is missing BehaviorParameters.", agent);
            return false;
        }

        if (decisionRequester == null)
        {
            Debug.LogError($"{logPrefix} Agent prefab is missing DecisionRequester.", agent);
            return false;
        }

        return true;
    }

    private static bool ValidatePrefabContract(
        GladiatorAgent agent,
        BehaviorParameters behaviorParameters,
        DecisionRequester decisionRequester,
        bool requireModel,
        Object logContext,
        string logPrefix
    )
    {
        if (string.IsNullOrWhiteSpace(behaviorParameters.BehaviorName))
        {
            Debug.LogError($"{logPrefix} Behavior name is empty on GladiatorAgent prefab.", agent);
            return false;
        }

        if (behaviorParameters.BrainParameters.VectorObservationSize != GladiatorObservationSchema.TotalSize)
        {
            Debug.LogError(
                $"{logPrefix} Observation size mismatch. Expected {GladiatorObservationSchema.TotalSize}, actual {behaviorParameters.BrainParameters.VectorObservationSize}.",
                agent
            );
            return false;
        }

        ActionSpec actionSpec = behaviorParameters.BrainParameters.ActionSpec;
        if (actionSpec.NumContinuousActions != GladiatorActionSchema.ContinuousSize)
        {
            Debug.LogError(
                $"{logPrefix} Continuous action count mismatch. Expected {GladiatorActionSchema.ContinuousSize}, actual {actionSpec.NumContinuousActions}.",
                agent
            );
            return false;
        }

        if (!MatchesExpectedBranches(actionSpec.BranchSizes))
        {
            Debug.LogError($"{logPrefix} Discrete action branches do not match GladiatorActionSchema.", agent);
            return false;
        }

        if (requireModel && behaviorParameters.Model == null)
        {
            Debug.LogError($"{logPrefix} BehaviorParameters model is missing on GladiatorAgent prefab.", logContext);
            return false;
        }

        if (decisionRequester.DecisionPeriod < 1)
        {
            Debug.LogError(
                $"{logPrefix} DecisionRequester.DecisionPeriod must be >= 1 on GladiatorAgent prefab.",
                agent
            );
            return false;
        }

        return true;
    }

    private static bool MatchesExpectedBranches(int[] branchSizes)
    {
        if (branchSizes == null || branchSizes.Length != ExpectedDiscreteBranches.Length)
        {
            return false;
        }

        for (int i = 0; i < ExpectedDiscreteBranches.Length; i++)
        {
            if (branchSizes[i] != ExpectedDiscreteBranches[i])
            {
                return false;
            }
        }

        return true;
    }
}
