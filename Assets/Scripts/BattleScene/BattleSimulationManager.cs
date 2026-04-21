using System.Collections.Generic;
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
public sealed class BattleSimulationManager : MonoBehaviour, ISkillEffectApplier
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

    private const float CommitmentEnterMultiplier = 1.2f;

    [Header("Debug")]
    public bool verboseLog = false;

    private readonly List<BattleRuntimeUnit> _runtimeUnits = new List<BattleRuntimeUnit>(12);

    // 3D 전장 클램프를 위한 BoxCollider
    private BoxCollider _battlefieldCollider;
    private BattleStatusGridUIManager _statusGridUIManager;
    private BattleSceneUIManager _battleSceneUIManager;
    private BattleStartPayload _payload;

    private BattleFieldView _fieldView;
    private Dictionary<BattleActionType, IBattleActionPlanner> _planners;
    private BattleSkillRegistry _skillRegistry;

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
        if (aiTuning != null)
            aiTuning.EnsureDefaultActionTunings();
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
    }

    // BoxCollider로 파라미터 변경
    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BoxCollider battlefieldCollider,
        BattleStatusGridUIManager statusGridUIManager = null,
        BattleSceneUIManager battleSceneUIManager = null,
        BattleStartPayload payload = null)
    {
        if (aiTuning != null)
            aiTuning.EnsureDefaultActionTunings();

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

            unit.State.SetBodyRadius(unitBodyRadius);
            unit.ClearCurrentTarget();
            unit.State.ClearAttackCooldown();
            unit.State.ClearSkillCooldown();
            unit.State.SetIdleState();

            _runtimeUnits.Add(unit);
        }

        _statusGridUIManager = statusGridUIManager;
        _battleSceneUIManager = battleSceneUIManager;
        _payload = payload;

        _fieldView = new BattleFieldView(_runtimeUnits, BuildParameterRadii(), escapeTowardTeamBlend);
        _planners = BuildPlannerRegistry();
        _skillRegistry = new BattleSkillRegistry(new IBattleSkill[]
        {
            new HeartAttackSkill(),
            new MadnessSkill(),
        });

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
                unit.State.TickAttackCooldown(tickDeltaTime);
                unit.State.TickSkillCooldown(tickDeltaTime);
                unit.State.TickBufflCooldown(tickDeltaTime);

                if (unit.IsAttacking && !unit.IsAttackAnimationPlaying())
                    unit.State.SetAttackState(false);
            }
        }
    }

    private void ComputeAllParameters()
    {
        // BattleUnitView 스냅샷 구성
        var allViews = new List<BattleUnitView>(_runtimeUnits.Count);
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit u = _runtimeUnits[i];
            if (u != null && !u.IsCombatDisabled)
                allViews.Add(BattleUnitView.From(u));
        }

        BattleParameterRadii radii = BuildParameterRadii();

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleUnitView self = BattleUnitView.From(unit);

            var allies = new List<BattleUnitView>();
            var enemies = new List<BattleUnitView>();
            for (int j = 0; j < allViews.Count; j++)
            {
                BattleUnitView v = allViews[j];
                if (v.UnitNumber == self.UnitNumber)
                    continue;
                if (v.IsEnemy == self.IsEnemy)
                    allies.Add(v);
                else
                    enemies.Add(v);
            }

            BattleParameterSet rawParameters = BattleParameterComputer.Compute(self, allies, enemies, radii);
            BattleParameterSet modifiedParameters = ApplyCurrentActionParameterModifiers(unit, rawParameters);

            rawParameters.Clamp01All();
            modifiedParameters.Clamp01All();

            unit.State.SetCurrentParameters(rawParameters, modifiedParameters);
        }
    }

    private BattleParameterRadii BuildParameterRadii()
    {
        if (aiTuning == null)
            return default;
        return new BattleParameterRadii
        {
            surroundRadius = aiTuning.surroundRadius,
            helpRadius = aiTuning.helpRadius,
            peelRadius = aiTuning.peelRadius,
            frontlineGapRadius = aiTuning.frontlineGapRadius,
            isolationRadius = aiTuning.isolationRadius,
            assassinReachRadius = aiTuning.assassinReachRadius,
            clusterRadius = aiTuning.clusterRadius,
            teamCenterDistanceRadius = aiTuning.teamCenterDistanceRadius
        };
    }

    private static Dictionary<BattleActionType, IBattleActionPlanner> BuildPlannerRegistry()
    {
        var planners = new IBattleActionPlanner[]
        {
            new AssassinatePlanner(),
            new DiveBacklinePlanner(),
            new PeelPlanner(),
            new EscapePlanner(),
            new RegroupPlanner(),
            new CollapsePlanner(),
            new EngageNearestPlanner()
        };
        var dict = new Dictionary<BattleActionType, IBattleActionPlanner>(planners.Length);
        foreach (var p in planners)
            dict[p.ActionType] = p;
        return dict;
    }

    private void EvaluateAllActionScores()
    {
        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            WeaponType weaponType = unit.Snapshot != null ? unit.Snapshot.WeaponType : WeaponType.None;
            BattleActionScoreSet scores = BattleActionScorer.Evaluate(unit.CurrentModifiedParameters, weaponType, aiTuning != null ? aiTuning.actionTunings : null);
            scores = BattleActionScorer.ApplyEscapeReengageBias(unit.CurrentActionType, unit.CurrentRawParameters, scores);
            unit.State.SetCurrentScores(scores);
        }
    }

    private void CommitOrSwitchActions(float tickDeltaTime)
    {
        float decay = aiTuning != null ? aiTuning.commitmentDecayPerSecond : 0.5f;

        for (int i = 0; i < _runtimeUnits.Count; i++)
        {
            BattleRuntimeUnit unit = _runtimeUnits[i];
            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleActionScoreSet scores = unit.CurrentScores;
            BattleActionType currentAction = unit.CurrentActionType;

            GetBestActionRespectingEscapeLimit(unit, scores, BattleActionType.None, out BattleActionType bestAction, out float bestScore);

            if (unit.IsExternallyControlled)
                continue;

            if (currentAction == BattleActionType.None)
            {
                EnterAction(unit, bestAction, bestScore);
                EnsureCurrentActionIsUsableOrFallback(unit);
                continue;
            }

            float decayedKeepBehaving = unit.KeepBehaving - (decay * tickDeltaTime);
            float nextActionTimer = unit.ActionTimer + tickDeltaTime;

            GetBestActionRespectingEscapeLimit(unit, scores, currentAction, out BattleActionType bestOtherAction, out float bestOtherScore);

            if (bestOtherScore > decayedKeepBehaving)
            {
                EnterAction(unit, bestOtherAction, bestOtherScore);
                EnsureCurrentActionIsUsableOrFallback(unit);
            }
            else
            {
                unit.State.SetCurrentActionType(currentAction, GetActionDisplayName(currentAction));
                unit.State.SetDecisionState(decayedKeepBehaving, nextActionTimer);
                EnsureCurrentActionIsUsableOrFallback(unit);
            }
        }
    }

    private void EnterAction(BattleRuntimeUnit unit, BattleActionType actionType, float chosenFinalScore)
    {
        if (unit == null)
            return;
        float keepBehaving = chosenFinalScore * CommitmentEnterMultiplier;
        unit.State.SetCurrentActionType(actionType, GetActionDisplayName(actionType));
        unit.State.SetDecisionState(keepBehaving, 0f);
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
        if (!_planners.TryGetValue(currentAction, out IBattleActionPlanner planner))
            return;

        BattleActionExecutionPlan currentPlan = planner.Build(unit, _fieldView);
        if (planner.IsUsable(unit, currentPlan, _fieldView))
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

            if (unit.IsExternallyControlled)
                continue;

            BattleActionExecutionPlan plan;
            if (!_planners.TryGetValue(unit.CurrentActionType, out IBattleActionPlanner planner))
            {
                plan = _planners[BattleActionType.EngageNearest].Build(unit, _fieldView);
            }
            else
            {
                plan = planner.Build(unit, _fieldView);
                if (!planner.IsUsable(unit, plan, _fieldView))
                {
                    IBattleActionPlanner engage = _planners[BattleActionType.EngageNearest];
                    BattleActionExecutionPlan engagePlan = engage.Build(unit, _fieldView);
                    plan = engage.IsUsable(unit, engagePlan, _fieldView) ? engagePlan : default;
                    if (plan.Action == BattleActionType.None)
                    {
                        plan.Action = unit.CurrentActionType;
                        plan.DesiredPosition = unit.Position;
                    }
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

            if (unit.IsAttacking)
                continue;

            if (unit.IsExternallyControlled)
            {
                if (unit.ExternalRotationDelta != 0f)
                    unit.Rotate(unit.ExternalRotationDelta * tickDeltaTime);

                if (unit.ExternalMoveDirection.sqrMagnitude > 0.0001f)
                {
                    unit.SetPosition(unit.Position + unit.ExternalMoveDirection * unit.MoveSpeed * tickDeltaTime);
                    unit.ClampInsideBattlefield(_battlefieldCollider);
                    unit.State.SetMovementState(true);
                }
                else
                {
                    unit.State.SetIdleState();
                }
                continue;
            }

            BattleRuntimeUnit targetEnemy = unit.PlannedTargetEnemy;

            if (_fieldView.IsValidEnemyTarget(unit, targetEnemy) && _fieldView.IsWithinEffectiveAttackDistance(unit, targetEnemy))
            {
                if (unit.IsMoving)
                    unit.State.SetIdleState();

                continue;
            }

            if (unit.HasPlannedDesiredPosition)
            {
                unit.FaceTarget(unit.PlannedDesiredPosition); //바라보도록

                bool moved = MoveTowardsPosition(unit, unit.PlannedDesiredPosition, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();
                continue;
            }

            if (_fieldView.IsValidEnemyTarget(unit, targetEnemy))
            {
                unit.FaceTarget(unit.PlannedDesiredPosition); //바라보도록

                bool moved = MoveTowardsTarget(unit, targetEnemy, tickDeltaTime);
                unit.State.SetMovementState(moved);
                if (!moved)
                    unit.State.SetIdleState();
                continue;
            }

            unit.State.SetIdleState();
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
            if (!_fieldView.IsValidEnemyTarget(attacker, target))
                continue;                    //유효하지 않은 적
            if (!_fieldView.IsWithinEffectiveAttackDistance(attacker, target))
                continue;       //사거리 안
            if (attacker.AttackCooldownRemaining > 0f)
                continue;                    //공격 쿨이 남음

            attacker.State.SetAttackState(true);        //실질적으로 때리는 타이밍

            target.State.ApplyDamage(attacker.Attack); //데미지 적용

            attacker.State.ResetAttackCooldown();       //공격 쿨 돌리고
            // SetAttackState(false)는 TickAllCooldowns에서 애니메이션 종료 후 호출
        }
    }

    private void ExecuteSkillPhase()
    {
        foreach (BattleRuntimeUnit unit in _runtimeUnits)
        {
            if (unit == null || unit.IsCombatDisabled)
                continue;
            if (unit.IsExternallyControlled) // TODO: 스킬이 추가되면 해제
                continue;
            if (unit.SkillCooldownRemaining > 0f)
                continue;

            IBattleSkill skill = _skillRegistry.Get(unit.State.GetSkill());
            if (!skill.CanActivate(unit, _fieldView))
                continue;

            skill.Apply(unit, _fieldView, this);
            unit.SetSkillState();           // 비주얼(Animator 트리거)은 BRU에 남음
            unit.State.ResetSkillCooldown();
        }
    }

    // ISkillEffectApplier 구현
    void ISkillEffectApplier.ApplyDamage(BattleUnitCombatState target, float amount)
        => target.ApplyDamage(amount);

    void ISkillEffectApplier.AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
        => target.AddKnockback(direction, force);

    void ISkillEffectApplier.ApplyHeal(BattleUnitCombatState caster, float amount)
        => caster.ApplyHeal(amount);

    void ISkillEffectApplier.ApplyBuff(BattleUnitCombatState caster, BuffType type, int level, float duration)
        => caster.BuffApply(type, level, duration);



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
                int buffNum = _runtimeUnits[i].State.BuffNum();









            }
        }




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
        float effectiveAttackDistance = _fieldView.GetEffectiveAttackDistance(mover, target);

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

    private string GetActionDisplayName(BattleActionType actionType)
    {
        BattleActionTuning tuning = GetActionTuning(actionType);
        if (tuning != null && !string.IsNullOrWhiteSpace(tuning.displayName))
            return tuning.displayName;
        return actionType.ToString();
    }

    private BattleActionTuning GetActionTuning(BattleActionType actionType)
    {
        if (aiTuning == null)
            return null;
        return aiTuning.GetActionTuning(actionType);
    }
}

