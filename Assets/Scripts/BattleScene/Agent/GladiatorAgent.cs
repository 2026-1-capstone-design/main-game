using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

// BehaviorParameters Inspector 설정:
//   Vector Observation Space Size = GladiatorObservationSchema.TotalSize (= 167)
//   Stacked Vectors                = 1
//   Discrete Branches              = 4
//     Branch 0 Size = GladiatorActionSchema.IntentBranchSize       (= 5)
//     Branch 1 Size = GladiatorActionSchema.MoveBranchSize         (= 6)
//     Branch 2 Size = GladiatorActionSchema.TargetBranchSize       (= 6)
//     Branch 3 Size = GladiatorActionSchema.RotationBranchSize     (= 3)
//
// 모드:
//   - Default / InferenceOnly : ML-Agents 정책이 결정. SetControlMode(ExternalAgent).
//   - HeuristicOnly            : Heuristic()가 룰베이스 결정을 ML-Agents action으로 inverse map.
//                                BC demonstration 녹화 시 사용. SetControlMode는 BuiltInAI 유지.
//                                (룰베이스가 직접 unit 행동, Heuristic은 action 기록용)
//
// Reward 설계는 GladiatorRewardConfig.cs 헤더 주석 참고.
public class GladiatorAgent : Agent
{
    private const float RotationSpeedDegPerSec = 240f;
    private const float HardBoundaryRadiusMultiplier = 1.25f;
    private const string TeamSizeEnvironmentParameter = "team_size";
    private const float MaxTeamSize = 6f;
    private const float RangedAttackRangeThreshold = 3f;

    [SerializeField]
    private GladiatorRewardConfig rewardConfig;

    private BattleRuntimeUnit _selfUnit;
    private BattleSceneFlowManager _flowManager;
    private TrainingBootstrapper _trainingBootstrapper;
    private Vector3 _arenaCenter;
    private float _arenaExtentsMin;
    private GladiatorRosterView _rosterView;
    private float _prevDistToNearestEnemy;
    private bool _boundaryResetRequested;
    private bool _isHeuristicRecordingMode;

    // 직전 step의 action (POMDP 대응 + 일관성 보상 입력).
    private int _lastIntent = GladiatorActionSchema.IntentHold;
    private int _lastMove = GladiatorActionSchema.MoveNone;
    private int _lastRotate = GladiatorActionSchema.RotateNone;

    public bool HasControlledUnit => _selfUnit != null;

    public void Initialize(
        BattleRuntimeUnit unit,
        BattleSceneFlowManager flowManager,
        TrainingBootstrapper trainingBootstrapper
    )
    {
        if (rewardConfig == null)
        {
            Debug.LogError("[GladiatorAgent] Reward config is required.", this);
            enabled = false;
            return;
        }

        CleanupSubscriptions();

        _selfUnit = unit;
        _flowManager = flowManager;
        _trainingBootstrapper = trainingBootstrapper;
        _boundaryResetRequested = false;

        // BehaviorType이 HeuristicOnly이면 BC 녹화 모드. 룰베이스가 unit 행동을 직접 결정한다.
        BehaviorParameters bp = GetComponent<BehaviorParameters>();
        _isHeuristicRecordingMode = bp != null && bp.BehaviorType == BehaviorType.HeuristicOnly;

        SphereCollider col = flowManager?.battlefieldCollider;
        _arenaCenter = col != null ? col.bounds.center : Vector3.zero;
        _arenaExtentsMin = col != null ? Mathf.Min(col.bounds.extents.x, col.bounds.extents.z) : float.MaxValue;
        _rosterView = CreateRosterView();

        _lastIntent = GladiatorActionSchema.IntentHold;
        _lastMove = GladiatorActionSchema.MoveNone;
        _lastRotate = GladiatorActionSchema.RotateNone;

        if (_selfUnit != null)
        {
            // 녹화 모드: BuiltInAI 유지 → 룰베이스가 PlannedDesiredPosition/PlannedTargetEnemy를 결정.
            // 정상 모드: ExternalAgent → 정책 action이 SetExternalMovement/SetExternalAttackTarget로 적용됨.
            if (!_isHeuristicRecordingMode)
            {
                _selfUnit.SetControlMode(BattleUnitControlMode.ExternalAgent);
                // 머리 위 statusText에 [ML] 명시. ExternalAgent 모드에서 BattleDecisionSystem이 skip되어
                // 갱신되지 않으므로 이 값이 그대로 유지된다. 룰베이스 unit은 매 tick EngageNearest 등으로 변동.
                _selfUnit.SetCurrentAction("[ML]");
            }

            _selfUnit.State.OnDamageTaken += HandleDamageTaken;
            _selfUnit.State.OnDied += HandleSelfDied;
            _selfUnit.OnAttackLanded += HandleAttackLanded;
        }

        _prevDistToNearestEnemy = GetDistToNearestOpponent();
    }

    private void HandleDamageTaken(float damage)
    {
        AddReward(rewardConfig.damageTaken);
        AddReward(Mathf.Max(0f, damage) * rewardConfig.damageTakenPerPoint);
    }

    private void HandleSelfDied()
    {
        AddReward(rewardConfig.death);
    }

    private void HandleAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
    {
        AddReward(rewardConfig.attackLanded);
        AddReward(actualDamage * rewardConfig.damageDealt);
        if (wasKill)
        {
            AddReward(rewardConfig.kill);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        float arenaRadius = _selfUnit != null ? _arenaExtentsMin - _selfUnit.BodyRadius : float.MaxValue;
        float teamSizeNormalized = ResolveTeamSizeNormalized();
        BattleObservationBuilder.Write(
            sensor,
            new GladiatorObservationContext(
                _selfUnit,
                _rosterView,
                _arenaCenter,
                arenaRadius,
                _lastIntent,
                _lastMove,
                _lastRotate,
                teamSizeNormalized
            )
        );
    }

    private float ResolveTeamSizeNormalized()
    {
        if (Academy.IsInitialized)
        {
            float teamSize = Academy.Instance.EnvironmentParameters.GetWithDefault(
                TeamSizeEnvironmentParameter,
                MaxTeamSize
            );
            return Mathf.Clamp01(teamSize / MaxTeamSize);
        }

        return 1f;
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled || _rosterView == null)
        {
            return;
        }

        // Stage 1~2: 스킬/방어 의도 봉인.
        actionMask.SetActionEnabled(0, GladiatorActionSchema.IntentUseSkill, false);
        actionMask.SetActionEnabled(0, GladiatorActionSchema.IntentDefend, false);

        bool hasValidTarget = HasLivingOpponent();
        if (!hasValidTarget)
        {
            actionMask.SetActionEnabled(0, GladiatorActionSchema.IntentAttack, false);
            return;
        }

        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            bool invalid = target == null || target.IsCombatDisabled;
            if (invalid)
            {
                actionMask.SetActionEnabled(2, i, false);
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            return;
        }

        int intent = actions.DiscreteActions[0];
        int moveMode = actions.DiscreteActions[1];
        int targetSlot = actions.DiscreteActions[2];
        int rotateAction = actions.DiscreteActions[3];

        // 녹화 모드는 unit 행동을 룰베이스에 위임. reward는 그래도 계산해서 demonstration의 advantage 추정에 활용.
        if (_isHeuristicRecordingMode)
        {
            CacheLastAction(intent, moveMode, rotateAction);
            return;
        }

        AddReward(rewardConfig.step);

        // ─── 회전 페널티 (현재 0으로 비활성, 자연스러움 우선) ───
        if (rotateAction != GladiatorActionSchema.RotateNone)
        {
            AddReward(rewardConfig.rotateActionCost);
            if (intent == GladiatorActionSchema.IntentMove && moveMode != GladiatorActionSchema.MoveNone)
            {
                AddReward(rewardConfig.rotateWhileMoving);
            }
        }

        // ─── 자연스러움: 이동 일관성 ───────────────────────────
        ApplyMovementConsistencyReward(intent, moveMode);

        // ─── 자연스러움: 무기 부합 행동 ─────────────────────────
        ApplyWeaponMatchedReward(intent, moveMode);

        // ─── 시각 명료성: 분산 ──────────────────────────────────
        ApplySpreadAndIsolationReward();

        // ─── 봉인 의도 위반 안전망 ──────────────────────────────
        if (intent == GladiatorActionSchema.IntentUseSkill || intent == GladiatorActionSchema.IntentDefend)
        {
            AddReward(rewardConfig.forbiddenIntent);
            intent = GladiatorActionSchema.IntentHold;
        }

        // ─── 경계 페널티 / 강제 reset ────────────────────────────
        float playableRadius = _arenaExtentsMin - _selfUnit.BodyRadius;
        Vector3 flatPos = new Vector3(_selfUnit.Position.x, 0f, _selfUnit.Position.z);
        Vector3 flatCenter = new Vector3(_arenaCenter.x, 0f, _arenaCenter.z);
        float distanceFromCenter = Vector3.Distance(flatPos, flatCenter);
        if (distanceFromCenter >= playableRadius)
        {
            AddReward(rewardConfig.boundary);
            if (distanceFromCenter >= playableRadius * HardBoundaryRadiusMultiplier)
            {
                RequestBoundaryReset();
                _selfUnit.SetExternalMovement(Vector3.zero, 0f);
                _selfUnit.SetExternalAttackTarget(null);
                CacheLastAction(GladiatorActionSchema.IntentHold, GladiatorActionSchema.MoveNone, rotateAction);
                return;
            }
        }

        // ─── 거리 변화 reward + 추격 보상 ────────────────────────
        BattleRuntimeUnit nearestOpponent = ResolveNearestOpponent();
        float nearestDist = nearestOpponent != null
            ? Vector3.Distance(_selfUnit.Position, nearestOpponent.Position)
            : float.MaxValue;
        bool shouldRetreat = ShouldRetreat(nearestDist, nearestOpponent);
        if (_prevDistToNearestEnemy < float.MaxValue && nearestDist < float.MaxValue)
        {
            float approachDelta = _prevDistToNearestEnemy - nearestDist;
            if (Mathf.Abs(approachDelta) > 0.0001f)
            {
                if (approachDelta < 0f && shouldRetreat)
                    AddReward(-approachDelta * rewardConfig.retreatDistance);
                else
                    AddReward(approachDelta * rewardConfig.approach);
            }
        }

        float rotDelta = rotateAction switch
        {
            GladiatorActionSchema.RotateLeft => -RotationSpeedDegPerSec,
            GladiatorActionSchema.RotateRight => RotationSpeedDegPerSec,
            _ => 0f,
        };

        bool attackBlocked = _selfUnit.AttackCooldownRemaining > 0f || _selfUnit.IsAttacking;
        bool isSpacingMove =
            intent == GladiatorActionSchema.IntentMove
            && (moveMode == GladiatorActionSchema.MoveBackward || moveMode == GladiatorActionSchema.MoveKeepRange);
        if (
            !attackBlocked
            && !isSpacingMove
            && intent != GladiatorActionSchema.IntentAttack
            && HasAttackableOpponent()
        )
        {
            AddReward(rewardConfig.inRangeNoAttack);
        }

        if (!attackBlocked && intent == GladiatorActionSchema.IntentHold && HasLivingOpponent())
        {
            AddReward(rewardConfig.disengaged);
        }

        _prevDistToNearestEnemy = nearestDist;

        // ─── 의도별 분기 ────────────────────────────────────────
        if (intent == GladiatorActionSchema.IntentMove)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(targetSlot);
            Vector3 movement = ResolveMovementDirection(moveMode, target);
            if (isSpacingMove)
            {
                AddReward(shouldRetreat ? rewardConfig.goodRetreat : rewardConfig.badRetreat);
            }

            // 적 방향 이동 보너스 (자동 chase 대체).
            if (
                moveMode == GladiatorActionSchema.MoveForward
                && nearestOpponent != null
                && IsOutOfAttackRange(nearestOpponent)
            )
            {
                Vector3 toEnemy = nearestOpponent.Position - _selfUnit.Position;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.0001f)
                {
                    float dot = Vector3.Dot(_selfUnit.transform.forward, toEnemy.normalized);
                    if (dot > 0.5f)
                        AddReward(rewardConfig.moveTowardTargetBonus);
                }
            }

            _selfUnit.SetExternalMovement(movement, rotDelta);
            _selfUnit.SetExternalAttackTarget(null);
            CacheLastAction(intent, moveMode, rotateAction);
            return;
        }

        if (intent == GladiatorActionSchema.IntentAttack)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(targetSlot);
            if (target == null || target.IsCombatDisabled)
            {
                AddReward(rewardConfig.invalidAction);
                CacheLastAction(GladiatorActionSchema.IntentHold, GladiatorActionSchema.MoveNone, rotateAction);
                return;
            }

            if (shouldRetreat)
            {
                AddReward(rewardConfig.dangerousAttack);
            }

            // 자동 chase 제거: IntentAttack은 멈춰서 공격 frame 발동만 담당.
            // 사거리 밖이면 정책이 IntentMove로 추격 학습해야 함.
            // 다만 사거리 밖 공격 시도는 chaseTarget만 약하게 보상해서 추격 의도 신호는 남김.
            if (IsOutOfAttackRange(target))
            {
                AddReward(rewardConfig.chaseTarget);
            }
            else
            {
                // 정면 응시 체크
                Vector3 toTarget = target.Position - _selfUnit.Position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float angle = Vector3.Angle(_selfUnit.transform.forward, toTarget);
                    if (angle > rewardConfig.facingConeDegrees)
                        AddReward(rewardConfig.notFacingTargetPenalty);
                }
            }

            _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
            _selfUnit.SetExternalAttackTarget(target);
            CacheLastAction(intent, moveMode, rotateAction);
            return;
        }

        // IntentHold (또는 봉인 fallback)
        _selfUnit.SetExternalMovement(Vector3.zero, rotDelta);
        _selfUnit.SetExternalAttackTarget(null);
        CacheLastAction(intent, moveMode, rotateAction);
    }

    private void CacheLastAction(int intent, int moveMode, int rotateAction)
    {
        _lastIntent = intent;
        _lastMove = moveMode;
        _lastRotate = rotateAction;
    }

    private void ApplyMovementConsistencyReward(int intent, int moveMode)
    {
        if (intent != GladiatorActionSchema.IntentMove)
            return;
        if (moveMode == GladiatorActionSchema.MoveNone)
            return;

        if (moveMode == _lastMove)
        {
            AddReward(rewardConfig.movementConsistency);
        }
        else if (IsOppositeMove(moveMode, _lastMove))
        {
            AddReward(rewardConfig.directionFlipPenalty);
        }
    }

    private static bool IsOppositeMove(int a, int b)
    {
        return (a == GladiatorActionSchema.MoveForward && b == GladiatorActionSchema.MoveBackward)
            || (a == GladiatorActionSchema.MoveBackward && b == GladiatorActionSchema.MoveForward)
            || (a == GladiatorActionSchema.MoveStrafeLeft && b == GladiatorActionSchema.MoveStrafeRight)
            || (a == GladiatorActionSchema.MoveStrafeRight && b == GladiatorActionSchema.MoveStrafeLeft);
    }

    private void ApplyWeaponMatchedReward(int intent, int moveMode)
    {
        if (intent != GladiatorActionSchema.IntentMove)
            return;

        bool isRanged = _selfUnit.AttackRange >= RangedAttackRangeThreshold;
        bool moveBackOrKeep =
            moveMode == GladiatorActionSchema.MoveBackward || moveMode == GladiatorActionSchema.MoveKeepRange;
        bool moveForward = moveMode == GladiatorActionSchema.MoveForward;

        if (isRanged)
        {
            if (moveBackOrKeep)
                AddReward(rewardConfig.weaponMatchedAction);
            else if (moveForward)
                AddReward(rewardConfig.weaponMismatchedAction);
        }
        else
        {
            if (moveForward)
                AddReward(rewardConfig.weaponMatchedAction);
            else if (moveBackOrKeep)
                AddReward(rewardConfig.weaponMismatchedAction);
        }
    }

    private void ApplySpreadAndIsolationReward()
    {
        if (_rosterView == null || _selfUnit == null)
            return;

        // 가까운 아군 페널티
        float radiusSqr = rewardConfig.teammateProximityRadius * rewardConfig.teammateProximityRadius;
        IReadOnlyList<BattleRuntimeUnit> teammates = _rosterView.GetSortedTeammates(_selfUnit);
        bool anyTooClose = false;
        Vector3 teamCenter = Vector3.zero;
        int aliveTeammates = 0;

        for (int i = 0; i < teammates.Count; i++)
        {
            BattleRuntimeUnit teammate = teammates[i];
            if (teammate == null || teammate.IsCombatDisabled)
                continue;

            Vector3 delta = teammate.Position - _selfUnit.Position;
            delta.y = 0f;
            if (!anyTooClose && delta.sqrMagnitude <= radiusSqr)
            {
                anyTooClose = true;
            }
            teamCenter += teammate.Position;
            aliveTeammates++;
        }

        if (anyTooClose)
        {
            AddReward(rewardConfig.teammateProximityPenalty);
        }

        // 본대 중심 고립 페널티
        if (aliveTeammates >= 2)
        {
            teamCenter /= aliveTeammates;
            Vector3 toCenter = teamCenter - _selfUnit.Position;
            toCenter.y = 0f;
            if (toCenter.magnitude > rewardConfig.isolationRadius)
            {
                AddReward(rewardConfig.isolationFromTeamPenalty);
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        _boundaryResetRequested = false;
        _prevDistToNearestEnemy = GetDistToNearestOpponent();
        _lastIntent = GladiatorActionSchema.IntentHold;
        _lastMove = GladiatorActionSchema.MoveNone;
        _lastRotate = GladiatorActionSchema.RotateNone;
    }

    public void GiveEndReward(BattleTeamId? winnerTeamId, bool isTimeout)
    {
        if (isTimeout)
        {
            AddReward(rewardConfig.timeout);
            return;
        }

        if (_selfUnit == null || !winnerTeamId.HasValue)
        {
            AddReward(rewardConfig.loss);
            return;
        }

        AddReward(winnerTeamId.Value == _selfUnit.TeamId ? rewardConfig.win : rewardConfig.loss);
    }

    // BC demonstration용 Heuristic.
    // 룰베이스 AI(BattleDecisionSystem + BattlePlanningSystem)가 이미 unit에 결정해둔 값을 읽어
    // ML-Agents Discrete action 4 branches로 inverse map.
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discrete = actionsOut.DiscreteActions;

        if (_selfUnit == null || _selfUnit.IsCombatDisabled)
        {
            discrete[0] = GladiatorActionSchema.IntentHold;
            discrete[1] = GladiatorActionSchema.MoveNone;
            discrete[2] = 0;
            discrete[3] = GladiatorActionSchema.RotateNone;
            return;
        }

        // Branch 0: Intent
        discrete[0] = MapActionTypeToIntent(_selfUnit.CurrentActionType, _selfUnit.PlannedTargetEnemy);

        // Branch 2: Target slot (PlannedTargetEnemy → roster slot index)
        discrete[2] = ResolveTargetSlotForState(_selfUnit.PlannedTargetEnemy);

        // Branch 1: Move (PlannedDesiredPosition - Position → 6방향 분류)
        discrete[1] = MapDesiredPositionToMove(_selfUnit, _selfUnit.PlannedTargetEnemy);

        // Branch 3: Rotate (PlannedDesiredPosition 또는 target 방향 vs forward → left/right/none)
        discrete[3] = MapRotation(_selfUnit, _selfUnit.PlannedTargetEnemy);
    }

    private static int MapActionTypeToIntent(BattleActionType actionType, BattleUnitCombatState plannedTargetEnemy)
    {
        switch (actionType)
        {
            case BattleActionType.AssassinateIsolatedEnemy:
            case BattleActionType.DiveEnemyBackline:
            case BattleActionType.PeelForWeakAlly:
            case BattleActionType.CollapseOnCluster:
            case BattleActionType.EngageNearest:
                // 적 타겟이 있으면 Attack 의도. 없으면 Move (접근 중).
                return plannedTargetEnemy != null
                    ? GladiatorActionSchema.IntentAttack
                    : GladiatorActionSchema.IntentMove;

            case BattleActionType.EscapeFromPressure:
            case BattleActionType.RegroupToAllies:
                return GladiatorActionSchema.IntentMove;

            case BattleActionType.None:
            default:
                return GladiatorActionSchema.IntentHold;
        }
    }

    private int ResolveTargetSlotForState(BattleUnitCombatState plannedEnemyState)
    {
        if (plannedEnemyState == null || _rosterView == null)
            return 0;

        IReadOnlyList<BattleRuntimeUnit> opponents = _rosterView.GetSortedHostiles(_selfUnit);
        for (int i = 0; i < opponents.Count && i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit candidate = opponents[i];
            if (candidate != null && candidate.State == plannedEnemyState)
                return i;
        }
        return 0;
    }

    private int MapDesiredPositionToMove(BattleRuntimeUnit self, BattleUnitCombatState plannedEnemyState)
    {
        // 룰베이스가 이동 의도가 없으면 None.
        if (!self.HasPlannedDesiredPosition)
            return GladiatorActionSchema.MoveNone;

        Vector3 desired = self.PlannedDesiredPosition;
        Vector3 delta = desired - self.Position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return GladiatorActionSchema.MoveNone;

        // 룰베이스가 적에게 너무 가까이 가서 KeepRange가 더 적합한 경우 처리.
        // 적 타겟이 있고 이미 사거리 안이면 KeepRange (제자리 또는 미세 조정).
        if (plannedEnemyState != null)
        {
            float effectiveRange = self.BodyRadius + plannedEnemyState.BodyRadius + self.AttackRange + 0.05f;
            float distToEnemy = Vector3.Distance(self.Position, plannedEnemyState.Position);
            if (distToEnemy <= effectiveRange * 0.95f && self.AttackRange >= RangedAttackRangeThreshold)
                return GladiatorActionSchema.MoveKeepRange;
        }

        // self-local frame으로 변환해서 forward/backward/strafeLeft/strafeRight 분류.
        Vector3 dirNormalized = delta / distance;
        float forwardDot = Vector3.Dot(dirNormalized, self.transform.forward);
        float rightDot = Vector3.Dot(dirNormalized, self.transform.right);

        if (Mathf.Abs(forwardDot) >= Mathf.Abs(rightDot))
        {
            return forwardDot >= 0f ? GladiatorActionSchema.MoveForward : GladiatorActionSchema.MoveBackward;
        }
        else
        {
            return rightDot >= 0f ? GladiatorActionSchema.MoveStrafeRight : GladiatorActionSchema.MoveStrafeLeft;
        }
    }

    private int MapRotation(BattleRuntimeUnit self, BattleUnitCombatState plannedEnemyState)
    {
        // 룰베이스 unit은 BattleRuntimeUnit.FaceTarget을 통해 자동 회전. 따라서 별도 회전 명령 불필요.
        // 적이 정면 ±20° 안이면 RotateNone, 좌측이면 Left, 우측이면 Right.
        Vector3 facing;
        if (plannedEnemyState != null)
        {
            facing = plannedEnemyState.Position - self.Position;
        }
        else if (self.HasPlannedDesiredPosition)
        {
            facing = self.PlannedDesiredPosition - self.Position;
        }
        else
        {
            return GladiatorActionSchema.RotateNone;
        }

        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
            return GladiatorActionSchema.RotateNone;

        Vector3 normalized = facing.normalized;
        float forwardDot = Vector3.Dot(self.transform.forward, normalized);
        if (forwardDot >= 0.94f) // ~20° 안
            return GladiatorActionSchema.RotateNone;

        float rightDot = Vector3.Dot(self.transform.right, normalized);
        return rightDot >= 0f ? GladiatorActionSchema.RotateRight : GladiatorActionSchema.RotateLeft;
    }

    private bool IsOutOfAttackRange(BattleRuntimeUnit target)
    {
        if (target == null || _selfUnit == null)
            return true;

        Vector3 delta = target.Position - _selfUnit.Position;
        delta.y = 0f;
        float effectiveRange = _selfUnit.BodyRadius + target.BodyRadius + _selfUnit.AttackRange + 0.05f;
        return delta.magnitude > effectiveRange;
    }

    private float GetDistToNearestOpponent() =>
        _rosterView != null ? _rosterView.GetDistanceToNearestHostile(_selfUnit) : float.MaxValue;

    private BattleRuntimeUnit ResolveOpponentSlot(int slotIndex) =>
        _rosterView != null ? _rosterView.ResolveHostileSlot(_selfUnit, slotIndex) : null;

    private bool HasAttackableOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target != null && !target.IsCombatDisabled && !IsOutOfAttackRange(target))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasLivingOpponent()
    {
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target != null && !target.IsCombatDisabled)
                return true;
        }
        return false;
    }

    private Vector3 ResolveMovementDirection(int moveMode, BattleRuntimeUnit target)
    {
        return moveMode switch
        {
            GladiatorActionSchema.MoveForward => _selfUnit.transform.forward,
            GladiatorActionSchema.MoveBackward => -_selfUnit.transform.forward,
            GladiatorActionSchema.MoveStrafeLeft => -_selfUnit.transform.right,
            GladiatorActionSchema.MoveStrafeRight => _selfUnit.transform.right,
            GladiatorActionSchema.MoveKeepRange => ResolveKeepRangeDirection(target),
            _ => Vector3.zero,
        };
    }

    private Vector3 ResolveKeepRangeDirection(BattleRuntimeUnit target)
    {
        BattleRuntimeUnit resolvedTarget =
            target != null && !target.IsCombatDisabled ? target : ResolveNearestOpponent();
        if (resolvedTarget == null)
            return Vector3.zero;

        Vector3 toTarget = resolvedTarget.Position - _selfUnit.Position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.0001f)
            return -_selfUnit.transform.forward;

        float effectiveRange = _selfUnit.BodyRadius + resolvedTarget.BodyRadius + _selfUnit.AttackRange;
        float minRange = effectiveRange * 0.65f;
        float maxRange = effectiveRange * 0.95f;
        if (distance < minRange)
            return -toTarget.normalized;
        if (distance > maxRange)
            return toTarget.normalized;

        return Vector3.zero;
    }

    private BattleRuntimeUnit ResolveNearestOpponent()
    {
        BattleRuntimeUnit nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleRuntimeUnit target = ResolveOpponentSlot(i);
            if (target == null || target.IsCombatDisabled)
                continue;

            Vector3 delta = target.Position - _selfUnit.Position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = target;
            }
        }
        return nearest;
    }

    private bool ShouldRetreat(float nearestDist, BattleRuntimeUnit nearestOpponent)
    {
        if (_selfUnit == null || nearestOpponent == null || nearestDist >= float.MaxValue)
            return false;

        float healthRatio = _selfUnit.MaxHealth > 0f ? _selfUnit.CurrentHealth / _selfUnit.MaxHealth : 1f;
        float selfEffectiveRange = _selfUnit.BodyRadius + nearestOpponent.BodyRadius + _selfUnit.AttackRange + 0.05f;
        float opponentEffectiveRange =
            nearestOpponent.BodyRadius + _selfUnit.BodyRadius + nearestOpponent.AttackRange + 0.05f;
        bool insideEnemyRange = nearestDist <= opponentEffectiveRange;
        bool closeToEnemy = nearestDist <= Mathf.Max(selfEffectiveRange, opponentEffectiveRange) * 1.15f;
        bool lowHealthAndThreatened = healthRatio <= 0.45f && (insideEnemyRange || closeToEnemy);
        bool criticalHealthNearEnemy = healthRatio <= 0.3f && closeToEnemy;
        bool cooldownSpacing = _selfUnit.AttackCooldownRemaining > 0f && insideEnemyRange;
        bool rangedTooClose = _selfUnit.AttackRange >= RangedAttackRangeThreshold && nearestDist <= selfEffectiveRange * 0.45f;

        return lowHealthAndThreatened || criticalHealthNearEnemy || cooldownSpacing || rangedTooClose;
    }

    private void OnDestroy()
    {
        CleanupSubscriptions();
    }

    private void CleanupSubscriptions()
    {
        if (_selfUnit == null)
            return;

        if (_selfUnit.State != null)
        {
            _selfUnit.State.OnDamageTaken -= HandleDamageTaken;
            _selfUnit.State.OnDied -= HandleSelfDied;
        }
        _selfUnit.OnAttackLanded -= HandleAttackLanded;
    }

    private void RequestBoundaryReset()
    {
        if (_boundaryResetRequested)
            return;

        _boundaryResetRequested = true;
        _trainingBootstrapper?.RequestEpisodeReset();
    }

    private GladiatorRosterView CreateRosterView()
    {
        BattleStartPayload payload = _flowManager != null ? _flowManager.CurrentPayload : null;
        IBattleRosterProjection projection = payload != null ? new BattleRosterProjection(payload) : null;
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits = _flowManager != null ? _flowManager.RuntimeUnits : null;
        return new GladiatorRosterView(_selfUnit, payload, projection, runtimeUnits);
    }
}
