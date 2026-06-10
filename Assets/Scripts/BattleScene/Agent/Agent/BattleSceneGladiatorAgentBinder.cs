using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleSceneGladiatorAgentBinder : MonoBehaviour
{
    [SerializeField]
    private BattleSceneFlowManager flowManager;

    [SerializeField]
    private GladiatorAgent agentPrefab;

    [SerializeField]
    private Transform agentHostParent;

    [SerializeField]
    private GladiatorControlledSide controlledSide = GladiatorControlledSide.HostileTeam;

    [SerializeField]
    private int maxAgentCount = BattleTeamConstants.MaxUnitsPerTeam * 2;

    [SerializeField]
    private bool bindAlreadySpawnedUnitsOnEnable = true;

    private readonly List<GladiatorAgent> _agentPool = new List<GladiatorAgent>();
    private readonly List<BattleRuntimeUnit> _boundUnits = new List<BattleRuntimeUnit>();
    private readonly List<TrainingUnitCombatOverlay> _rangeOverlays = new List<TrainingUnitCombatOverlay>();

    private void OnValidate()
    {
        if (agentHostParent == null)
        {
            agentHostParent = transform;
        }

        maxAgentCount = Mathf.Clamp(maxAgentCount, 0, BattleTeamConstants.MaxUnitsPerTeam * 2);
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
            Debug.LogError("[BattleSceneGladiatorAgentBinder] Cannot bind agents. Battle payload is missing.", this);
            DeactivateSurplusAgents(0);
            return;
        }

        BattleRosterProjection projection = new BattleRosterProjection(payload);
        List<BattleRuntimeUnit> controlledUnits = ResolveControlledUnits(payload, projection);
        int bindCount = Mathf.Min(controlledUnits.Count, maxAgentCount);
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
            agent.Initialize(unit, flowManager, flowManager);
            _boundUnits.Add(unit);
            AttachRangeOverlay(unit);
        }

        DeactivateSurplusAgents(bindCount);
    }

    private bool ConfigureAgentHost(GladiatorAgent agent, BattleRuntimeUnit unit, int agentIndex)
    {
        if (agent == null || unit == null)
        {
            return false;
        }

        return GladiatorAgentContract.TryApplyRuntimeOverrides(
            agent,
            unit,
            agentIndex,
            true,
            this,
            "[BattleSceneGladiatorAgentBinder]"
        );
    }

    private bool ValidateConfig()
    {
        if (controlledSide == GladiatorControlledSide.None)
        {
            return false;
        }

        if (agentPrefab == null)
        {
            Debug.LogError("[BattleSceneGladiatorAgentBinder] Agent prefab is not assigned.", this);
            return false;
        }

        if (flowManager == null)
        {
            Debug.LogError("[BattleSceneGladiatorAgentBinder] BattleSceneFlowManager is not assigned.", this);
            return false;
        }

        return GladiatorAgentContract.ValidatePrefab(agentPrefab, true, this, "[BattleSceneGladiatorAgentBinder]");
    }

    private List<BattleRuntimeUnit> ResolveControlledUnits(
        BattleStartPayload payload,
        BattleRosterProjection projection
    )
    {
        var controlledUnits = new List<BattleRuntimeUnit>();

        if (controlledSide == GladiatorControlledSide.PlayerTeam || controlledSide == GladiatorControlledSide.BothTeams)
        {
            controlledUnits.AddRange(
                GladiatorUnitSelection.GetSortedUnitsForTeam(
                    flowManager.RuntimeUnits,
                    payload.GetPlayerTeam().TeamId,
                    projection
                )
            );
        }

        if (
            controlledSide == GladiatorControlledSide.HostileTeam
            || controlledSide == GladiatorControlledSide.BothTeams
        )
        {
            controlledUnits.AddRange(
                GladiatorUnitSelection.GetSortedUnitsForTeam(
                    flowManager.RuntimeUnits,
                    payload.GetHostileTeam().TeamId,
                    projection
                )
            );
        }

        return controlledUnits;
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
        HideRangeOverlays();

        for (int i = 0; i < _boundUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _boundUnits[i];
            if (unit != null)
            {
                flowManager?.BattleSimulationManager?.SetUnitControlMode(unit.State, BattleUnitControlMode.BuiltInAI);
            }
        }

        _boundUnits.Clear();
    }

    private void AttachRangeOverlay(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        TrainingUnitCombatOverlay overlay = unit.GetComponent<TrainingUnitCombatOverlay>();
        if (overlay == null)
        {
            overlay = unit.gameObject.AddComponent<TrainingUnitCombatOverlay>();
        }

        overlay.SetRangeOnlyVisible(true);
        _rangeOverlays.Add(overlay);
    }

    private void HideRangeOverlays()
    {
        for (int i = 0; i < _rangeOverlays.Count; i++)
        {
            TrainingUnitCombatOverlay overlay = _rangeOverlays[i];
            if (overlay != null)
            {
                overlay.SetRangeOnlyVisible(false);
            }
        }

        _rangeOverlays.Clear();
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
}
