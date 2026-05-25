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
    Untargetable, //지정 불가

    //부정 버프
    BleedDamage,
    Taunt,
    Stun,
}

[DisallowMultipleComponent]
public sealed class BattleSimulationManager : MonoBehaviour
{
    private const int ForcedControlPriority = 400;
    private const int PlayerCommandControlPriority = 300;
    private const int MlAgentControlPriority = 200;
    private const int BuiltInAiControlPriority = 100;

    [Header("Simulation")]
    public float simulationTickRate = 15f;
    public float simulationSpeedMultiplier = 1f;

    // Update()에서 자동으로 시뮬레이션 틱을 진행할지 여부. false로 설정하면 외부에서 명시적으로 StepSimulationTick() 또는 StepSimulationTicks()를 호출해야 틱이 진행됨.
    public bool autoStepInUpdate = true;

    [Header("Simulation Speed Clamp")]
    public float minSimulationSpeed = 0.05f;
    public float maxSimulationSpeed = 8f;

    [Header("Battle")]
    public float unitBodyRadius = 50f;

    [Header("AI Configuration")]
    public BattleAITuningSO aiTuning;

    // SLM 명령 처리 튜닝 수치(거리/threshold/timeout 등). 미할당이어도 코드 디폴트값으로 동작.
    [SerializeField]
    private SlmCommandTuningSO slmCommandTuningSO;

    [Header("AI / Position Helpers")]
    public float desiredPositionStopDistance = 8f;
    public float escapeTowardTeamBlend = 0.35f;

    [Header("Training Optimization")]
    [SerializeField]
    private bool trainingOptimizedSimulation;

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

    private SphereCollider _battlefieldCollider;

    // SLM 영역(SlmMoveSubtypeResolver.ResolveEscape)에서 도주 좌표를 경기장 안으로 보정할 때 사용한다.
    public SphereCollider BattlefieldCollider => _battlefieldCollider;

    // BattleUnitCombatState에서 BattleRuntimeUnit으로의 역참조를 O(1)로 노출한다.
    // BattleCombatSystem 등 다른 시스템과 동일 dictionary를 공유한다.
    public IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> RuntimeUnitByState => _runtimeUnitByState;

    private BattleStatusGridUIManager _statusGridUIManager;
    private BattleSceneUIManager _battleSceneUIManager;
    private BattleStartPayload _payload;
    private readonly BattleCooldownSystem _cooldownSystem = new BattleCooldownSystem();
    private readonly BattleParameterSystem _parameterSystem = new BattleParameterSystem();
    private readonly BattlePlanningSystem _planningSystem = new BattlePlanningSystem();
    private readonly BattlePhysicsSystem _physicsSystem = new BattlePhysicsSystem();
    private readonly BattleArtifactSystem _artifactSystem = new BattleArtifactSystem();
    private readonly BattleSkillChannelSystem _channelSystem = new BattleSkillChannelSystem();
    private readonly BattleScheduledEffectSystem _scheduledEffectSystem = new BattleScheduledEffectSystem();
    private readonly BattlePositionHistory _positionHistory = new BattlePositionHistory();
    private readonly BattleDamageLifecycle _damageLifecycle = new BattleDamageLifecycle();
    private readonly BattleRosterMutationSystem _rosterMutationSystem = new BattleRosterMutationSystem();

    public BattleProjectileManager projectileManager;
    private readonly BattleProjectileSystem _projectileSystem = new BattleProjectileSystem(); //투사체 시스템

    public BattleTextManager _battleTextManager = new BattleTextManager(); //텍스트 메시지 매니저

    private BattleEffectSystem _effectSystem;
    private BattleCombatSystem _combatSystem;
    private readonly BattleVictorySystem _victorySystem = new BattleVictorySystem();
    private readonly BattleAgentControlBuffer _agentControlBuffer = new BattleAgentControlBuffer();
    private readonly ForcedControlPlanBuffer _forcedControlPlanBuffer = new ForcedControlPlanBuffer();
    private readonly PlayerCommandControlBuffer _playerCommandControlBuffer = new PlayerCommandControlBuffer();
    private readonly BattleControlPlannerRegistry _controlPlannerRegistry = new BattleControlPlannerRegistry();
    private ForcedControlPlanner _forcedControlPlanner;
    private PlayerCommandControlPlanner _playerCommandControlPlanner;
    private readonly LegacyBuiltInAiPlanner _legacyBuiltInAiPlanner = new LegacyBuiltInAiPlanner();
    private MlAgentControlPlanner _mlAgentControlPlanner;

    // SLM 명령 매니저. controller stack에 직접 등록되지 않고 매 tick PlayerCommandControlBuffer에 plan을 채운다.
    private readonly SlmCommandUnitPlanner _slmCommandUnitPlanner = new SlmCommandUnitPlanner();
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
    private readonly BattleControlPlan[] _tickControlPlanBuffer = new BattleControlPlan[
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
    public bool AutoStepInUpdate => autoStepInUpdate;
    public BattleStartPayload InitialPayload => _payload;
    public int BattleTickCount => _battleTickCount;
    public float TickInterval => _tickInterval;
    public BattleFieldSnapshot CurrentSnapshot { get; private set; }
    public BattleAgentControlBuffer AgentControlBuffer => _agentControlBuffer;
    public ForcedControlPlanBuffer ForcedControlPlanBuffer => _forcedControlPlanBuffer;
    public PlayerCommandControlBuffer PlayerCommandControlBuffer => _playerCommandControlBuffer;
    public BattleArtifactSystem ArtifactSystem => _artifactSystem;
    public bool TrainingOptimizedSimulation => trainingOptimizedSimulation;

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

    public static BattleSimulationManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
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
        _channelSystem.Clear();
        _scheduledEffectSystem.Clear();
        _positionHistory.Clear();
        _damageLifecycle.Clear();
        _rosterMutationSystem.Clear();
        _projectileSystem.Clear();
        _controlPlannerRegistry.Clear();
        _agentControlBuffer.ClearAll();
        _forcedControlPlanBuffer.ClearAll();
        _playerCommandControlBuffer.ClearAll();
        _slmCommandUnitPlanner.ClearAll();
        _slmCommandUnitPlanner.SetTuning(slmCommandTuningSO);
        SlmMoveSubtypeResolver.SetTuning(slmCommandTuningSO);
        _battlefieldCollider = battlefieldCollider;
        if (_mlAgentControlPlanner == null)
            _mlAgentControlPlanner = new MlAgentControlPlanner(_agentControlBuffer);
        EnsureControlPlanners();
        RegisterControlPlanners();

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
        EnsureCombatSystems();

        if (projectileManager != null)
        {
            _projectileSystem.Configure(_effectSystem, projectileManager.ProjectileRoot, _runtimeUnits);
        }
        if (_battleTextManager != null)
        {
            _battleTextManager.Initialize(_effectSystem);
        }

        _rosterMutationSystem.Configure(
            _runtimeUnits,
            _unitStates,
            _runtimeUnitByState,
            _battlefieldCollider,
            _payload != null ? _payload.PlayerTeamId : BattleTeamIds.Player
        );
        _effectSystem.ConfigureLongRunningSystems(_scheduledEffectSystem, _damageLifecycle, _rosterMutationSystem);
        BattleParameterRadii initialRadii = BattleParameterSystem.BuildRadii(aiTuning);
        if (ShouldBuildSnapshot())
        {
            CurrentSnapshot = BattleFieldSnapshot.Build(
                _runtimeUnits,
                initialRadii,
                escapeTowardTeamBlend,
                CurrentSnapshot,
                UseArtifacts ? _artifactSystem.TargetingPolicy : null
            );
        }
        _effectSystem.Configure(_tickCombatResultBuffer, _runtimeUnitByState, _battlefieldCollider);
        if (UseArtifacts)
            _artifactSystem.Initialize(_runtimeUnits, CurrentSnapshot, 0f, 0, _effectSystem);
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
        if (!autoStepInUpdate || !_initialized || _battleFinished || _isTemporarilyPaused)
            return;

        float scaledDeltaTime = Time.deltaTime * Mathf.Max(0f, simulationSpeedMultiplier);
        _tickAccumulator += scaledDeltaTime;

        while (_tickAccumulator >= _tickInterval)
        {
            _tickAccumulator -= _tickInterval;
            StepSimulationTick();

            if (_battleFinished)
                break;
        }
    }

    public void SetAutoStepInUpdate(bool enabled)
    {
        autoStepInUpdate = enabled;
        if (!enabled)
            _tickAccumulator = 0f;
    }

    public bool StepSimulationTick()
    {
        if (!_initialized || _battleFinished || _isTemporarilyPaused)
            return false;

        StepSimulation(_tickInterval);
        return true;
    }

    public int StepSimulationTicks(int tickCount)
    {
        int steppedCount = 0;
        tickCount = Mathf.Max(0, tickCount);

        for (int i = 0; i < tickCount; i++)
        {
            if (!StepSimulationTick())
                break;

            steppedCount++;
        }

        return steppedCount;
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

    public void SetTrainingOptimizedSimulation(bool enabled)
    {
        trainingOptimizedSimulation = enabled;
        _channelSystem.Clear();
        ReleaseSnapshot();
        EnsureCombatSystems();
    }

    private void StepSimulation(float tickDeltaTime)
    {
        _rosterMutationSystem.FlushPendingSummons();
        _battleTickCount++;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit != null)
            {
                unit.SetPosition(unit.Position);
            }
        }

        BattleParameterRadii radii = BattleParameterSystem.BuildRadii(aiTuning);
        if (ShouldBuildSnapshot())
        {
            CurrentSnapshot = BattleFieldSnapshot.Build(
                _runtimeUnits,
                radii,
                escapeTowardTeamBlend,
                CurrentSnapshot,
                UseArtifacts ? _artifactSystem.TargetingPolicy : null
            );
        }
        else
        {
            ReleaseSnapshot();
        }

        float battleTime = _battleTickCount * _tickInterval;
        BattleEffectContext tickContext = new BattleEffectContext(
            null,
            null,
            CurrentSnapshot,
            _runtimeUnits,
            battleTime,
            _battleTickCount
        );
        _tickCombatResultBuffer.Clear();
        _effectSystem.Configure(_tickCombatResultBuffer, _runtimeUnitByState, _battlefieldCollider);
        _rosterMutationSystem.Tick(battleTime);
        if (UseSkills)
            _channelSystem.Tick(tickContext, _effectSystem);
        _scheduledEffectSystem.Tick(tickContext, _effectSystem);
        _cooldownSystem.Tick(_runtimeUnits, tickDeltaTime, _effectSystem, UseSkills);
        if (UseProjectiles)
            _projectileSystem.Tick(tickDeltaTime);

        if (CurrentSnapshot != null)
        {
            _parameterSystem.Compute(_runtimeUnits, radii, aiTuning, CurrentSnapshot, _tickModifierOverflowFlagsBuffer);
        }
        else
        {
            for (int i = 0; i < _runtimeUnits.Count && i < _tickModifierOverflowFlagsBuffer.Length; i++)
                _tickModifierOverflowFlagsBuffer[i] = false;
        }

        // SLM 활성 액터의 plan을 PlayerCommandControlBuffer에 채워둔다.
        // 직후 _planningSystem.Build가 controller stack을 통해 이 plan을 소비한다.
        BattlePlanningContext slmContext = new BattlePlanningContext(
            _runtimeUnits,
            CurrentSnapshot,
            aiTuning,
            tickDeltaTime
        );
        _slmCommandUnitPlanner.Tick(in slmContext, _playerCommandControlBuffer);

        _planningSystem.Build(
            _runtimeUnits,
            CurrentSnapshot,
            _controlPlannerRegistry,
            aiTuning,
            tickDeltaTime,
            _tickControlPlanBuffer,
            _rosterMutationSystem
        );
        _physicsSystem.Execute(
            _runtimeUnits,
            _runtimeUnitByState,
            tickDeltaTime,
            _tickControlPlanBuffer,
            UseArtifacts ? _artifactSystem.MovementPolicy : null,
            UseSkills ? _channelSystem : null
        );
        bool recordPositionHistory = UseArtifacts;
        if (recordPositionHistory)
            _positionHistory.RecordAll(_runtimeUnits, battleTime);
        if (recordPositionHistory)
            _artifactSystem.TickPositionHistoryArtifacts(_positionHistory, tickContext, _effectSystem);
        _combatSystem.Execute(
            _runtimeUnits,
            _runtimeUnitByState,
            _tickCombatResultBuffer,
            CurrentSnapshot,
            battleTime,
            _battleTickCount,
            _tickControlPlanBuffer,
            clearResults: false,
            projectilesEnabled: UseProjectiles
        );

        BattleOutcome? outcome = _victorySystem.Evaluate(
            _runtimeUnits,
            _battleTickCount,
            _payload != null ? _payload.PlayerTeamId : BattleTeamIds.Player,
            _payload != null ? _payload.PreviewRewardGold : 0
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
            _tickDecisionBuffer[i] = unit.CurrentActionType;
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

    private void EnsureCombatSystems()
    {
        _effectSystem = new BattleEffectSystem(UseArtifacts ? _artifactSystem : null);
        _combatSystem = new BattleCombatSystem(
            _effectSystem,
            UseSkills ? _channelSystem : null,
            UseArtifacts ? _artifactSystem : null,
            _rosterMutationSystem,
            this,
            UseSkills
        );
    }

    private void ReleaseSnapshot()
    {
        if (CurrentSnapshot == null)
            return;

        CurrentSnapshot.Reset();
        CurrentSnapshot = null;
    }

    // 평타 전용
    public void LaunchBasicProjectile(
        BattleDamageRequest request,
        Vector3 startPos,
        Vector3 direction,
        WeaponType weaponType,
        float delay = 0f
    )
    {
        TryLaunchBasicProjectile(request, startPos, direction, weaponType, delay);
    }

    public bool TryLaunchBasicProjectile(
        BattleDamageRequest request,
        Vector3 startPos,
        Vector3 direction,
        WeaponType weaponType,
        float delay = 0f
    )
    {
        if (!UseProjectiles)
            return false;
        if (projectileManager == null)
            return false;

        GameObject prefab =
            (weaponType == WeaponType.staff)
                ? projectileManager.NormalMagicPrefab
                : projectileManager.NormalArrowPrefab;
        if (prefab == null)
            return false;

        _projectileSystem.Launch(request, startPos, direction, 5f, prefab, delay, null, true);
        return true;
    }

    // 스킬 전용 (string ID 사용)
    public void LaunchCustomProjectile(
        BattleDamageRequest request,
        Vector3 startPos,
        Vector3 direction,
        float speed,
        string projectileId,
        float delay = 0f,
        Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHit = null
    )
    {
        if (!UseProjectiles)
            return;
        if (projectileManager == null)
            return;

        GameObject customPrefab = projectileManager.GetCustomPrefab(projectileId);
        if (customPrefab != null)
        {
            //기본값 false
            _projectileSystem.Launch(request, startPos, direction, speed, customPrefab, delay, onHit, false);
        }
    }

    public void SetUnitControlMode(BattleUnitCombatState state, BattleUnitControlMode mode)
    {
        if (state == null)
            return;

        if (_mlAgentControlPlanner == null)
        {
            _mlAgentControlPlanner = new MlAgentControlPlanner(_agentControlBuffer);
        }

        if (mode == BattleUnitControlMode.AgentPolicy)
        {
            _controlPlannerRegistry.SetUnitPlannerEnabled(state, _mlAgentControlPlanner, MlAgentControlPriority, true);
            return;
        }

        _controlPlannerRegistry.SetUnitPlannerEnabled(state, _mlAgentControlPlanner, MlAgentControlPriority, false);
        _agentControlBuffer.Clear(state);
    }

    public BattleUnitControlMode GetUnitControlMode(BattleUnitCombatState state)
    {
        if (state != null && _controlPlannerRegistry.IsUnitPlannerEnabled(state, _mlAgentControlPlanner))
        {
            return BattleUnitControlMode.AgentPolicy;
        }

        return BattleUnitControlMode.BuiltInAI;
    }

    // 검증된 SLM 명령 시퀀스를 액터에 활성화한다. BattleOrdersManager에서 호출되는 진입점.
    // 명령이 끝나면 SlmCommandUnitPlanner.Tick이 PlayerCommandControlBuffer를 비우고,
    // controller stack이 다음 우선순위(ML 또는 BuiltInAi)로 자동 fallback한다.
    public void IssueSlmCommands(BattleUnitCombatState actor, IReadOnlyList<SlmUnitCommand> commands)
    {
        if (actor == null || commands == null || commands.Count == 0)
            return;

        _slmCommandUnitPlanner.IssueCommands(actor, commands);
    }

    private bool UseArtifacts => !trainingOptimizedSimulation;

    private bool UseSkills => !trainingOptimizedSimulation;

    // 학습 환경에서는 투사체 비행/피격 지연을 제거한다.
    // 강화학습 보상은 action step 경계에 묶이므로, 원거리 평타 피해를 나중 tick에 적용하면
    // Attack 명령과 damage/kill reward의 credit assignment가 흐려진다.
    private bool UseProjectiles => !trainingOptimizedSimulation;

    private bool ShouldBuildSnapshot()
    {
        if (!trainingOptimizedSimulation)
            return true;

        return !AreAllUnitsAgentControlled();
    }

    private bool AreAllUnitsAgentControlled()
    {
        if (_runtimeUnits.Count == 0)
            return false;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (
                unit == null
                || unit.State == null
                || GetUnitControlMode(unit.State) != BattleUnitControlMode.AgentPolicy
            )
                return false;
        }

        return true;
    }

    private void RegisterControlPlanners()
    {
        EnsureControlPlanners();
        _controlPlannerRegistry.RegisterGlobal(_forcedControlPlanner, ForcedControlPriority);
        _controlPlannerRegistry.RegisterGlobal(_playerCommandControlPlanner, PlayerCommandControlPriority);
        _controlPlannerRegistry.RegisterGlobal(_legacyBuiltInAiPlanner, BuiltInAiControlPriority);
    }

    private void EnsureControlPlanners()
    {
        if (_forcedControlPlanner == null)
            _forcedControlPlanner = new ForcedControlPlanner(_forcedControlPlanBuffer);

        if (_playerCommandControlPlanner == null)
            _playerCommandControlPlanner = new PlayerCommandControlPlanner(_playerCommandControlBuffer);
    }
}
