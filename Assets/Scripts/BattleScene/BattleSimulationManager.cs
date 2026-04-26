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

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>(
        BattleTeamConstants.MaxUnitsInBattle
    );
    private readonly List<BattleUnitCombatState> _unitStates = new List<BattleUnitCombatState>(
        BattleTeamConstants.MaxUnitsInBattle
    );
    private readonly Dictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState = new Dictionary<
        BattleUnitCombatState,
        BattleRuntimeUnit
    >(BattleTeamConstants.MaxUnitsInBattle);

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
    private readonly int[] _tickUnitNumbersBuffer = new int[BattleTeamConstants.MaxUnitsInBattle];
    private readonly BattleParameterSet[] _tickRawParametersBuffer = new BattleParameterSet[
        BattleTeamConstants.MaxUnitsInBattle
    ];
    private readonly BattleParameterSet[] _tickModifiedParametersBuffer = new BattleParameterSet[
        BattleTeamConstants.MaxUnitsInBattle
    ];
    private readonly bool[] _tickModifierOverflowFlagsBuffer = new bool[BattleTeamConstants.MaxUnitsInBattle];
    private readonly BattleActionType[] _tickDecisionBuffer = new BattleActionType[
        BattleTeamConstants.MaxUnitsInBattle
    ];
    private readonly BattleCombatResultBuffer _tickCombatResultBuffer = new BattleCombatResultBuffer(
        BattleTeamConstants.MaxUnitsInBattle
    );

    private bool _initialized;
    private bool _battleFinished;
    private bool _isTemporarilyPaused;
    private float _tickAccumulator;
    private float _tickInterval;
    private int _battleTickCount;
    private SimulationTickData _tickData;

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

        ReleaseSnapshot();
        _physicsSystem.Configure(_battlefieldCollider, desiredPositionStopDistance);

        _tickAccumulator = 0f;
        _tickInterval = 1f / Mathf.Max(1f, simulationTickRate);
        _battleFinished = false;
        _isTemporarilyPaused = false;
        _battleTickCount = 0;
        EnsureTickData();
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
        CurrentSnapshot = BattleFieldSnapshot.Build(_runtimeUnits, radii, escapeTowardTeamBlend, CurrentSnapshot);
        _cooldownSystem.Tick(_runtimeUnits, tickDeltaTime);

        _parameterSystem.Compute(_runtimeUnits, radii, aiTuning, CurrentSnapshot, _tickModifierOverflowFlagsBuffer);
        _decisionSystem.Decide(_runtimeUnits, aiTuning, tickDeltaTime, _tickDecisionBuffer);

        _planningSystem.Build(_runtimeUnits, CurrentSnapshot);
        _physicsSystem.Execute(_runtimeUnits, tickDeltaTime);
        _combatSystem.Execute(_runtimeUnits, _runtimeUnitByState, _tickCombatResultBuffer);

        BattleOutcome? outcome = _victorySystem.Evaluate(
            _runtimeUnits,
            _battleTickCount,
            _payload != null ? _payload.PlayerTeamId : BattleTeamIds.Player
        );

        SimulationTickData tickData = BuildTickData();
        OnSimulationTicked?.Invoke(tickData);

        if (outcome.HasValue)
            HandleBattleFinished(outcome.Value);
    }

    private void OnDestroy()
    {
        ReleaseSnapshot();
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

    private SimulationTickData BuildTickData()
    {
        EnsureTickData();

        int unitCount = _runtimeUnits.Count;
        for (int i = 0; i < unitCount; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null)
            {
                _tickUnitNumbersBuffer[i] = -1;
                _tickRawParametersBuffer[i] = default;
                _tickModifiedParametersBuffer[i] = default;
                _tickModifierOverflowFlagsBuffer[i] = false;
                _tickDecisionBuffer[i] = BattleActionType.None;
                continue;
            }

            _tickUnitNumbersBuffer[i] = unit.UnitNumber;
            _tickRawParametersBuffer[i] = unit.CurrentRawParameters;
            _tickModifiedParametersBuffer[i] = unit.CurrentModifiedParameters;
        }

        _tickData.Update(_battleTickCount, unitCount, _tickCombatResultBuffer.Count);
        return _tickData;
    }

    private void EnsureTickData()
    {
        if (_tickData == null)
        {
            _tickData = new SimulationTickData(
                _tickUnitNumbersBuffer,
                _tickRawParametersBuffer,
                _tickModifiedParametersBuffer,
                _tickModifierOverflowFlagsBuffer,
                _tickDecisionBuffer,
                _tickCombatResultBuffer.Items
            );
            return;
        }

        if (!ReferenceEquals(_tickData.CombatResults, _tickCombatResultBuffer.Items))
            _tickData.UpdateCombatResultsBuffer(_tickCombatResultBuffer.Items);
    }

    private void ReleaseSnapshot()
    {
        if (CurrentSnapshot == null)
            return;

        CurrentSnapshot.Reset();
        CurrentSnapshot = null;
    }
}
