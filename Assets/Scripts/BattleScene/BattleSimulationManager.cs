using System.Collections.Generic;
using UnityEngine;

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

    [Header("AI / Action Switching")]
    [SerializeField] private float commitmentDecayPerSecond = 0.5f;

    private const float CommitmentEnterMultiplier = 1.2f;

    [Header("AI / Parameter Radii")]
    public float surroundRadius = 350f;
    public float helpRadius = 450f;
    public float peelRadius = 500f;
    public float frontlineGapRadius = 600f;     //리그룹, 400에서 600 보정
    public float isolationRadius = 450f;
    public float assassinReachRadius = 600f;
    public float clusterRadius = 400f;
    public float teamCenterDistanceRadius = 800f;       //리그룹, 500에서 800 보정

    [Header("AI / Position Helpers")]
    public float desiredPositionStopDistance = 8f;
    public float escapeTowardTeamBlend = 0.35f;

    [Header("AI / Action Tunings")]
    public List<BattleActionTuning> actionTunings = new List<BattleActionTuning>();

    [Header("Debug")]
    public bool verboseLog = false;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>(12);

    private RectTransform _battlefieldRect;
    private BattleStatusGridUIManager _statusGridUIManager;
    private BattleSceneUIManager _battleSceneUIManager;
    private BattleStartPayload _payload;

    private bool _initialized;
    private bool _battleFinished;
    private bool _isTemporarilyPaused;
    private float _tickAccumulator;
    private float _tickInterval;

    public IReadOnlyList<BattleRuntimeUnit> RuntimeUnits => _runtimeUnits;
    public float SimulationSpeedMultiplier => simulationSpeedMultiplier;
    public float UnitBodyRadius => unitBodyRadius;
    public bool IsBattleFinished => _battleFinished;
    public bool IsTemporarilyPaused => _isTemporarilyPaused;
    public BattleStartPayload InitialPayload => _payload;

    private void Reset()
    {
        EnsureDefaultActionTunings();
    }

    private void OnValidate()
    {
        simulationTickRate = Mathf.Max(1f, simulationTickRate);
        simulationSpeedMultiplier = Mathf.Max(0f, simulationSpeedMultiplier);
        unitBodyRadius = Mathf.Max(0f, unitBodyRadius);
        minSimulationSpeed = Mathf.Max(0.01f, minSimulationSpeed);
        maxSimulationSpeed = Mathf.Max(minSimulationSpeed, maxSimulationSpeed);
        simulationSpeedMultiplier = Mathf.Clamp(simulationSpeedMultiplier, minSimulationSpeed, maxSimulationSpeed);

        commitmentDecayPerSecond = Mathf.Max(0f, commitmentDecayPerSecond);

        surroundRadius = Mathf.Max(1f, surroundRadius);
        helpRadius = Mathf.Max(1f, helpRadius);
        peelRadius = Mathf.Max(1f, peelRadius);
        frontlineGapRadius = Mathf.Max(1f, frontlineGapRadius);
        isolationRadius = Mathf.Max(1f, isolationRadius);
        assassinReachRadius = Mathf.Max(1f, assassinReachRadius);
        clusterRadius = Mathf.Max(1f, clusterRadius);
        teamCenterDistanceRadius = Mathf.Max(1f, teamCenterDistanceRadius);
        desiredPositionStopDistance = Mathf.Max(0f, desiredPositionStopDistance);

        EnsureDefaultActionTunings();
    }

    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        RectTransform battlefieldRect,
        BattleStatusGridUIManager statusGridUIManager = null,
        BattleSceneUIManager battleSceneUIManager = null,
        BattleStartPayload payload = null)
    {
        EnsureDefaultActionTunings();

        if (runtimeUnits == null)
        {
            Debug.LogError("[BattleSimulationManager] runtimeUnits is null.", this);
            return;
        }

        if (battlefieldRect == null)
        {
            Debug.LogError("[BattleSimulationManager] battlefieldRect is null.", this);
            return;
        }

        _runtimeUnits.Clear();

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
            {
                continue;
            }

            unit.SetBodyRadius(unitBodyRadius);
            unit.ClearCurrentTarget();
            unit.ClearAttackCooldown();
            unit.SetIdleState();
            unit.ClampInsideBattlefield(battlefieldRect);

            _runtimeUnits.Add(unit);
        }

        _battlefieldRect = battlefieldRect;
        _statusGridUIManager = statusGridUIManager;
        _battleSceneUIManager = battleSceneUIManager;
        _payload = payload;

        _tickAccumulator = 0f;
        _tickInterval = 1f / Mathf.Max(1f, simulationTickRate);
        _battleFinished = false;
        _isTemporarilyPaused = false;
        _initialized = true;

        if (_statusGridUIManager != null)
        {
            _statusGridUIManager.Initialize(this, _runtimeUnits, _battleSceneUIManager);
            _statusGridUIManager.Refresh();
        }

        if (_battleSceneUIManager != null)
        {
            _battleSceneUIManager.Initialize();
            _battleSceneUIManager.HideAll();
        }

        if (verboseLog)
        {
            Debug.Log($"[BattleSimulationManager] Initialized. RuntimeUnitCount={_runtimeUnits.Count}", this);
        }
    }

    private void Update()
    {
        if (!_initialized || _battleFinished || _isTemporarilyPaused)
        {
            return;
        }

        float scaledDeltaTime = Time.unscaledDeltaTime * Mathf.Max(0f, simulationSpeedMultiplier);
        _tickAccumulator += scaledDeltaTime;

        while (_tickAccumulator >= _tickInterval)
        {
            _tickAccumulator -= _tickInterval;
            StepSimulation(_tickInterval);

            if (_battleFinished)
            {
                break;
            }
        }
    }

    public void SetSimulationSpeedMultiplier(float multiplier)
    {
        simulationSpeedMultiplier = Mathf.Clamp(multiplier, minSimulationSpeed, maxSimulationSpeed);

        if (_statusGridUIManager != null)
        {
            _statusGridUIManager.Refresh();
        }
    }

    public void MultiplySimulationSpeed(float multiplier)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        SetSimulationSpeedMultiplier(simulationSpeedMultiplier * multiplier);
    }

    public void SetTemporaryPause(bool isPaused)
    {
        _isTemporarilyPaused = isPaused;
    }

    private void StepSimulation(float tickDeltaTime)
    {
        TickAllCooldowns(tickDeltaTime);
        ComputeAllParameters();
        EvaluateAllActionScores();
        CommitOrSwitchActions(tickDeltaTime);
        BuildAllExecutionPlans();
        ExecuteMovementPhase(tickDeltaTime);
        ResolveUnitSeparation();
        ExecuteAttackPhase();
        TryFinishBattle();

        if (_statusGridUIManager != null)
        {
            _statusGridUIManager.Refresh();
        }
    }

    private void TickAllCooldowns(float tickDeltaTime)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            unit.TickAttackCooldown(tickDeltaTime);
        }
    }

    private void ComputeAllParameters()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            BattleParameterSet rawParameters = ComputeParametersForUnit(unit);
            BattleParameterSet modifiedParameters = ApplyCurrentActionParameterModifiers(unit, rawParameters);

            rawParameters.Clamp01All();
            modifiedParameters.Clamp01All();

            unit.SetCurrentParameters(rawParameters, modifiedParameters);
        }
    }

    private void EvaluateAllActionScores()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            BattleActionScoreSet scores = EvaluateActionScores(unit, unit.CurrentModifiedParameters);
            scores = ApplyEscapeReengageBias(unit, unit.CurrentRawParameters, scores);      //도망이 너무 길어져서, 이것도 결과값에 보정을 줍니다.
            unit.SetCurrentScores(scores);
        }
    }

    private void CommitOrSwitchActions(float tickDeltaTime)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            BattleActionScoreSet scores = unit.CurrentScores;
            BattleActionType currentAction = unit.CurrentActionType;

            GetBestActionRespectingEscapeLimit(
                unit,
                scores,
                BattleActionType.None,
                out BattleActionType bestAction,
                out float bestScore);

            if (currentAction == BattleActionType.None)
            {
                EnterAction(unit, bestAction, bestScore);
                EnsureCurrentActionIsUsableOrFallback(unit);
                continue;
            }

            float decayedKeepBehaving = unit.KeepBehaving - (commitmentDecayPerSecond * tickDeltaTime);
            float nextActionTimer = unit.ActionTimer + tickDeltaTime;

            GetBestActionRespectingEscapeLimit(
                unit,
                scores,
                currentAction,
                out BattleActionType bestOtherAction,
                out float bestOtherScore);

            if (bestOtherScore > decayedKeepBehaving)
            {
                EnterAction(unit, bestOtherAction, bestOtherScore);
                EnsureCurrentActionIsUsableOrFallback(unit);

                if (verboseLog)
                {
                    Debug.Log(
                        $"[BattleSimulationManager] Action switched. Unit={unit.UnitNumber}, " +
                        $"From={currentAction}, To={unit.CurrentActionType}, " +
                        $"BestOther={bestOtherScore:0.##}, PreviousKeep={decayedKeepBehaving:0.##}",
                        this
                    );
                }
            }
            else
            {
                unit.SetCurrentActionType(currentAction, GetActionDisplayName(currentAction));
                unit.SetDecisionState(decayedKeepBehaving, nextActionTimer);
                EnsureCurrentActionIsUsableOrFallback(unit);
            }
        }
    }

    private void EnterAction(BattleRuntimeUnit unit, BattleActionType actionType, float chosenFinalScore)
    {
        if (unit == null)
        {
            return;
        }

        float keepBehaving = chosenFinalScore * CommitmentEnterMultiplier;

        unit.SetCurrentActionType(actionType, GetActionDisplayName(actionType));
        unit.SetDecisionState(keepBehaving, 0f);
    }

    private int GetLivingUnitCountForDecision()
    {
        int count = 0;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private int GetCurrentEscapeUnitCount()
    {
        int count = 0;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            if (unit.CurrentActionType == BattleActionType.EscapeFromPressure)
            {
                count++;
            }
        }

        return count;
    }

    private int GetMaxEscapeUnitCount()
    {
        int livingUnitCount = GetLivingUnitCountForDecision();
        int maxEscapeCount = Mathf.FloorToInt(livingUnitCount * 0.3f);
        return Mathf.Max(1, maxEscapeCount);
    }

    private bool CanEnterEscapeAction(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.IsCombatDisabled)
        {
            return false;
        }

        // 이미 Escape 중인 유닛은 슬롯 유지 허용
        if (unit.CurrentActionType == BattleActionType.EscapeFromPressure)
        {
            return true;
        }

        return GetCurrentEscapeUnitCount() < GetMaxEscapeUnitCount();
    }

    private void GetBestActionRespectingEscapeLimit(
        BattleRuntimeUnit unit,
        BattleActionScoreSet scores,
        BattleActionType excludedAction,
        out BattleActionType bestAction,
        out float bestScore)
    {
        bool canEnterEscape = CanEnterEscapeAction(unit);

        bestAction = BattleActionType.None;
        bestScore = float.MinValue;

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.AssassinateIsolatedEnemy,
            scores.AssassinateIsolatedEnemy,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.DiveEnemyBackline,
            scores.DiveEnemyBackline,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.PeelForWeakAlly,
            scores.PeelForWeakAlly,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.EscapeFromPressure,
            scores.EscapeFromPressure,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.RegroupToAllies,
            scores.RegroupToAllies,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.CollapseOnCluster,
            scores.CollapseOnCluster,
            excludedAction,
            canEnterEscape);

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.EngageNearest,
            scores.EngageNearest,
            excludedAction,
            canEnterEscape);

        if (bestAction == BattleActionType.None)
        {
            bestAction = BattleActionType.EngageNearest;
            bestScore = scores.EngageNearest;
        }
    }

    private void TryConsiderActionRespectingEscapeLimit(
        ref BattleActionType bestAction,
        ref float bestScore,
        BattleActionType candidateAction,
        float candidateScore,
        BattleActionType excludedAction,
        bool canEnterEscape)
    {
        if (candidateAction == excludedAction)
        {
            return;
        }

        if (candidateAction == BattleActionType.EscapeFromPressure && !canEnterEscape)
        {
            return;
        }

        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }

    private void EnsureCurrentActionIsUsableOrFallback(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.IsCombatDisabled)
        {
            return;
        }

        BattleActionType currentAction = unit.CurrentActionType;
        if (currentAction == BattleActionType.None)
        {
            return;
        }

        BattleActionExecutionPlan currentPlan = BuildExecutionPlan(unit, currentAction);
        if (IsExecutionPlanUsable(unit, currentPlan))
        {
            return;
        }

        if (currentAction == BattleActionType.EngageNearest)
        {
            return;
        }

        float engageScore = unit.CurrentScores.GetScore(BattleActionType.EngageNearest);
        EnterAction(unit, BattleActionType.EngageNearest, engageScore);

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSimulationManager] Unusable plan fallback. Unit={unit.UnitNumber}, " +
                $"From={currentAction}, To={BattleActionType.EngageNearest}",
                this
            );
        }
    }

    private void BuildAllExecutionPlans()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            BattleActionExecutionPlan plan = BuildExecutionPlan(unit, unit.CurrentActionType);

            if (!IsExecutionPlanUsable(unit, plan))
            {
                BattleActionExecutionPlan engagePlan = BuildExecutionPlan(unit, BattleActionType.EngageNearest);

                if (IsExecutionPlanUsable(unit, engagePlan))
                {
                    plan = engagePlan;
                }
                else
                {
                    plan = default;
                    plan.Action = unit.CurrentActionType;
                    plan.TargetEnemy = null;
                    plan.TargetAlly = null;
                    plan.DesiredPosition = unit.AnchoredPosition;
                    plan.HasDesiredPosition = false;
                }
            }

            unit.SetExecutionPlan(plan);
        }
    }

    private void ExecuteMovementPhase(float tickDeltaTime)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            BattleRuntimeUnit targetEnemy = unit.PlannedTargetEnemy;

            if (IsValidEnemyTarget(unit, targetEnemy) && IsWithinEffectiveAttackDistance(unit, targetEnemy))
            {
                unit.SetAttackState(true);
                continue;
            }

            if (unit.HasPlannedDesiredPosition)
            {
                bool moved = MoveTowardsPosition(unit, unit.PlannedDesiredPosition, tickDeltaTime);
                unit.SetMovementState(moved);

                if (!moved)
                {
                    unit.SetIdleState();
                }

                continue;
            }

            if (IsValidEnemyTarget(unit, targetEnemy))
            {
                bool moved = MoveTowardsTarget(unit, targetEnemy, tickDeltaTime);
                unit.SetMovementState(moved);

                if (!moved)
                {
                    unit.SetIdleState();
                }

                continue;
            }

            unit.SetIdleState();
        }
    }

    private void ExecuteAttackPhase()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit attacker = _runtimeUnits[i];
            if (attacker == null || attacker.IsCombatDisabled)
            {
                continue;
            }

            BattleRuntimeUnit target = attacker.PlannedTargetEnemy;
            if (!IsValidEnemyTarget(attacker, target))
            {
                continue;
            }

            if (!IsWithinEffectiveAttackDistance(attacker, target))
            {
                continue;
            }

            attacker.SetAttackState(true);

            if (attacker.AttackCooldownRemaining > 0f)
            {
                continue;
            }

            target.ApplyDamage(attacker.Attack);
            attacker.ResetAttackCooldown();

            if (verboseLog)
            {
                Debug.Log(
                    $"[BattleSimulationManager] Attack resolved. Attacker={attacker.UnitNumber}, Target={target.UnitNumber}, Damage={attacker.Attack:0.##}, TargetHP={target.CurrentHealth:0.##}/{target.MaxHealth:0.##}",
                    this
                );
            }
        }
    }

    private BattleParameterSet ComputeParametersForUnit(BattleRuntimeUnit self)
    {
        BattleParameterSet parameters = default;

        parameters.SelfHpLow = ComputeSelfHpLow(self);
        parameters.SelfSurroundedByEnemies = ComputeSelfSurroundedByEnemies(self);
        parameters.LowHealthAllyProximity = ComputeLowHealthAllyProximity(self);
        parameters.AllyUnderFocusPressure = ComputeAllyUnderFocusPressure(self);
        parameters.AllyFrontlineGap = ComputeAllyFrontlineGap(self);
        parameters.IsolatedEnemyVulnerability = ComputeIsolatedEnemyVulnerability(self);
        parameters.EnemyClusterDensity = ComputeEnemyClusterDensity(self);
        parameters.DistanceToTeamCenter = ComputeDistanceToTeamCenter(self);
        parameters.SelfCanAttackNow = ComputeSelfCanAttackNow(self);

        parameters.Clamp01All();
        return parameters;
    }

    private BattleParameterSet ApplyCurrentActionParameterModifiers(BattleRuntimeUnit unit, BattleParameterSet rawParameters)
    {
        if (unit == null)
        {
            return rawParameters;
        }

        BattleActionTuning tuning = GetActionTuning(unit.CurrentActionType);
        if (tuning == null || unit.CurrentActionType == BattleActionType.None)
        {
            return rawParameters;
        }

        BattleParameterSet modified = tuning.currentActionParameterPercents.ApplyPercentModifiers(rawParameters);
        modified.Clamp01All();
        return modified;
    }

    private BattleActionScoreSet EvaluateActionScores(BattleRuntimeUnit unit, BattleParameterSet modifiedParameters)
    {
        BattleActionScoreSet scores = default;

        for (int i = 0; i < actionTunings.Count; i++)
        {
            BattleActionTuning tuning = actionTunings[i];
            if (tuning == null || tuning.actionType == BattleActionType.None)
            {
                continue;
            }

            float rawScore = tuning.baseBias + tuning.scoreWeights.Evaluate(modifiedParameters);

            int weaponTypePercent = GetWeaponTypeAffinityPercent(unit, tuning.actionType);
            int personalityPercent = GetPersonalityAffinityPercent(unit, tuning.actionType);

            float finalScore = rawScore * (weaponTypePercent / 100f) * (personalityPercent / 100f);
            scores.SetScore(tuning.actionType, finalScore);
        }

        return scores;
    }

    private int GetWeaponTypeAffinityPercent(BattleRuntimeUnit unit, BattleActionType actionType)
    {
        BattleActionTuning tuning = GetActionTuning(actionType);
        if (tuning == null)
        {
            return 100;
        }

        if (unit == null || unit.Snapshot == null)
        {
            return 100;
        }

        return tuning.GetWeaponTypePercent(unit.Snapshot.WeaponType);
    }

    private int GetPersonalityAffinityPercent(BattleRuntimeUnit unit, BattleActionType actionType)
    {
        return 100;
    }

    private BattleActionExecutionPlan BuildExecutionPlan(BattleRuntimeUnit unit, BattleActionType actionType)
    {
        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
                return BuildAssassinatePlan(unit);

            case BattleActionType.DiveEnemyBackline:
                return BuildDiveBacklinePlan(unit);

            case BattleActionType.PeelForWeakAlly:
                return BuildPeelPlan(unit);

            case BattleActionType.EscapeFromPressure:
                return BuildEscapePlan(unit);

            case BattleActionType.RegroupToAllies:
                return BuildRegroupPlan(unit);

            case BattleActionType.CollapseOnCluster:
                return BuildCollapsePlan(unit);

            case BattleActionType.EngageNearest:
            default:
                return BuildEngageNearestPlan(unit);
        }
    }

    private BattleActionExecutionPlan BuildAssassinatePlan(BattleRuntimeUnit unit)
    {
        BattleRuntimeUnit target = FindBestIsolatedEnemy(unit);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.AssassinateIsolatedEnemy,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.AnchoredPosition : unit.AnchoredPosition,
            HasDesiredPosition = target != null
        };
    }

    private BattleActionExecutionPlan BuildDiveBacklinePlan(BattleRuntimeUnit unit)
    {
        BattleRuntimeUnit target = FindBestBacklineEnemy(unit);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.DiveEnemyBackline,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.AnchoredPosition : unit.AnchoredPosition,
            HasDesiredPosition = target != null
        };
    }

    private BattleActionExecutionPlan BuildPeelPlan(BattleRuntimeUnit unit)
    {
        BattleRuntimeUnit ally = FindMostPressuredAlly(unit);
        BattleRuntimeUnit enemy = FindBestPeelEnemy(unit, ally);

        Vector2 desiredPosition = ally != null ? ally.AnchoredPosition : unit.AnchoredPosition;
        bool hasDesiredPosition = ally != null;

        if (enemy != null)
        {
            desiredPosition = enemy.AnchoredPosition;
            hasDesiredPosition = true;
        }

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.PeelForWeakAlly,
            TargetEnemy = enemy,
            TargetAlly = ally,
            DesiredPosition = desiredPosition,
            HasDesiredPosition = hasDesiredPosition
        };
    }

    private BattleActionExecutionPlan BuildEscapePlan(BattleRuntimeUnit unit)
    {
        Vector2 selfPos = unit.AnchoredPosition;
        Vector2 pressureCenter = ComputeEnemyPressureCenter(unit);
        Vector2 away = selfPos - pressureCenter;

        if (away.sqrMagnitude < 0.0001f)
        {
            away = unit.IsEnemy ? Vector2.right : Vector2.left;
        }

        away.Normalize();

        Vector2 teamCenter = ComputeTeamCenter(unit.IsEnemy);
        Vector2 towardTeam = (teamCenter - selfPos).normalized;

        Vector2 escapeDirection = (away * (1f - escapeTowardTeamBlend) + towardTeam * escapeTowardTeamBlend).normalized;
        Vector2 desiredPosition = selfPos + escapeDirection * Mathf.Max(80f, unit.MoveSpeed);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EscapeFromPressure,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = desiredPosition,
            HasDesiredPosition = true
        };
    }

    private BattleActionExecutionPlan BuildRegroupPlan(BattleRuntimeUnit unit)
    {
        Vector2 teamCenter = ComputeTeamCenter(unit.IsEnemy);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.RegroupToAllies,
            TargetEnemy = null,
            TargetAlly = null,
            DesiredPosition = teamCenter,
            HasDesiredPosition = true
        };
    }

    private BattleActionExecutionPlan BuildCollapsePlan(BattleRuntimeUnit unit)
    {
        Vector2 enemyClusterCenter = ComputeTeamCenter(!unit.IsEnemy);
        BattleRuntimeUnit target = FindEnemyClosestToPoint(unit, enemyClusterCenter);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.CollapseOnCluster,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = enemyClusterCenter,
            HasDesiredPosition = true
        };
    }

    private BattleActionExecutionPlan BuildEngageNearestPlan(BattleRuntimeUnit unit)
    {
        BattleRuntimeUnit target = FindNearestLivingEnemy(unit);

        return new BattleActionExecutionPlan
        {
            Action = BattleActionType.EngageNearest,
            TargetEnemy = target,
            TargetAlly = null,
            DesiredPosition = target != null ? target.AnchoredPosition : unit.AnchoredPosition,
            HasDesiredPosition = target != null
        };
    }

    private bool IsExecutionPlanUsable(BattleRuntimeUnit unit, BattleActionExecutionPlan plan)
    {
        switch (plan.Action)
        {
            case BattleActionType.EscapeFromPressure:
            case BattleActionType.RegroupToAllies:
            case BattleActionType.CollapseOnCluster:
                return plan.HasDesiredPosition || IsValidEnemyTarget(unit, plan.TargetEnemy);

            case BattleActionType.AssassinateIsolatedEnemy:
            case BattleActionType.DiveEnemyBackline:
            case BattleActionType.PeelForWeakAlly:
            case BattleActionType.EngageNearest:
                return IsValidEnemyTarget(unit, plan.TargetEnemy);

            default:
                return false;
        }
    }

    private float ComputeSelfHpLow(BattleRuntimeUnit self)
    {
        if (self == null || self.MaxHealth <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - (self.CurrentHealth / self.MaxHealth));
    }

    private float ComputeSelfSurroundedByEnemies(BattleRuntimeUnit self)
    {
        float sum = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float distance = Vector2.Distance(self.AnchoredPosition, enemy.AnchoredPosition);
            float weight = QuadraticCloseFalloff(distance, surroundRadius);
            sum += weight;
        }

        return Mathf.Clamp01(sum / 3f);
    }

    private float ComputeLowHealthAllyProximity(BattleRuntimeUnit self)
    {
        float sum = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit ally = _runtimeUnits[i];
            if (!IsValidSameTeamAlly(self, ally))
            {
                continue;
            }

            float hpLow = ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector2.Distance(self.AnchoredPosition, ally.AnchoredPosition), helpRadius);
            sum += hpLow * distanceWeight;
        }

        return Mathf.Clamp01(sum / 2f);
    }

    private float ComputeAllyUnderFocusPressure(BattleRuntimeUnit self)
    {
        float best = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit ally = _runtimeUnits[i];
            if (!IsValidSameTeamAlly(self, ally))
            {
                continue;
            }

            int focusCount = CountEnemiesTargeting(self, ally);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpFactor = 0.5f + 0.5f * ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector2.Distance(self.AnchoredPosition, ally.AnchoredPosition), peelRadius);

            float value = focusRatio * hpFactor * distanceWeight;
            if (value > best)
            {
                best = value;
            }
        }

        return Mathf.Clamp01(best);
    }

    private float ComputeAllyFrontlineGap(BattleRuntimeUnit self)
    {
        List<BattleRuntimeUnit> allies = GetLivingUnits(self.IsEnemy);
        if (allies.Count <= 1)
        {
            return 0f;
        }

        float sumNearest = 0f;
        int count = 0;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            float nearest = float.MaxValue;

            for (int j = 0; j < allies.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                float distance = Vector2.Distance(ally.AnchoredPosition, allies[j].AnchoredPosition);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            if (nearest < float.MaxValue)
            {
                sumNearest += nearest;
                count++;
            }
        }

        if (count == 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((sumNearest / count) / frontlineGapRadius);
    }

    private float ComputeIsolatedEnemyVulnerability(BattleRuntimeUnit self)
    {
        float best = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float score = ComputeIsolatedEnemyTargetScore(self, enemy);
            if (score > best)
            {
                best = score;
            }
        }

        return Mathf.Clamp01(best);
    }

    private float ComputeEnemyClusterDensity(BattleRuntimeUnit self)
    {
        List<BattleRuntimeUnit> enemies = GetLivingUnits(!self.IsEnemy);
        if (enemies.Count <= 1)
        {
            return 0f;
        }

        float sum = 0f;
        int pairCount = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            for (int j = i + 1; j < enemies.Count; j++)
            {
                float distance = Vector2.Distance(enemies[i].AnchoredPosition, enemies[j].AnchoredPosition);
                sum += LinearFalloff(distance, clusterRadius);
                pairCount++;
            }
        }

        if (pairCount == 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(sum / pairCount);
    }

    private float ComputeDistanceToTeamCenter(BattleRuntimeUnit self)
    {
        Vector2 teamCenter = ComputeTeamCenter(self.IsEnemy);
        float distance = Vector2.Distance(self.AnchoredPosition, teamCenter);
        return Mathf.Clamp01(distance / teamCenterDistanceRadius);
    }

    private float ComputeSelfCanAttackNow(BattleRuntimeUnit self)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            if (IsWithinEffectiveAttackDistance(self, enemy))
            {
                return 1f;
            }
        }

        return 0f;
    }

    private float ComputeIsolatedEnemyTargetScore(BattleRuntimeUnit self, BattleRuntimeUnit enemy)
    {
        if (!IsValidEnemyTarget(self, enemy))
        {
            return 0f;
        }

        float nearestSupportDistance = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit otherEnemy = _runtimeUnits[i];
            if (otherEnemy == null || otherEnemy == enemy || otherEnemy.IsCombatDisabled || otherEnemy.IsEnemy != enemy.IsEnemy)
            {
                continue;
            }

            float distance = Vector2.Distance(enemy.AnchoredPosition, otherEnemy.AnchoredPosition);
            if (distance < nearestSupportDistance)
            {
                nearestSupportDistance = distance;
            }
        }

        if (nearestSupportDistance == float.MaxValue)
        {
            nearestSupportDistance = isolationRadius;
        }

        float isolation = Mathf.Clamp01(nearestSupportDistance / isolationRadius);
        float hpLow = ComputeSelfHpLow(enemy);
        float reachFactor = 0.35f + 0.65f * LinearFalloff(Vector2.Distance(self.AnchoredPosition, enemy.AnchoredPosition), assassinReachRadius);

        return isolation * (0.6f + 0.4f * hpLow) * reachFactor;
    }

    private int CountEnemiesTargeting(BattleRuntimeUnit self, BattleRuntimeUnit ally)
    {
        int count = 0;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(ally, enemy))
            {
                continue;
            }

            if (enemy.CurrentTarget == ally || enemy.PlannedTargetEnemy == ally)
            {
                count++;
            }
        }

        return count;
    }

    private BattleRuntimeUnit FindBestIsolatedEnemy(BattleRuntimeUnit self)
    {
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float score = ComputeIsolatedEnemyTargetScore(self, enemy);
            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private BattleRuntimeUnit FindBestBacklineEnemy(BattleRuntimeUnit self)
    {
        Vector2 enemyCenter = ComputeTeamCenter(!self.IsEnemy);

        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float hpLow = ComputeSelfHpLow(enemy);
            float isolation = ComputeIsolatedEnemyTargetScore(self, enemy);
            float backlineFactor = Mathf.Clamp01(Vector2.Distance(enemy.AnchoredPosition, enemyCenter) / teamCenterDistanceRadius);

            float score = hpLow * 0.45f + isolation * 0.35f + backlineFactor * 0.20f;

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        return best;
    }

    private BattleRuntimeUnit FindMostPressuredAlly(BattleRuntimeUnit self)
    {
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit ally = _runtimeUnits[i];
            if (!IsValidSameTeamAlly(self, ally))
            {
                continue;
            }

            int focusCount = CountEnemiesTargeting(self, ally);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpFactor = 0.5f + 0.5f * ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector2.Distance(self.AnchoredPosition, ally.AnchoredPosition), peelRadius);

            float score = focusRatio * hpFactor * distanceWeight;
            if (score > bestScore)
            {
                bestScore = score;
                best = ally;
            }
        }

        return best;
    }

    private BattleRuntimeUnit FindBestPeelEnemy(BattleRuntimeUnit self, BattleRuntimeUnit protectedAlly)
    {
        if (protectedAlly == null)
        {
            return FindNearestLivingEnemy(self);
        }

        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            bool attackingProtectedAlly = enemy.CurrentTarget == protectedAlly || enemy.PlannedTargetEnemy == protectedAlly;
            if (!attackingProtectedAlly)
            {
                continue;
            }

            float distanceToAlly = Vector2.Distance(enemy.AnchoredPosition, protectedAlly.AnchoredPosition);
            float score = 1f - Mathf.Clamp01(distanceToAlly / peelRadius);

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        if (best != null)
        {
            return best;
        }

        return FindNearestLivingEnemy(self);
    }

    private BattleRuntimeUnit FindEnemyClosestToPoint(BattleRuntimeUnit self, Vector2 point)
    {
        BattleRuntimeUnit best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float distance = Vector2.Distance(enemy.AnchoredPosition, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }

        return best;
    }

    private Vector2 ComputeTeamCenter(bool isEnemyTeam)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy != isEnemyTeam)
            {
                continue;
            }

            sum += unit.AnchoredPosition;
            count++;
        }

        return count > 0 ? sum / count : Vector2.zero;
    }

    private Vector2 ComputeEnemyPressureCenter(BattleRuntimeUnit self)
    {
        Vector2 weightedSum = Vector2.zero;
        float weightSum = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
            {
                continue;
            }

            float distance = Vector2.Distance(self.AnchoredPosition, enemy.AnchoredPosition);
            float weight = QuadraticCloseFalloff(distance, surroundRadius);
            weightedSum += enemy.AnchoredPosition * weight;
            weightSum += weight;
        }

        if (weightSum <= 0.0001f)
        {
            return ComputeTeamCenter(!self.IsEnemy);
        }

        return weightedSum / weightSum;
    }

    private bool MoveTowardsTarget(BattleRuntimeUnit mover, BattleRuntimeUnit target, float tickDeltaTime)
    {
        if (mover == null || target == null)
        {
            return false;
        }

        Vector2 currentPosition = mover.AnchoredPosition;
        Vector2 targetPosition = target.AnchoredPosition;
        Vector2 toTarget = targetPosition - currentPosition;
        float centerDistance = toTarget.magnitude;
        float effectiveAttackDistance = GetEffectiveAttackDistance(mover, target);

        if (centerDistance <= effectiveAttackDistance)
        {
            return false;
        }

        Vector2 direction = centerDistance > 0.0001f ? toTarget / centerDistance : Vector2.zero;
        float remainingDistanceUntilAttack = Mathf.Max(0f, centerDistance - effectiveAttackDistance);
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, remainingDistanceUntilAttack);

        if (moveDistance <= 0.0001f)
        {
            return false;
        }

        mover.SetAnchoredPosition(currentPosition + direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldRect);
        return true;
    }

    private bool MoveTowardsPosition(BattleRuntimeUnit mover, Vector2 desiredPosition, float tickDeltaTime)
    {
        if (mover == null)
        {
            return false;
        }

        Vector2 currentPosition = mover.AnchoredPosition;
        Vector2 toTarget = desiredPosition - currentPosition;
        float distance = toTarget.magnitude;

        if (distance <= desiredPositionStopDistance)
        {
            return false;
        }

        Vector2 direction = distance > 0.0001f ? toTarget / distance : Vector2.zero;
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, distance);

        if (moveDistance <= 0.0001f)
        {
            return false;
        }

        mover.SetAnchoredPosition(currentPosition + direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldRect);
        return true;
    }

    private BattleActionScoreSet ApplyEscapeReengageBias(
    BattleRuntimeUnit unit,
    BattleParameterSet rawParameters,
    BattleActionScoreSet scores)
    {
        if (unit == null || unit.CurrentActionType != BattleActionType.EscapeFromPressure)
        {
            return scores;
        }

        bool noEnemyInAttackRange = rawParameters.SelfCanAttackNow <= 0f;
        bool pressureMostlyGone = rawParameters.SelfSurroundedByEnemies <= 0.20f;

        if (!noEnemyInAttackRange || !pressureMostlyGone)
        {
            return scores;
        }

        // 도망은 확 깎고
        scores.EscapeFromPressure *= 0.10f;

        // 다시 전투 쪽 행동들을 올린다
        scores.AssassinateIsolatedEnemy *= 1.50f;
        scores.DiveEnemyBackline *= 1.35f;
        scores.CollapseOnCluster *= 1.30f;

        return scores;
    }

    private void ResolveUnitSeparation()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit a = _runtimeUnits[i];
            if (a == null || a.IsCombatDisabled)
            {
                continue;
            }

            for (int j = i + 1; j < _runtimeUnits.Count; j++)
            {
                BattleRuntimeUnit b = _runtimeUnits[j];
                if (b == null || b.IsCombatDisabled)
                {
                    continue;
                }

                Vector2 delta = a.AnchoredPosition - b.AnchoredPosition;
                float distance = delta.magnitude;
                float minDistance = a.BodyRadius + b.BodyRadius;

                if (distance >= minDistance)
                {
                    continue;
                }

                Vector2 pushDirection;
                if (distance > 0.0001f)
                {
                    pushDirection = delta / distance;
                }
                else
                {
                    pushDirection = (a.UnitNumber <= b.UnitNumber) ? Vector2.left : Vector2.right;
                    distance = 0f;
                }

                float overlap = minDistance - distance;
                Vector2 push = pushDirection * (overlap * 0.5f);

                a.SetAnchoredPosition(a.AnchoredPosition + push);
                b.SetAnchoredPosition(b.AnchoredPosition - push);

                a.ClampInsideBattlefield(_battlefieldRect);
                b.ClampInsideBattlefield(_battlefieldRect);
            }
        }
    }

    private void TryFinishBattle()
    {
        bool hasLivingAlly = false;
        bool hasLivingEnemy = false;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            if (unit.IsEnemy)
            {
                hasLivingEnemy = true;
            }
            else
            {
                hasLivingAlly = true;
            }

            if (hasLivingAlly && hasLivingEnemy)
            {
                return;
            }
        }

        bool wasWin = hasLivingAlly && !hasLivingEnemy;
        int currentDay = SessionManager.Instance != null ? Mathf.Max(1, SessionManager.Instance.CurrentDay) : 1;
        int pendingReward = wasWin ? CalculateVictoryReward(currentDay) : 0;

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.SetPendingBattleReward(pendingReward);
        }

        BattleResolution resolution = BattleResolution.Create(wasWin, pendingReward, currentDay);

        _battleFinished = true;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            unit.SetIdleState();
        }

        if (_statusGridUIManager != null)
        {
            _statusGridUIManager.Refresh();
        }

        if (_battleSceneUIManager != null)
        {
            _battleSceneUIManager.ShowBattleEndPanel(resolution);
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleSimulationManager] Battle finished. WasWin={wasWin}, PendingReward={pendingReward}, Day={currentDay}",
                this
            );
        }
    }

    private int CalculateVictoryReward(int currentDay)
    {
        BalanceSO balance = ContentDatabaseProvider.Instance != null ? ContentDatabaseProvider.Instance.Balance : null;
        int rewardPerDay = balance != null ? Mathf.Max(0, balance.battleVictoryRewardPerDay) : 100;
        return Mathf.Max(0, currentDay) * rewardPerDay;
    }

    private BattleRuntimeUnit FindNearestLivingEnemy(BattleRuntimeUnit requester)
    {
        if (requester == null)
        {
            return null;
        }

        BattleRuntimeUnit nearest = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit candidate = _runtimeUnits[i];
            if (!IsValidEnemyTarget(requester, candidate))
            {
                continue;
            }

            float distanceSqr = (candidate.AnchoredPosition - requester.AnchoredPosition).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private List<BattleRuntimeUnit> GetLivingUnits(bool isEnemyTeam)
    {
        List<BattleRuntimeUnit> result = new List<BattleRuntimeUnit>();

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy != isEnemyTeam)
            {
                continue;
            }

            result.Add(unit);
        }

        return result;
    }

    private bool IsValidEnemyTarget(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
        {
            return false;
        }

        if (requester == candidate)
        {
            return false;
        }

        if (requester.IsEnemy == candidate.IsEnemy)
        {
            return false;
        }

        if (candidate.IsCombatDisabled)
        {
            return false;
        }

        return true;
    }

    private bool IsValidSameTeamAlly(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
        {
            return false;
        }

        if (requester == candidate)
        {
            return false;
        }

        if (requester.IsEnemy != candidate.IsEnemy)
        {
            return false;
        }

        if (candidate.IsCombatDisabled)
        {
            return false;
        }

        return true;
    }

    private bool IsValidSameTeamAllyOrSelf(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
        {
            return false;
        }

        if (requester.IsEnemy != candidate.IsEnemy)
        {
            return false;
        }

        if (candidate.IsCombatDisabled)
        {
            return false;
        }

        return true;
    }

    private bool IsWithinEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
        {
            return false;
        }

        float distance = Vector2.Distance(attacker.AnchoredPosition, target.AnchoredPosition);
        return distance <= GetEffectiveAttackDistance(attacker, target);
    }

    private float GetEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        return attacker.BodyRadius + target.BodyRadius + attacker.AttackRange;
    }

    private float LinearFalloff(float distance, float radius)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, 1f - distance / radius);
    }

    private float QuadraticCloseFalloff(float distance, float radius)
    {
        float linear = LinearFalloff(distance, radius);
        return linear * linear;
    }

    private string GetActionDisplayName(BattleActionType actionType)
    {
        BattleActionTuning tuning = GetActionTuning(actionType);
        if (tuning != null && !string.IsNullOrWhiteSpace(tuning.displayName))
        {
            return tuning.displayName;
        }

        return actionType.ToString();
    }

    private BattleActionTuning GetActionTuning(BattleActionType actionType)
    {
        for (int i = 0; i < actionTunings.Count; i++)
        {
            BattleActionTuning tuning = actionTunings[i];
            if (tuning != null && tuning.actionType == actionType)
            {
                return tuning;
            }
        }

        return null;
    }

    private void EnsureDefaultActionTunings()
    {
        if (actionTunings == null)
        {
            actionTunings = new List<BattleActionTuning>();
        }

        EnsureActionTuningExists(BattleActionType.AssassinateIsolatedEnemy);
        EnsureActionTuningExists(BattleActionType.DiveEnemyBackline);
        EnsureActionTuningExists(BattleActionType.PeelForWeakAlly);
        EnsureActionTuningExists(BattleActionType.EscapeFromPressure);
        EnsureActionTuningExists(BattleActionType.RegroupToAllies);
        EnsureActionTuningExists(BattleActionType.CollapseOnCluster);
        EnsureActionTuningExists(BattleActionType.EngageNearest);
    }

    private void EnsureActionTuningExists(BattleActionType actionType)
    {
        if (GetActionTuning(actionType) != null)
        {
            return;
        }

        actionTunings.Add(CreateDefaultTuning(actionType));
    }

    private BattleActionTuning CreateDefaultTuning(BattleActionType actionType)
    {
        BattleActionTuning tuning = new BattleActionTuning
        {
            actionType = actionType,
            displayName = actionType.ToString(),
            currentActionParameterPercents = BattleParameterWeights.CreateFilled(100)
        };

        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
                tuning.baseBias = 1;
                tuning.scoreWeights.selfHpLow = -8;
                tuning.scoreWeights.selfSurroundedByEnemies = -7;
                tuning.scoreWeights.lowHealthAllyProximity = -3;
                tuning.scoreWeights.allyUnderFocusPressure = -4;
                tuning.scoreWeights.allyFrontlineGap = -2;
                tuning.scoreWeights.isolatedEnemyVulnerability = 10;
                tuning.scoreWeights.enemyClusterDensity = -8;
                tuning.scoreWeights.distanceToTeamCenter = -3;
                tuning.scoreWeights.selfCanAttackNow = 4;

                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 120;
                tuning.currentActionParameterPercents.enemyClusterDensity = 60;
                tuning.currentActionParameterPercents.selfCanAttackNow = 35;

                tuning.macePercent = 70;
                tuning.swordPercent = 115;
                tuning.orbPercent = 100;
                tuning.crossbowPercent = 120;
                tuning.daggerPercent = 180;
                break;

            case BattleActionType.DiveEnemyBackline:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = -9;
                tuning.scoreWeights.selfSurroundedByEnemies = -4;
                tuning.scoreWeights.lowHealthAllyProximity = -2;
                tuning.scoreWeights.allyUnderFocusPressure = -3;
                tuning.scoreWeights.allyFrontlineGap = -1;
                tuning.scoreWeights.isolatedEnemyVulnerability = 8;
                tuning.scoreWeights.enemyClusterDensity = -4;
                tuning.scoreWeights.distanceToTeamCenter = 2;
                tuning.scoreWeights.selfCanAttackNow = 5;

                tuning.currentActionParameterPercents.allyUnderFocusPressure = 75;
                tuning.currentActionParameterPercents.enemyClusterDensity = 75;
                tuning.currentActionParameterPercents.selfCanAttackNow = 45;

                tuning.macePercent = 70;
                tuning.swordPercent = 130;
                tuning.orbPercent = 65;
                tuning.crossbowPercent = 80;
                tuning.daggerPercent = 150;
                break;

            case BattleActionType.PeelForWeakAlly:
                tuning.baseBias = 2;
                tuning.scoreWeights.selfHpLow = -3;
                tuning.scoreWeights.selfSurroundedByEnemies = -1;
                tuning.scoreWeights.lowHealthAllyProximity = 9;
                tuning.scoreWeights.allyUnderFocusPressure = 10;
                tuning.scoreWeights.allyFrontlineGap = 5;
                tuning.scoreWeights.isolatedEnemyVulnerability = 1;
                tuning.scoreWeights.enemyClusterDensity = 1;
                tuning.scoreWeights.distanceToTeamCenter = -5;
                tuning.scoreWeights.selfCanAttackNow = 2;

                tuning.currentActionParameterPercents.allyUnderFocusPressure = 130;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 35;
                tuning.currentActionParameterPercents.enemyClusterDensity = 50;
                tuning.currentActionParameterPercents.selfCanAttackNow = 25;

                tuning.macePercent = 140;
                tuning.swordPercent = 90;
                tuning.orbPercent = 95;
                tuning.crossbowPercent = 105;
                tuning.daggerPercent = 35;
                break;

            case BattleActionType.EscapeFromPressure:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = 10;
                tuning.scoreWeights.selfSurroundedByEnemies = 10;
                tuning.scoreWeights.lowHealthAllyProximity = -3;
                tuning.scoreWeights.allyUnderFocusPressure = -2;
                tuning.scoreWeights.allyFrontlineGap = 2;
                tuning.scoreWeights.isolatedEnemyVulnerability = -4;
                tuning.scoreWeights.enemyClusterDensity = 6;
                tuning.scoreWeights.distanceToTeamCenter = 4;
                tuning.scoreWeights.selfCanAttackNow = -6;

                tuning.currentActionParameterPercents.selfSurroundedByEnemies = 125;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 25;
                tuning.currentActionParameterPercents.enemyClusterDensity = 20;
                tuning.currentActionParameterPercents.selfCanAttackNow = 20;

                tuning.macePercent = 70;
                tuning.swordPercent = 70;
                tuning.orbPercent = 110;
                tuning.crossbowPercent = 105;
                tuning.daggerPercent = 90;
                break;

            case BattleActionType.RegroupToAllies:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = 2;
                tuning.scoreWeights.selfSurroundedByEnemies = 3;
                tuning.scoreWeights.lowHealthAllyProximity = 2;
                tuning.scoreWeights.allyUnderFocusPressure = 3;
                tuning.scoreWeights.allyFrontlineGap = 5;
                tuning.scoreWeights.isolatedEnemyVulnerability = -3;
                tuning.scoreWeights.enemyClusterDensity = -1;
                tuning.scoreWeights.distanceToTeamCenter = 7;
                tuning.scoreWeights.selfCanAttackNow = -3;

                tuning.currentActionParameterPercents.allyFrontlineGap = 115;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 50;
                tuning.currentActionParameterPercents.distanceToTeamCenter = 125;
                tuning.currentActionParameterPercents.selfCanAttackNow = 25;

                tuning.macePercent = 125;
                tuning.swordPercent = 85;
                tuning.orbPercent = 120;
                tuning.crossbowPercent = 120;
                tuning.daggerPercent = 20;
                break;

            case BattleActionType.CollapseOnCluster:
                tuning.baseBias = 1;
                tuning.scoreWeights.selfHpLow = -6;
                tuning.scoreWeights.selfSurroundedByEnemies = -6;
                tuning.scoreWeights.lowHealthAllyProximity = 0;
                tuning.scoreWeights.allyUnderFocusPressure = 1;
                tuning.scoreWeights.allyFrontlineGap = 2;
                tuning.scoreWeights.isolatedEnemyVulnerability = -2;
                tuning.scoreWeights.enemyClusterDensity = 10;
                tuning.scoreWeights.distanceToTeamCenter = 0;
                tuning.scoreWeights.selfCanAttackNow = 6;

                tuning.currentActionParameterPercents.selfSurroundedByEnemies = 85;
                tuning.currentActionParameterPercents.enemyClusterDensity = 125;
                tuning.currentActionParameterPercents.selfCanAttackNow = 50;

                tuning.macePercent = 80;
                tuning.swordPercent = 130;
                tuning.orbPercent = 85;
                tuning.crossbowPercent = 70;
                tuning.daggerPercent = 65;
                break;

            case BattleActionType.EngageNearest:
            default:
                tuning.baseBias = 3;
                tuning.scoreWeights.selfHpLow = -2;
                tuning.scoreWeights.selfSurroundedByEnemies = 2;
                tuning.scoreWeights.lowHealthAllyProximity = 1;
                tuning.scoreWeights.allyUnderFocusPressure = 1;
                tuning.scoreWeights.allyFrontlineGap = -1;
                tuning.scoreWeights.isolatedEnemyVulnerability = 3;
                tuning.scoreWeights.enemyClusterDensity = 2;
                tuning.scoreWeights.distanceToTeamCenter = -2;
                tuning.scoreWeights.selfCanAttackNow = 10;

                tuning.currentActionParameterPercents.selfCanAttackNow = 110;

                tuning.macePercent = 110;
                tuning.swordPercent = 115;
                tuning.orbPercent = 115;
                tuning.crossbowPercent = 115;
                tuning.daggerPercent = 120;
                break;
        }

        return tuning;
    }
}