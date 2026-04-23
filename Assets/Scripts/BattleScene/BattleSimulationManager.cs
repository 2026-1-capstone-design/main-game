using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuffType
{
    MoveSpeed = 0,
    AttackRange = 1,
    AttackSpeed = 2,
    AttackDamage = 3,
    RedudeDamage = 4,

    //부정 버프
    BleedDamage,
    Taunt,
    Stun,
}

[DisallowMultipleComponent]
public sealed class BattleSimulationManager : MonoBehaviour
{
    [Header("Simulation")]
    public float simulationTickRate = 15f;
    public float simulationSpeedMultiplier = 1f;

    [Header("Simulation Speed Clamp")]
    public float minSimulationSpeed = 0.05f;
    public float maxSimulationSpeed = 8f;

    [Header("Battle")]
    public float unitBodyRadius = 50f;

    [Header("AI Configuration")]
    public BattleAITuningSO aiTuning;

    [Header("AI / Position Helpers")]
    public float desiredPositionStopDistance = 8f;
    public float escapeTowardTeamBlend = 0.35f;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>(12);
    private readonly List<BattleUnitCombatState> _unitStates = new List<BattleUnitCombatState>(12);
    private readonly Dictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState = new Dictionary<
        BattleUnitCombatState,
        BattleRuntimeUnit
    >(12);

    // 3D 전장 클램프를 위한 SphereCollider
    private SphereCollider _battlefieldCollider;
    private BattleStatusGridUIManager _statusGridUIManager;
    private BattleSceneUIManager _battleSceneUIManager;
    private BattleStartPayload _payload;
    private readonly BattleCooldownSystem _cooldownSystem = new BattleCooldownSystem();
    private readonly BattleParameterSystem _parameterSystem = new BattleParameterSystem();
    private readonly BattleDecisionSystem _decisionSystem = new BattleDecisionSystem();
    private readonly BattlePlanningSystem _planningSystem = new BattlePlanningSystem();
    private readonly BattlePhysicsSystem _physicsSystem = new BattlePhysicsSystem();
    private readonly BattleCombatSystem _combatSystem = new BattleCombatSystem(new SkillEffectApplier());
    private readonly BattleVictorySystem _victorySystem = new BattleVictorySystem();

    private bool _initialized;
    private bool _battleFinished;
    private bool _isTemporarilyPaused;
    private float _tickAccumulator;
    private float _tickInterval;
    private int _battleTickCount;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;
    public float SimulationSpeedMultiplier => simulationSpeedMultiplier;
    public float UnitBodyRadius => unitBodyRadius;
    public bool IsBattleFinished => _battleFinished;
    public bool IsTemporarilyPaused => _isTemporarilyPaused;
    public BattleStartPayload InitialPayload => _payload;
    public int BattleTickCount => _battleTickCount;
    public BattleFieldSnapshot CurrentSnapshot { get; private set; }

    public event Action<SimulationTickData> OnSimulationTicked;
    public event Action<BattleOutcome> OnBattleFinished;

    public void ForceFinishBattle()
    {
        if (_battleFinished)
            return;

        _battleFinished = true;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit != null && !unit.IsCombatDisabled)
                unit.SetIdleState();
        }
    }

    private void OnValidate()
    {
        simulationTickRate = Mathf.Max(1f, simulationTickRate);
        simulationSpeedMultiplier = Mathf.Max(0f, simulationSpeedMultiplier);
        unitBodyRadius = Mathf.Max(0f, unitBodyRadius);
        minSimulationSpeed = Mathf.Max(0.01f, minSimulationSpeed);
        maxSimulationSpeed = Mathf.Max(minSimulationSpeed, maxSimulationSpeed);
        simulationSpeedMultiplier = Mathf.Clamp(simulationSpeedMultiplier, minSimulationSpeed, maxSimulationSpeed);

        desiredPositionStopDistance = Mathf.Max(0f, desiredPositionStopDistance);

        if (aiTuning != null)
            aiTuning.EnsureDefaultActionTunings();

        if (_initialized)
            _physicsSystem.Configure(_battlefieldCollider, desiredPositionStopDistance);
    }

    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        SphereCollider battlefieldCollider,
        BattleStartPayload payload = null
    )
    {
        if (aiTuning != null)
            aiTuning.EnsureDefaultActionTunings();

        if (runtimeUnits == null)
        {
            Debug.LogError("[BattleSimulationManager] runtimeUnits is null.", this);
            return;
        }

        _runtimeUnits.Clear();
        _unitStates.Clear();
        _runtimeUnitByState.Clear();
        _battlefieldCollider = battlefieldCollider;

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
                continue;

            unit.State.SetBodyRadius(unitBodyRadius);
            unit.ClearExecutionPlan();
            unit.State.ClearAttackCooldown();
            unit.State.ClearSkillCooldown();
            unit.State.SetIdleState();

            _runtimeUnits.Add(unit);
            _unitStates.Add(unit.State);
            _runtimeUnitByState[unit.State] = unit;
        }

        _payload = payload;

        CurrentSnapshot = null;
        _physicsSystem.Configure(_battlefieldCollider, desiredPositionStopDistance);

        _tickAccumulator = 0f;
        _tickInterval = 1f / Mathf.Max(1f, simulationTickRate);
        _battleFinished = false;
        _isTemporarilyPaused = false;
        _battleTickCount = 0;
        _initialized = true;
    }

    private void Update()
    {
        if (!_initialized || _battleFinished || _isTemporarilyPaused)
            return;

        float scaledDeltaTime = Time.deltaTime * Mathf.Max(0f, simulationSpeedMultiplier);
        _tickAccumulator += scaledDeltaTime;

        while (_tickAccumulator >= _tickInterval)
        {
            _tickAccumulator -= _tickInterval;
            StepSimulation(_tickInterval);

            if (_battleFinished)
                break;
        }
    }

    public void AnimationSpeedSetting()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            if (_runtimeUnits[i] != null)
                _runtimeUnits[i].SetAnimationSpeed(simulationSpeedMultiplier);
        }
    }

    public void SetSimulationSpeedMultiplier(float multiplier)
    {
        simulationSpeedMultiplier = Mathf.Clamp(multiplier, minSimulationSpeed, maxSimulationSpeed);
        AnimationSpeedSetting();
    }

    public void MultiplySimulationSpeed(float multiplier)
    {
        if (multiplier <= 0f)
            return;

        SetSimulationSpeedMultiplier(simulationSpeedMultiplier * multiplier);
    }

    public void SetTemporaryPause(bool isPaused)
    {
        _isTemporarilyPaused = isPaused;
    }

    private void StepSimulation(float tickDeltaTime)
    {
        _battleTickCount++;

        BattleParameterRadii radii = BattleParameterSystem.BuildRadii(aiTuning);
        CurrentSnapshot = BattleFieldSnapshot.Build(_runtimeUnits, radii, escapeTowardTeamBlend);
        _cooldownSystem.Tick(_runtimeUnits, tickDeltaTime);

        BattleParameterComputation[] parameterResults = _parameterSystem.Compute(_runtimeUnits, radii, aiTuning);
        BattleActionType[] decisions = _decisionSystem.Decide(_runtimeUnits, aiTuning, tickDeltaTime);

        _planningSystem.Build(_runtimeUnits, CurrentSnapshot);
        _physicsSystem.Execute(_runtimeUnits, tickDeltaTime);

        BattleCombatResult[] combatResults = _combatSystem.Execute(_runtimeUnits, _runtimeUnitByState);

        BattleOutcome? outcome = _victorySystem.Evaluate(_runtimeUnits, _battleTickCount);

        SimulationTickData tickData = BuildTickData(parameterResults, decisions, combatResults);
        OnSimulationTicked?.Invoke(tickData);

        if (outcome.HasValue)
            HandleBattleFinished(outcome.Value);
    }

    private void HandleBattleFinished(BattleOutcome outcome)
    {
        if (_battleFinished)
            return;

        _battleFinished = true;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            unit.SetIdleState();
        }

        OnBattleFinished?.Invoke(outcome);
    }

    private SimulationTickData BuildTickData(
        BattleParameterComputation[] parameterResults,
        BattleActionType[] decisions,
        BattleCombatResult[] combatResults
    )
    {
        int unitCount = _runtimeUnits.Count;
        int[] unitNumbers = new int[unitCount];
        var rawParameters = new BattleParameterSet[unitCount];
        var modifiedParameters = new BattleParameterSet[unitCount];
        var modifierOverflowFlags = new bool[unitCount];
        var decisionResults = new BattleActionType[unitCount];

        for (int i = 0; i < unitCount; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
            {
                unitNumbers[i] = -1;
                decisionResults[i] = BattleActionType.None;
                continue;
            }

            unitNumbers[i] = unit.UnitNumber;
            rawParameters[i] = unit.CurrentRawParameters;
            modifiedParameters[i] = unit.CurrentModifiedParameters;

            if (decisions != null && i < decisions.Length)
                decisionResults[i] = decisions[i];
            else
                decisionResults[i] = unit.CurrentActionType;
        }

        if (parameterResults != null)
        {
            for (int i = 0; i < parameterResults.Length; i++)
            {
                BattleParameterComputation result = parameterResults[i];
                if (result.UnitIndex < 0 || result.UnitIndex >= unitCount)
                    continue;

                rawParameters[result.UnitIndex] = result.Raw;
                modifiedParameters[result.UnitIndex] = result.Modified;
                modifierOverflowFlags[result.UnitIndex] = result.ModifierOverflowed;
            }
        }

        return new SimulationTickData(
            _battleTickCount,
            unitNumbers,
            rawParameters,
            modifiedParameters,
            modifierOverflowFlags,
            decisionResults,
            combatResults ?? new BattleCombatResult[0]
        );
    }
}
