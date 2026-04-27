using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSceneMlAgentBinder : MonoBehaviour
{
    private static readonly int[] ExpectedDiscreteBranches =
    {
        GladiatorActionSchema.IntentBranchSize,
        GladiatorActionSchema.MoveBranchSize,
        GladiatorActionSchema.TargetBranchSize,
        GladiatorActionSchema.RotationBranchSize,
    };

    [SerializeField]
    private BattleSceneFlowManager flowManager;

    [SerializeField]
    private BattleMlAgentInferenceConfig config;

    [SerializeField]
    private GladiatorAgent agentPrefab;

    [SerializeField]
    private Transform agentHostParent;

    [SerializeField]
    private bool bindAlreadySpawnedUnitsOnEnable = true;

    private readonly List<GladiatorAgent> _agentPool = new List<GladiatorAgent>();
    private readonly List<BattleRuntimeUnit> _boundUnits = new List<BattleRuntimeUnit>();

    private void OnValidate()
    {
        if (agentHostParent == null)
        {
            agentHostParent = transform;
        }
    }

    private void Awake()
    {
        ResolveSceneReferences();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();

        if (flowManager != null)
        {
            flowManager.OnUnitsSpawned -= HandleUnitsSpawned;
            flowManager.OnUnitsSpawned += HandleUnitsSpawned;
        }

        if (bindAlreadySpawnedUnitsOnEnable && HasRuntimeUnits())
        {
            BindSpawnedUnits();
        }
    }

    private void OnDisable()
    {
        if (flowManager != null)
        {
            flowManager.OnUnitsSpawned -= HandleUnitsSpawned;
        }

        UnbindUnits();
        DeactivateSurplusAgents(0);
    }

    private void HandleUnitsSpawned()
    {
        BindSpawnedUnits();
    }

    private void BindSpawnedUnits()
    {
        UnbindUnits();

        if (!ValidateConfig())
        {
            DeactivateSurplusAgents(0);
            return;
        }

        BattleStartPayload payload = flowManager.CurrentPayload;
        if (payload == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Cannot bind agents. Battle payload is missing.", this);
            DeactivateSurplusAgents(0);
            return;
        }

        BattleRosterProjection projection = new BattleRosterProjection(payload);
        List<BattleRuntimeUnit> controlledUnits = ResolveControlledUnits(payload, projection);
        int bindCount = Mathf.Min(controlledUnits.Count, config.maxAgentCount);
        EnsureAgentPool(bindCount);

        for (int i = 0; i < bindCount; i++)
        {
            GladiatorAgent agent = _agentPool[i];
            BattleRuntimeUnit unit = controlledUnits[i];
            if (!ConfigureAgentHost(agent, unit, i))
            {
                continue;
            }

            agent.gameObject.SetActive(true);
            agent.Initialize(unit, flowManager, null);
            _boundUnits.Add(unit);
        }

        DeactivateSurplusAgents(bindCount);
    }

    private bool ConfigureAgentHost(GladiatorAgent agent, BattleRuntimeUnit unit, int agentIndex)
    {
        if (agent == null || unit == null)
        {
            return false;
        }

        BehaviorParameters behaviorParameters = agent.GetComponent<BehaviorParameters>();
        DecisionRequester decisionRequester = agent.GetComponent<DecisionRequester>();
        if (behaviorParameters == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Agent host is missing BehaviorParameters.", agent);
            return false;
        }

        if (decisionRequester == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Agent host is missing DecisionRequester.", agent);
            return false;
        }

        behaviorParameters.BehaviorName = config.behaviorName;
        behaviorParameters.Model = config.model;
        behaviorParameters.InferenceDevice = config.inferenceDevice;
        behaviorParameters.BehaviorType = BehaviorType.Default;
        behaviorParameters.DeterministicInference = config.deterministicInference;
        behaviorParameters.TeamId = unit.TeamId.GetHashCode();
        behaviorParameters.UseChildSensors = false;
        behaviorParameters.UseChildActuators = true;
        behaviorParameters.BrainParameters.VectorObservationSize = GladiatorObservationSchema.TotalSize;
        behaviorParameters.BrainParameters.ActionSpec = new ActionSpec(0, (int[])ExpectedDiscreteBranches.Clone());

        decisionRequester.DecisionPeriod = Mathf.Max(1, config.decisionPeriod);
        decisionRequester.DecisionStep = agentIndex % decisionRequester.DecisionPeriod;
        decisionRequester.TakeActionsBetweenDecisions = config.takeActionsBetweenDecisions;

        agent.SetModel(config.behaviorName, config.model, config.inferenceDevice);
        return ValidateAgentContract(agent, behaviorParameters, decisionRequester);
    }

    private bool ValidateConfig()
    {
        if (config == null)
        {
            Debug.LogWarning(
                "[BattleSceneMlAgentBinder] Inference config is not assigned. ML agent binding skipped.",
                this
            );
            return false;
        }

        if (config.controlledSide == BattleMlControlledSide.None)
        {
            return false;
        }

        if (config.model == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Inference config model is not assigned.", config);
            return false;
        }

        if (agentPrefab == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Agent prefab is not assigned.", this);
            return false;
        }

        if (flowManager == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] BattleSceneFlowManager is not assigned.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.behaviorName))
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Behavior name is empty.", config);
            return false;
        }

        if (config.decisionPeriod < 1)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Decision period must be >= 1.", config);
            return false;
        }

        return true;
    }

    private bool ValidateAgentContract(
        GladiatorAgent agent,
        BehaviorParameters behaviorParameters,
        DecisionRequester decisionRequester
    )
    {
        if (behaviorParameters.BehaviorName != config.behaviorName)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Behavior name does not match inference config.", agent);
            return false;
        }

        if (behaviorParameters.BrainParameters.VectorObservationSize != GladiatorObservationSchema.TotalSize)
        {
            Debug.LogError(
                $"[BattleSceneMlAgentBinder] Observation size mismatch. Expected {GladiatorObservationSchema.TotalSize}, actual {behaviorParameters.BrainParameters.VectorObservationSize}.",
                agent
            );
            return false;
        }

        ActionSpec actionSpec = behaviorParameters.BrainParameters.ActionSpec;
        if (actionSpec.NumContinuousActions != 0)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] Continuous actions must be 0.", agent);
            return false;
        }

        if (!MatchesExpectedBranches(actionSpec.BranchSizes))
        {
            Debug.LogError(
                "[BattleSceneMlAgentBinder] Discrete action branches do not match GladiatorActionSchema.",
                agent
            );
            return false;
        }

        if (behaviorParameters.Model == null)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] BehaviorParameters model is missing.", agent);
            return false;
        }

        if (decisionRequester.DecisionPeriod < 1)
        {
            Debug.LogError("[BattleSceneMlAgentBinder] DecisionRequester.DecisionPeriod must be >= 1.", agent);
            return false;
        }

        return true;
    }

    private List<BattleRuntimeUnit> ResolveControlledUnits(
        BattleStartPayload payload,
        BattleRosterProjection projection
    )
    {
        var controlledUnits = new List<BattleRuntimeUnit>();

        if (
            config.controlledSide == BattleMlControlledSide.PlayerTeam
            || config.controlledSide == BattleMlControlledSide.BothTeams
        )
        {
            AddSortedUnitsForTeam(payload.GetPlayerTeam().TeamId, projection, controlledUnits);
        }

        if (
            config.controlledSide == BattleMlControlledSide.HostileTeam
            || config.controlledSide == BattleMlControlledSide.BothTeams
        )
        {
            AddSortedUnitsForTeam(payload.GetHostileTeam().TeamId, projection, controlledUnits);
        }

        return controlledUnits;
    }

    private void AddSortedUnitsForTeam(
        BattleTeamId teamId,
        BattleRosterProjection projection,
        List<BattleRuntimeUnit> result
    )
    {
        var sorted = new List<(int SortIndex, int UnitNumber, BattleRuntimeUnit Unit)>();
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = flowManager.RuntimeUnits;
        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null || unit.TeamId != teamId)
            {
                continue;
            }

            sorted.Add((ResolveSortIndex(unit, projection), unit.UnitNumber, unit));
        }

        sorted.Sort(
            (left, right) =>
            {
                int byIndex = left.SortIndex.CompareTo(right.SortIndex);
                return byIndex != 0 ? byIndex : left.UnitNumber.CompareTo(right.UnitNumber);
            }
        );

        for (int i = 0; i < sorted.Count; i++)
        {
            result.Add(sorted[i].Unit);
        }
    }

    private static int ResolveSortIndex(BattleRuntimeUnit unit, BattleRosterProjection projection)
    {
        if (projection.IsPlayerUnit(unit) && projection.TryGetPlayerIndex(unit, out int playerIndex))
        {
            return playerIndex;
        }

        if (projection.TryGetHostileIndex(unit, out int hostileIndex))
        {
            return hostileIndex;
        }

        return unit != null ? unit.UnitNumber : int.MaxValue;
    }

    private void EnsureAgentPool(int count)
    {
        Transform parent = agentHostParent != null ? agentHostParent : transform;
        while (_agentPool.Count < count)
        {
            GladiatorAgent instance = Instantiate(agentPrefab, parent);
            instance.name = $"{agentPrefab.name}_{_agentPool.Count + 1:00}";
            instance.gameObject.SetActive(false);
            _agentPool.Add(instance);
        }
    }

    private void DeactivateSurplusAgents(int activeCount)
    {
        for (int i = activeCount; i < _agentPool.Count; i++)
        {
            if (_agentPool[i] != null)
            {
                _agentPool[i].gameObject.SetActive(false);
            }
        }
    }

    private void UnbindUnits()
    {
        for (int i = 0; i < _boundUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _boundUnits[i];
            if (unit != null)
            {
                unit.SetExternalMovement(Vector3.zero, 0f);
                unit.SetExternalAttackTarget(null);
                unit.SetControlMode(BattleUnitControlMode.BuiltInAI);
            }
        }

        _boundUnits.Clear();
    }

    private bool HasRuntimeUnits()
    {
        return flowManager != null && flowManager.RuntimeUnits != null && flowManager.RuntimeUnits.Count > 0;
    }

    private void ResolveSceneReferences()
    {
        if (flowManager == null)
        {
            flowManager = FindFirstObjectByType<BattleSceneFlowManager>();
        }

        if (agentHostParent == null)
        {
            agentHostParent = transform;
        }
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
