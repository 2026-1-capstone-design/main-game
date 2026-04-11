using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;


// BattleSimulationManager 책임 (실제 전투 본체):
// 1. fixed tick 기반 시뮬레이션 루프
// 2. RAW 파라미터 9개 계산 → 현재 행동 기준 MOD 파라미터 보정
// 3. 각 행동에 대해 점수 계산 → 무기 타입별 최종 배율 적용
// 4. commitment / timer / decay 기반 행동 유지/전환 판단
// 5. 행동별 execution plan 생성
// 6. 이동 처리 → separation(겹침 방지) → 공격속도 기반 공격 → HP 감소/전투불능
// 7. 전투 종료 판정 → 승리 시 pending reward 저장 → UI 결과 표시 요청
// ※ 배속: simulationSpeedMultiplier (minSimulationSpeed ~ maxSimulationSpeed 범위로 clamp)
public enum BuffType
{
    //긍정 버프
    MoveSpeed = 0,
    AttackRange = 1,
    AttackSpeed = 2,
    AttackDamage = 3,
    RedudeDamage = 4,


    //부정 버프
    BleedDamage,
    MoveReduce,
    DamageReduce,
    FearDamage
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

    [Header("AI / Action Switching")]
    // 행동 유지 관성 감소 속도. remainingCommitment = max(0, KeepBehaving - decayPerSecond * ActionTimer)
    // 다른 행동으로 전환하려면 bestOtherScore > currentEffectiveScore + actionSwitchThreshold 여야 한다.
    [SerializeField] private float commitmentDecayPerSecond = 0.5f;

    // 행동 진입 시 KeepBehaving = chosenFinalScore * 이 값. 진입 직후 관성을 부여한다.
    private const float CommitmentEnterMultiplier = 1.2f;

    [Header("AI / Parameter Radii")]
    public float surroundRadius = 350f;            // self_surrounded_by_enemies 계산 기준 반경
    public float helpRadius = 450f;                // low_health_ally_proximity 계산 기준 반경
    public float peelRadius = 500f;                // ally_under_focus_pressure 거리 가중치 기준 반경
    public float frontlineGapRadius = 600f;        // ally_frontline_gap 정규화 기준 반경
    public float isolationRadius = 450f;           // isolated_enemy_vulnerability 고립 판정 기준 반경
    public float assassinReachRadius = 600f;       // isolated_enemy_vulnerability 자기 도달 가능 거리 기준
    public float clusterRadius = 400f;             // enemy_cluster_density 군집 판정 기준 반경
    public float teamCenterDistanceRadius = 800f;  // distance_to_team_center 정규화 기준 반경

    [Header("AI / Position Helpers")]
    public float desiredPositionStopDistance = 8f;
    public float escapeTowardTeamBlend = 0.35f;

    [Header("AI / Action Tunings")]
    public List<BattleActionTuning> actionTunings = new List<BattleActionTuning>();

    [Header("Debug")]
    public bool verboseLog = false;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>(12);

    // 3D 전장 클램프를 위한 BoxCollider
    private BoxCollider _battlefieldCollider;
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

    // BoxCollider로 파라미터 변경
    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BoxCollider battlefieldCollider,
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

        _runtimeUnits.Clear();
        _battlefieldCollider = battlefieldCollider; // 저장

        for (int i = 0; i < runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = runtimeUnits[i];
            if (unit == null)
                continue;

            unit.SetBodyRadius(unitBodyRadius);
            unit.ClearCurrentTarget();
            unit.ClearAttackCooldown();
            unit.ClearSkillCooldown();  //스킬 쿨다움 초기화
            unit.SetIdleState();

            _runtimeUnits.Add(unit);
        }

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
    }

    private void Update()
    {
        if (!_initialized || _battleFinished || _isTemporarilyPaused)
            return;

        float scaledDeltaTime = Time.unscaledDeltaTime * Mathf.Max(0f, simulationSpeedMultiplier);
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
        if (_statusGridUIManager != null)
            _statusGridUIManager.Refresh();
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

    // 전투 AI 한 tick 처리 순서:
    // 1. 쿨다운 감소 → 2. RAW 파라미터 9개 계산 → 3. MOD 보정 → 4. 행동 점수 계산
    // 5. 행동 유지/전환 판단 → 6. execution plan 생성 → 7. 이동 → 8. 겹침 해소
    // 9. 공격 → 10. 스킬 → 11. 승패 판정
    private void StepSimulation(float tickDeltaTime)
    {
        TickAllCooldowns(tickDeltaTime);
        ComputeAllParameters();
        EvaluateAllActionScores();
        CommitOrSwitchActions(tickDeltaTime);
        BuildAllExecutionPlans();
        ExecuteSpecialEffect(tickDeltaTime);
        ExecuteMovementPhase(tickDeltaTime);
        ResolveUnitSeparation();
        ExecuteAttackPhase();
        ExecuteSkillPhase();            //스킬
        TryFinishBattle();

        if (_statusGridUIManager != null)
            _statusGridUIManager.Refresh();
    }

    private void TickAllCooldowns(float tickDeltaTime)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit != null && !unit.IsCombatDisabled)
            {
                unit.TickAttackCooldown(tickDeltaTime);
                unit.TickSkillCooldown(tickDeltaTime);
                unit.TickBufflCooldown(tickDeltaTime);
            }
        }
    }

    private void ComputeAllParameters()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

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
                continue;

            BattleActionScoreSet scores = EvaluateActionScores(unit, unit.CurrentModifiedParameters);
            scores = ApplyEscapeReengageBias(unit, unit.CurrentRawParameters, scores);
            unit.SetCurrentScores(scores);
        }
    }

    private void CommitOrSwitchActions(float tickDeltaTime)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleActionScoreSet scores = unit.CurrentScores;
            BattleActionType currentAction = unit.CurrentActionType;

            GetBestActionRespectingEscapeLimit(unit, scores, BattleActionType.None, out BattleActionType bestAction, out float bestScore);

            if (currentAction == BattleActionType.None)
            {
                EnterAction(unit, bestAction, bestScore);
                EnsureCurrentActionIsUsableOrFallback(unit);
                continue;
            }

            float decayedKeepBehaving = unit.KeepBehaving - (commitmentDecayPerSecond * tickDeltaTime);
            float nextActionTimer = unit.ActionTimer + tickDeltaTime;

            GetBestActionRespectingEscapeLimit(unit, scores, currentAction, out BattleActionType bestOtherAction, out float bestOtherScore);

            if (bestOtherScore > decayedKeepBehaving)
            {
                EnterAction(unit, bestOtherAction, bestOtherScore);
                EnsureCurrentActionIsUsableOrFallback(unit);
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
            return;
        float keepBehaving = chosenFinalScore * CommitmentEnterMultiplier;
        unit.SetCurrentActionType(actionType, GetActionDisplayName(actionType));
        unit.SetDecisionState(keepBehaving, 0f);
    }

    private int GetLivingUnitCountForDecision()
    {
        int count = 0;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            if (_runtimeUnits[i] != null && !_runtimeUnits[i].IsCombatDisabled)
                count++;
        }
        return count;
    }

    private int GetCurrentEscapeUnitCount()
    {
        int count = 0;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            if (_runtimeUnits[i] != null && !_runtimeUnits[i].IsCombatDisabled && _runtimeUnits[i].CurrentActionType == BattleActionType.EscapeFromPressure)
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
            return false;
        if (unit.CurrentActionType == BattleActionType.EscapeFromPressure)
            return true;
        return GetCurrentEscapeUnitCount() < GetMaxEscapeUnitCount();
    }

    private void GetBestActionRespectingEscapeLimit(BattleRuntimeUnit unit, BattleActionScoreSet scores, BattleActionType excludedAction, out BattleActionType bestAction, out float bestScore)
    {
        bool canEnterEscape = CanEnterEscapeAction(unit);
        bestAction = BattleActionType.None;
        bestScore = float.MinValue;

        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.AssassinateIsolatedEnemy, scores.AssassinateIsolatedEnemy, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.DiveEnemyBackline, scores.DiveEnemyBackline, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.PeelForWeakAlly, scores.PeelForWeakAlly, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.EscapeFromPressure, scores.EscapeFromPressure, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.RegroupToAllies, scores.RegroupToAllies, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.CollapseOnCluster, scores.CollapseOnCluster, excludedAction, canEnterEscape);
        TryConsiderActionRespectingEscapeLimit(ref bestAction, ref bestScore, BattleActionType.EngageNearest, scores.EngageNearest, excludedAction, canEnterEscape);

        if (bestAction == BattleActionType.None)
        {
            bestAction = BattleActionType.EngageNearest;
            bestScore = scores.EngageNearest;
        }
    }

    private void TryConsiderActionRespectingEscapeLimit(ref BattleActionType bestAction, ref float bestScore, BattleActionType candidateAction, float candidateScore, BattleActionType excludedAction, bool canEnterEscape)
    {
        if (candidateAction == excludedAction)
            return;
        if (candidateAction == BattleActionType.EscapeFromPressure && !canEnterEscape)
            return;

        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }

    private void EnsureCurrentActionIsUsableOrFallback(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.IsCombatDisabled)
            return;
        BattleActionType currentAction = unit.CurrentActionType;
        if (currentAction == BattleActionType.None)
            return;

        BattleActionExecutionPlan currentPlan = BuildExecutionPlan(unit, currentAction);
        if (IsExecutionPlanUsable(unit, currentPlan))
            return;
        if (currentAction == BattleActionType.EngageNearest)
            return;

        float engageScore = unit.CurrentScores.GetScore(BattleActionType.EngageNearest);
        EnterAction(unit, BattleActionType.EngageNearest, engageScore);
    }

    private void BuildAllExecutionPlans()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

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
                    plan.DesiredPosition = unit.Position; // Vector3로 변경
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
                continue;

            BattleRuntimeUnit targetEnemy = unit.PlannedTargetEnemy;

            if (IsValidEnemyTarget(unit, targetEnemy) && IsWithinEffectiveAttackDistance(unit, targetEnemy))
            {
                if (unit.IsMoving)
                    unit.SetIdleState();

                continue;
            }

            if (unit.HasPlannedDesiredPosition)
            {
                unit.FaceTarget(unit.PlannedDesiredPosition); //바라보도곩

                bool moved = MoveTowardsPosition(unit, unit.PlannedDesiredPosition, tickDeltaTime);
                unit.SetMovementState(moved);
                if (!moved)
                    unit.SetIdleState();
                continue;
            }

            if (IsValidEnemyTarget(unit, targetEnemy))
            {
                unit.FaceTarget(unit.PlannedDesiredPosition);   //바라보도록

                bool moved = MoveTowardsTarget(unit, targetEnemy, tickDeltaTime);
                unit.SetMovementState(moved);
                if (!moved)
                    unit.SetIdleState();
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
                continue;            //전투 불가능한 상태

            BattleRuntimeUnit target = attacker.PlannedTargetEnemy;
            if (!IsValidEnemyTarget(attacker, target))
                continue;                    //유효하지 않은 적
            if (!IsWithinEffectiveAttackDistance(attacker, target))
                continue;       //사거리 안
            if (attacker.AttackCooldownRemaining > 0f)
                continue;                    //공격 쿨이 남음

            attacker.SetAttackState(true);              //실질적으로 때리는 타이밍

            target.ApplyDamage(attacker.Attack);        //데미지 적용

            attacker.ResetAttackCooldown();             //공격 쿨 돌리고

            attacker.SetAttackState(false);             //때리는 것 끝
        }
    }

    //임시 스킬 쿨이 되면 스킬 사용
    private void ExecuteSkillPhase()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit Caster = _runtimeUnits[i];
            if (Caster == null || Caster.IsCombatDisabled)
                continue;

            if (Caster.SkillCooldownRemaining > 0f)
                continue;

            switch (Caster.getSkillType())
            {
                case skillType.attack:
                    BattleRuntimeUnit target = Caster.PlannedTargetEnemy;

                    if (!IsValidEnemyTarget(Caster, target))
                        continue;
                    if (!IsWithinEffectiveAttackDistance(Caster, target))
                        continue;

                    UseSkill(Caster, target);

                    break;
                case skillType.tank:
                    UseSkill(Caster, null);

                    break;
                case skillType.support:
                    FindNearestLivingAlly(Caster);
                    UseSkill(Caster, null);
                    break;
                case skillType.enhance:
                    UseSkill(Caster, null);
                    break;
                default:
                case skillType.None:
                    UseSkill(Caster, null);
                    break;
            }
        }
    }


    private void UseSkill(BattleRuntimeUnit Caster, BattleRuntimeUnit target)
    {
        WeaponSkillId Use = Caster.getSkill();

        switch (Use)
        {
            case WeaponSkillId.HeartAttack:
                Vector3 pushDirection = target.Position - Caster.Position;
                target.ApplyDamage(20f);
                target.AddKnockback(pushDirection, 50f);
                break;
            case WeaponSkillId.Madness:
                Caster.BuffApply(BuffType.AttackSpeed, 2, 20);
                break;

            default:
            case WeaponSkillId.None:
                Caster.ApplyHeal(10);
                break;

        }

        Caster.SetSkillState();
        Caster.ResetSkillCooldown();
    }



    //Unit당 특수 상태 이상 처리,
    //개인 상태 이상은 tick당 내부에서 처리하는 것이 좋을 것 같은데...
    private void ExecuteSpecialEffect(float tickDeltaTime)
    {
        //넉백
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            if (_runtimeUnits[i] != null && !_runtimeUnits[i].IsCombatDisabled)
            {
                _runtimeUnits[i].TickKnockback(tickDeltaTime);
            }
        }



        //버프 or 디버프
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            if (_runtimeUnits[i] != null && !_runtimeUnits[i].IsCombatDisabled)
            {
                int buffNum = _runtimeUnits[i].BuffNum();









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
            return rawParameters;
        BattleActionTuning tuning = GetActionTuning(unit.CurrentActionType);
        if (tuning == null || unit.CurrentActionType == BattleActionType.None)
            return rawParameters;

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
                continue;

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
            return 100;
        if (unit == null || unit.Snapshot == null)
            return 100;
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
            DesiredPosition = target != null ? target.Position : unit.Position,
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
            DesiredPosition = target != null ? target.Position : unit.Position,
            HasDesiredPosition = target != null
        };
    }

    private BattleActionExecutionPlan BuildPeelPlan(BattleRuntimeUnit unit)
    {
        BattleRuntimeUnit ally = FindMostPressuredAlly(unit);
        BattleRuntimeUnit enemy = FindBestPeelEnemy(unit, ally);

        Vector3 desiredPosition = ally != null ? ally.Position : unit.Position;
        bool hasDesiredPosition = ally != null;

        if (enemy != null)
        {
            desiredPosition = enemy.Position;
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
        Vector3 selfPos = unit.Position;
        Vector3 pressureCenter = ComputeEnemyPressureCenter(unit);
        Vector3 away = selfPos - pressureCenter;
        away.y = 0f; // 평면 강제

        if (away.sqrMagnitude < 0.0001f)
        {
            away = unit.IsEnemy ? Vector3.right : Vector3.left;
        }
        away.Normalize();

        Vector3 teamCenter = ComputeTeamCenter(unit.IsEnemy);
        Vector3 towardTeam = (teamCenter - selfPos);
        towardTeam.y = 0f;
        towardTeam.Normalize();

        Vector3 escapeDirection = (away * (1f - escapeTowardTeamBlend) + towardTeam * escapeTowardTeamBlend).normalized;
        Vector3 desiredPosition = selfPos + escapeDirection * Mathf.Max(80f, unit.MoveSpeed);

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
        Vector3 teamCenter = ComputeTeamCenter(unit.IsEnemy);
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
        Vector3 enemyClusterCenter = ComputeTeamCenter(!unit.IsEnemy);
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
            DesiredPosition = target != null ? target.Position : unit.Position,
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
            return 0f;
        return Mathf.Clamp01(1f - (self.CurrentHealth / self.MaxHealth));
    }

    private float ComputeSelfSurroundedByEnemies(BattleRuntimeUnit self)
    {
        float sum = 0f;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            float distance = Vector3.Distance(self.Position, enemy.Position);
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
                continue;

            float hpLow = ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector3.Distance(self.Position, ally.Position), helpRadius);
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
                continue;

            int focusCount = CountEnemiesTargeting(self, ally);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpFactor = 0.5f + 0.5f * ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector3.Distance(self.Position, ally.Position), peelRadius);

            float value = focusRatio * hpFactor * distanceWeight;
            if (value > best)
                best = value;
        }
        return Mathf.Clamp01(best);
    }

    private float ComputeAllyFrontlineGap(BattleRuntimeUnit self)
    {
        List<BattleRuntimeUnit> allies = GetLivingUnits(self.IsEnemy);
        if (allies.Count <= 1)
            return 0f;

        float sumNearest = 0f;
        int count = 0;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            float nearest = float.MaxValue;

            for (int j = 0; j < allies.Count; j++)
            {
                if (i == j)
                    continue;
                float distance = Vector3.Distance(ally.Position, allies[j].Position);
                if (distance < nearest)
                    nearest = distance;
            }

            if (nearest < float.MaxValue)
            {
                sumNearest += nearest;
                count++;
            }
        }
        if (count == 0)
            return 0f;
        return Mathf.Clamp01((sumNearest / count) / frontlineGapRadius);
    }

    private float ComputeIsolatedEnemyVulnerability(BattleRuntimeUnit self)
    {
        float best = 0f;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            float score = ComputeIsolatedEnemyTargetScore(self, enemy);
            if (score > best)
                best = score;
        }
        return Mathf.Clamp01(best);
    }

    private float ComputeEnemyClusterDensity(BattleRuntimeUnit self)
    {
        List<BattleRuntimeUnit> enemies = GetLivingUnits(!self.IsEnemy);
        if (enemies.Count <= 1)
            return 0f;

        float sum = 0f;
        int pairCount = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            for (int j = i + 1; j < enemies.Count; j++)
            {
                float distance = Vector3.Distance(enemies[i].Position, enemies[j].Position);
                sum += LinearFalloff(distance, clusterRadius);
                pairCount++;
            }
        }
        if (pairCount == 0)
            return 0f;
        return Mathf.Clamp01(sum / pairCount);
    }

    private float ComputeDistanceToTeamCenter(BattleRuntimeUnit self)
    {
        Vector3 teamCenter = ComputeTeamCenter(self.IsEnemy);
        float distance = Vector3.Distance(self.Position, teamCenter);
        return Mathf.Clamp01(distance / teamCenterDistanceRadius);
    }

    private float ComputeSelfCanAttackNow(BattleRuntimeUnit self)
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;
            if (IsWithinEffectiveAttackDistance(self, enemy))
                return 1f;
        }
        return 0f;
    }

    private float ComputeIsolatedEnemyTargetScore(BattleRuntimeUnit self, BattleRuntimeUnit enemy)
    {
        if (!IsValidEnemyTarget(self, enemy))
            return 0f;

        float nearestSupportDistance = float.MaxValue;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit otherEnemy = _runtimeUnits[i];
            if (otherEnemy == null || otherEnemy == enemy || otherEnemy.IsCombatDisabled || otherEnemy.IsEnemy != enemy.IsEnemy)
                continue;

            float distance = Vector3.Distance(enemy.Position, otherEnemy.Position);
            if (distance < nearestSupportDistance)
                nearestSupportDistance = distance;
        }

        if (nearestSupportDistance == float.MaxValue)
            nearestSupportDistance = isolationRadius;

        float isolation = Mathf.Clamp01(nearestSupportDistance / isolationRadius);
        float hpLow = ComputeSelfHpLow(enemy);
        float reachFactor = 0.35f + 0.65f * LinearFalloff(Vector3.Distance(self.Position, enemy.Position), assassinReachRadius);

        return isolation * (0.6f + 0.4f * hpLow) * reachFactor;
    }

    private int CountEnemiesTargeting(BattleRuntimeUnit self, BattleRuntimeUnit ally)
    {
        int count = 0;
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(ally, enemy))
                continue;
            if (enemy.CurrentTarget == ally || enemy.PlannedTargetEnemy == ally)
                count++;
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
                continue;

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
        Vector3 enemyCenter = ComputeTeamCenter(!self.IsEnemy);
        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            float hpLow = ComputeSelfHpLow(enemy);
            float isolation = ComputeIsolatedEnemyTargetScore(self, enemy);
            float backlineFactor = Mathf.Clamp01(Vector3.Distance(enemy.Position, enemyCenter) / teamCenterDistanceRadius);

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
                continue;

            int focusCount = CountEnemiesTargeting(self, ally);
            float focusRatio = Mathf.Clamp01(focusCount / 3f);
            float hpFactor = 0.5f + 0.5f * ComputeSelfHpLow(ally);
            float distanceWeight = LinearFalloff(Vector3.Distance(self.Position, ally.Position), peelRadius);

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
            return FindNearestLivingEnemy(self);

        BattleRuntimeUnit best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            bool attackingProtectedAlly = enemy.CurrentTarget == protectedAlly || enemy.PlannedTargetEnemy == protectedAlly;
            if (!attackingProtectedAlly)
                continue;

            float distanceToAlly = Vector3.Distance(enemy.Position, protectedAlly.Position);
            float score = 1f - Mathf.Clamp01(distanceToAlly / peelRadius);

            if (score > bestScore)
            {
                bestScore = score;
                best = enemy;
            }
        }

        if (best != null)
            return best;
        return FindNearestLivingEnemy(self);
    }

    private BattleRuntimeUnit FindEnemyClosestToPoint(BattleRuntimeUnit self, Vector3 point)
    {
        BattleRuntimeUnit best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            float distance = Vector3.Distance(enemy.Position, point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = enemy;
            }
        }
        return best;
    }

    private Vector3 ComputeTeamCenter(bool isEnemyTeam)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled || unit.IsEnemy != isEnemyTeam)
                continue;

            sum += unit.Position;
            count++;
        }
        return count > 0 ? sum / count : Vector3.zero;
    }

    private Vector3 ComputeEnemyPressureCenter(BattleRuntimeUnit self)
    {
        Vector3 weightedSum = Vector3.zero;
        float weightSum = 0f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit enemy = _runtimeUnits[i];
            if (!IsValidEnemyTarget(self, enemy))
                continue;

            float distance = Vector3.Distance(self.Position, enemy.Position);
            float weight = QuadraticCloseFalloff(distance, surroundRadius);
            weightedSum += enemy.Position * weight;
            weightSum += weight;
        }

        if (weightSum <= 0.0001f)
            return ComputeTeamCenter(!self.IsEnemy);
        return weightedSum / weightSum;
    }

    private bool MoveTowardsTarget(BattleRuntimeUnit mover, BattleRuntimeUnit target, float tickDeltaTime)
    {
        if (mover == null || target == null)
            return false;

        Vector3 currentPosition = mover.Position;
        Vector3 targetPosition = target.Position;
        Vector3 toTarget = targetPosition - currentPosition;

        // 3D 평면 연산 보장
        toTarget.y = 0f;

        float centerDistance = toTarget.magnitude;
        float effectiveAttackDistance = GetEffectiveAttackDistance(mover, target);

        if (centerDistance <= effectiveAttackDistance)
            return false;

        Vector3 direction = centerDistance > 0.0001f ? toTarget / centerDistance : Vector3.zero;
        float remainingDistanceUntilAttack = Mathf.Max(0f, centerDistance - effectiveAttackDistance);
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, remainingDistanceUntilAttack);

        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private bool MoveTowardsPosition(BattleRuntimeUnit mover, Vector3 desiredPosition, float tickDeltaTime)
    {
        if (mover == null)
            return false;

        Vector3 currentPosition = mover.Position;
        Vector3 toTarget = desiredPosition - currentPosition;
        toTarget.y = 0f; // 평면 강제

        float distance = toTarget.magnitude;

        if (distance <= desiredPositionStopDistance)
            return false;

        Vector3 direction = distance > 0.0001f ? toTarget / distance : Vector3.zero;
        float moveDistance = Mathf.Min(mover.MoveSpeed * tickDeltaTime, distance);

        if (moveDistance <= 0.0001f)
            return false;

        mover.SetPosition(currentPosition + direction * moveDistance);
        mover.ClampInsideBattlefield(_battlefieldCollider);
        return true;
    }

    private BattleActionScoreSet ApplyEscapeReengageBias(BattleRuntimeUnit unit, BattleParameterSet rawParameters, BattleActionScoreSet scores)
    {
        if (unit == null || unit.CurrentActionType != BattleActionType.EscapeFromPressure)
            return scores;

        bool noEnemyInAttackRange = rawParameters.SelfCanAttackNow <= 0f;
        bool pressureMostlyGone = rawParameters.SelfSurroundedByEnemies <= 0.20f;

        if (!noEnemyInAttackRange || !pressureMostlyGone)
            return scores;

        scores.EscapeFromPressure *= 0.10f;
        scores.AssassinateIsolatedEnemy *= 1.50f;
        scores.DiveEnemyBackline *= 1.35f;
        scores.CollapseOnCluster *= 1.30f;

        return scores;
    }

    // 밀어내기 처리도 3D 평면을 사용합니다.
    private void ResolveUnitSeparation()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit a = _runtimeUnits[i];
            if (a == null || a.IsCombatDisabled)
                continue;

            for (int j = i + 1; j < _runtimeUnits.Count; j++)
            {
                BattleRuntimeUnit b = _runtimeUnits[j];
                if (b == null || b.IsCombatDisabled)
                    continue;

                Vector3 delta = a.Position - b.Position;
                delta.y = 0f; // y축을 무시하여 오직 평면상에서만 밀어내기

                float distance = delta.magnitude;
                float minDistance = a.BodyRadius + b.BodyRadius;

                if (distance >= minDistance)
                    continue;

                Vector3 pushDirection;
                if (distance > 0.0001f)
                {
                    pushDirection = delta / distance;
                }
                else
                {
                    pushDirection = (a.UnitNumber <= b.UnitNumber) ? Vector3.left : Vector3.right;
                    distance = 0f;
                }

                float overlap = minDistance - distance;
                Vector3 push = pushDirection * (overlap * 0.5f);

                a.SetPosition(a.Position + push);
                b.SetPosition(b.Position - push);

                a.ClampInsideBattlefield(_battlefieldCollider);
                b.ClampInsideBattlefield(_battlefieldCollider);
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
                continue;

            if (unit.IsEnemy)
                hasLivingEnemy = true;
            else
                hasLivingAlly = true;

            if (hasLivingAlly && hasLivingEnemy)
                return;
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
                continue;
            unit.SetIdleState();
        }

        if (_statusGridUIManager != null)
            _statusGridUIManager.Refresh();
        if (_battleSceneUIManager != null)
            _battleSceneUIManager.ShowBattleEndPanel(resolution);
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
            return null;

        BattleRuntimeUnit nearest = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit candidate = _runtimeUnits[i];
            if (!IsValidEnemyTarget(requester, candidate))
                continue;

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                nearest = candidate;
            }
        }
        return nearest;
    }

    private BattleRuntimeUnit FindNearestLivingAlly(BattleRuntimeUnit requester)
    {
        if (requester == null)
            return null;

        BattleRuntimeUnit nearest = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit candidate = _runtimeUnits[i];
            if (IsValidEnemyTarget(requester, candidate))
                continue;     //반대로 적이면 스킵

            Vector3 delta = candidate.Position - requester.Position;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;

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
                continue;
            result.Add(unit);
        }
        return result;
    }

    private bool IsValidEnemyTarget(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy == candidate.IsEnemy)
            return false;
        if (candidate.IsCombatDisabled)
            return false;
        return true;
    }

    private bool IsValidSameTeamAlly(BattleRuntimeUnit requester, BattleRuntimeUnit candidate)
    {
        if (requester == null || candidate == null)
            return false;
        if (requester == candidate)
            return false;
        if (requester.IsEnemy != candidate.IsEnemy)
            return false;
        if (candidate.IsCombatDisabled)
            return false;
        return true;
    }

    private bool IsWithinEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
            return false;

        Vector3 delta = attacker.Position - target.Position;
        delta.y = 0f;
        float distance = delta.magnitude;

        return distance <= (GetEffectiveAttackDistance(attacker, target) + 0.05f); //오차보정 추가
    }

    private float GetEffectiveAttackDistance(BattleRuntimeUnit attacker, BattleRuntimeUnit target)
    {
        if (attacker == null || target == null)
            return 0f;
        return attacker.BodyRadius + target.BodyRadius + attacker.AttackRange;
    }

    private float LinearFalloff(float distance, float radius)
    {
        if (radius <= 0f)
            return 0f;
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
            return tuning.displayName;
        return actionType.ToString();
    }

    private BattleActionTuning GetActionTuning(BattleActionType actionType)
    {
        for (int i = 0; i < actionTunings.Count; i++)
        {
            BattleActionTuning tuning = actionTunings[i];
            if (tuning != null && tuning.actionType == actionType)
                return tuning;
        }
        return null;
    }

    private void EnsureDefaultActionTunings()
    {
        if (actionTunings == null)
            actionTunings = new List<BattleActionTuning>();
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
            return;
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

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 110;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 180;
                tuning.handgunPercent = 60;
                tuning.dualgunPercent = 60;
                tuning.riflePercent = 60;
                tuning.staffPercent = 60;
                tuning.bowPercent = 60;

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

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 110;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 180;
                tuning.handgunPercent = 60;
                tuning.dualgunPercent = 60;
                tuning.riflePercent = 60;
                tuning.staffPercent = 60;
                tuning.bowPercent = 60;

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

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 70;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

                break;
            case BattleActionType.EscapeFromPressure:
                tuning.baseBias = 0;
                tuning.scoreWeights.selfHpLow = 6;
                tuning.scoreWeights.selfSurroundedByEnemies = 6;
                tuning.scoreWeights.lowHealthAllyProximity = -3;
                tuning.scoreWeights.allyUnderFocusPressure = -2;
                tuning.scoreWeights.allyFrontlineGap = 2;
                tuning.scoreWeights.isolatedEnemyVulnerability = -4;
                tuning.scoreWeights.enemyClusterDensity = 3;
                tuning.scoreWeights.distanceToTeamCenter = 3;
                tuning.scoreWeights.selfCanAttackNow = -6;
                tuning.currentActionParameterPercents.selfSurroundedByEnemies = 125;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 25;
                tuning.currentActionParameterPercents.enemyClusterDensity = 20;
                tuning.currentActionParameterPercents.selfCanAttackNow = 20;

                tuning.oneHandPercent = 70;
                tuning.twoHandPercent = 70;
                tuning.dualHandPercent = 70;
                tuning.spearPercent = 70;
                tuning.shieldPercent = 70;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

                break;
            case BattleActionType.RegroupToAllies:
                tuning.baseBias = -7;
                tuning.scoreWeights.selfHpLow = 1;
                tuning.scoreWeights.selfSurroundedByEnemies = 2;
                tuning.scoreWeights.lowHealthAllyProximity = 1;
                tuning.scoreWeights.allyUnderFocusPressure = 2;
                tuning.scoreWeights.allyFrontlineGap = 3;
                tuning.scoreWeights.isolatedEnemyVulnerability = -3;
                tuning.scoreWeights.enemyClusterDensity = -1;
                tuning.scoreWeights.distanceToTeamCenter = 3;
                tuning.scoreWeights.selfCanAttackNow = -3;
                tuning.currentActionParameterPercents.allyFrontlineGap = 115;
                tuning.currentActionParameterPercents.isolatedEnemyVulnerability = 50;
                tuning.currentActionParameterPercents.distanceToTeamCenter = 125;
                tuning.currentActionParameterPercents.selfCanAttackNow = 25;

                tuning.oneHandPercent = 100;
                tuning.twoHandPercent = 100;
                tuning.dualHandPercent = 100;
                tuning.spearPercent = 100;
                tuning.shieldPercent = 120;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 150;
                tuning.dualgunPercent = 150;
                tuning.riflePercent = 150;
                tuning.staffPercent = 150;
                tuning.bowPercent = 150;

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

                tuning.oneHandPercent = 120;
                tuning.twoHandPercent = 120;
                tuning.dualHandPercent = 120;
                tuning.spearPercent = 120;
                tuning.shieldPercent = 100;
                tuning.daggerPercent = 30;
                tuning.handgunPercent = 50;
                tuning.dualgunPercent = 50;
                tuning.riflePercent = 50;
                tuning.staffPercent = 50;
                tuning.bowPercent = 50;

                break;
            case BattleActionType.EngageNearest:
            default:
                tuning.baseBias = 100;
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

                tuning.oneHandPercent = 100;
                tuning.twoHandPercent = 100;
                tuning.dualHandPercent = 100;
                tuning.spearPercent = 100;
                tuning.shieldPercent = 100;
                tuning.daggerPercent = 100;
                tuning.handgunPercent = 100;
                tuning.dualgunPercent = 100;
                tuning.riflePercent = 100;
                tuning.staffPercent = 100;
                tuning.bowPercent = 100;

                break;
        }
        return tuning;
    }
}
