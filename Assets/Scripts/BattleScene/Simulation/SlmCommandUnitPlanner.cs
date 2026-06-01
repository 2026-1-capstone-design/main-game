using System.Collections.Generic;
using UnityEngine;

// SLM이 액터에게 내린 명령 시퀀스를 매 tick BattleControlPlan으로 변환한다.
// IBattleControlPlanner를 직접 구현하지 않고 PlayerCommandControlBuffer에 plan을 채우는 방식이라,
// 명령이 끝나 buffer.Clear되면 controller stack이 ML/BuiltInAi로 자동 fallback한다.
public sealed class SlmCommandUnitPlanner
{
    private sealed class SlmActiveCommandState
    {
        public List<SlmUnitCommand> Sequence;
        public int CurrentIndex;
        public float ElapsedSecOnCurrent;
        public float GraceRemainingSec;

        // skill 액션이 1회 시전됐는지 표시 (ShouldAdvance가 종료 판정에 사용).
        public bool SkillFired;

        // skillControl 액션이 SkillDisabled status 부여를 1회 처리했는지 표시.
        public bool SkillDisabledStatusApplied;

        // flank 명령 상태. 호 부호와 시작점을 명령 시작 시 1회 고정해 진행 방향 흔들림을 막는다.
        public bool FlankInitialized;
        public int FlankSign;
        public Vector3 FlankStartActorPos;
    }

    private SlmCommandTuningSO _tuning;

    public void SetTuning(SlmCommandTuningSO tuning) => _tuning = tuning;

    private float CommandCompletionGraceSec => _tuning != null ? _tuning.commandCompletionGraceSec : 0.5f;
    private float AttackDurationSec => _tuning != null ? _tuning.attackDurationSec : 10f;
    private float MoveTimeoutSec => _tuning != null ? _tuning.moveTimeoutSec : 10f;
    private float SkillTimeoutSec => _tuning != null ? _tuning.skillTimeoutSec : 10f;
    private float DefaultWaitDurationSec => _tuning != null ? _tuning.defaultWaitDurationSec : 10f;
    private float EscapeDistance => _tuning != null ? _tuning.escapeDistance : 10f;
    private int HelpThreatThreshold => _tuning != null ? _tuning.helpThreatThreshold : 1;
    private float WaitMinSec => _tuning != null ? _tuning.waitMinSec : 1f;
    private float WaitMaxSec => _tuning != null ? _tuning.waitMaxSec : 10f;

    private readonly Dictionary<BattleUnitCombatState, SlmActiveCommandState> _activeByState =
        new Dictionary<BattleUnitCombatState, SlmActiveCommandState>();

    // foreach 중에 dictionary 수정할 수 없어 종료된 액터를 임시로 모았다가 루프 밖에서 제거한다.
    private readonly List<BattleUnitCombatState> _completedThisTick = new List<BattleUnitCombatState>();

    public void IssueCommands(BattleUnitCombatState actor, IReadOnlyList<SlmUnitCommand> commands)
    {
        if (actor == null || commands == null || commands.Count == 0)
            return;

        _activeByState[actor] = new SlmActiveCommandState
        {
            Sequence = new List<SlmUnitCommand>(commands),
            CurrentIndex = 0,
            ElapsedSecOnCurrent = 0f,
            GraceRemainingSec = 0f,
            SkillFired = false,
            SkillDisabledStatusApplied = false,
        };
    }

    public bool HasActiveCommand(BattleUnitCombatState state) => state != null && _activeByState.ContainsKey(state);

    public void Clear(BattleUnitCombatState state)
    {
        if (state != null)
            _activeByState.Remove(state);
    }

    public void ClearAll()
    {
        _activeByState.Clear();
        _completedThisTick.Clear();
    }

    // 매 tick BattleSimulationManager가 호출.
    public void Tick(in BattlePlanningContext context, PlayerCommandControlBuffer buffer)
    {
        if (buffer == null)
            return;

        _completedThisTick.Clear();

        foreach (var pair in _activeByState)
        {
            BattleUnitCombatState actor = pair.Key;
            SlmActiveCommandState entry = pair.Value;

            // 시퀀스 끝난 후 grace 처리
            if (entry.CurrentIndex >= entry.Sequence.Count)
            {
                entry.GraceRemainingSec -= context.TickDeltaTime;
                if (entry.GraceRemainingSec <= 0f)
                {
                    _completedThisTick.Add(actor);
                    continue;
                }
                buffer.SetPlan(actor, BuildHoldPlan());
                continue;
            }

            SlmUnitCommand current = entry.Sequence[entry.CurrentIndex];
            BattleRuntimeUnit actorRuntime = FindRuntimeUnit(actor, context.Units);

            if (IsHoldFrontSelfCommand(current, actorRuntime))
            {
                if (entry.CurrentIndex + 1 >= entry.Sequence.Count)
                {
                    _completedThisTick.Add(actor);
                    buffer.Clear(actor);
                    continue;
                }

                AdvanceOrEndSequence(entry);
                buffer.Clear(actor);
                continue;
            }

            // 공통 종료: 타겟이 필요한 액션에서 타겟이 사망/null이면 즉시 다음 액션으로.
            // skill은 canSkillTargetDead=true인 죽은 아군 target을 예외로 허용한다.
            if (IsTargetInvalidForCurrent(actorRuntime, current))
            {
                AdvanceOrEndSequence(entry);
                buffer.SetPlan(actor, BuildHoldPlan());
                continue;
            }

            entry.ElapsedSecOnCurrent += context.TickDeltaTime;

            // skillControl 특수: SkillDisabled status 부여 + buffer 비움.
            // NoSkill과 DeferSkill 모두 일정 시간 스킬 사용을 막고, 이동/공격 주도권은 하위 controller로 넘긴다.
            if (current.Kind == SlmCommandKind.NoSkill || current.Kind == SlmCommandKind.DeferSkill)
            {
                if (!entry.SkillDisabledStatusApplied)
                {
                    float duration = ClampWaitSec(current.DurationSec);
                    actor.ApplyStatus(
                        new BattleStatusRequest
                        {
                            Source = actor,
                            Target = actor,
                            Type = BattleStatusType.SkillDisabled,
                            Level = 1,
                            Duration = duration,
                            IsDebuff = false,
                            IsDispelAllowed = false,
                        }
                    );
                    entry.SkillDisabledStatusApplied = true;
                }

                buffer.Clear(actor);
            }
            else
            {
                BattleControlPlan plan = current.Kind switch
                {
                    SlmCommandKind.Attack => BuildAttackPlan(actor, current),
                    SlmCommandKind.Move => BuildMovePlan(actor, current, in context),
                    SlmCommandKind.Skill => BuildSkillPlan(actor, current, entry, actorRuntime),
                    SlmCommandKind.Wait => BuildHoldPlan(),
                    SlmCommandKind.DeferSkill => BuildHoldPlan(),
                    _ => BuildHoldPlan(),
                };
                buffer.SetPlan(actor, plan);
            }

            // 종료 판정
            if (ShouldAdvance(current, actor, context, entry))
            {
                AdvanceOrEndSequence(entry);
            }
        }

        // 종료된 액터: dictionary에서 제거 + buffer 비우기.
        for (int i = 0; i < _completedThisTick.Count; i++)
        {
            BattleUnitCombatState actor = _completedThisTick[i];
            _activeByState.Remove(actor);
            buffer.Clear(actor);
        }
    }

    // 다음 액션으로 진행하거나 시퀀스 끝이면 grace 시작.
    private void AdvanceOrEndSequence(SlmActiveCommandState entry)
    {
        entry.CurrentIndex++;
        entry.ElapsedSecOnCurrent = 0f;
        entry.SkillFired = false;
        entry.SkillDisabledStatusApplied = false;

        // flank 상태 초기화. 다음 명령이 또 flank여도 새 호로 재시작한다.
        entry.FlankInitialized = false;
        entry.FlankSign = 0;
        entry.FlankStartActorPos = Vector3.zero;

        if (entry.CurrentIndex >= entry.Sequence.Count)
        {
            entry.GraceRemainingSec = CommandCompletionGraceSec;
        }
    }

    private float ClampWaitSec(float requestedSec)
    {
        float sec = requestedSec > 0f ? requestedSec : DefaultWaitDurationSec;
        return Mathf.Clamp(sec, WaitMinSec, WaitMaxSec);
    }

    // 타겟이 필요한 액션에서 타겟 null/사망 검사 (공통 종료 조건).
    private static bool IsTargetInvalidForCurrent(BattleRuntimeUnit actorRuntime, in SlmUnitCommand cmd)
    {
        switch (cmd.Kind)
        {
            case SlmCommandKind.Attack:
                return cmd.Target == null || cmd.Target.IsCombatDisabled;

            case SlmCommandKind.Skill:
                if (cmd.Target == null)
                    return true;

                if (!cmd.Target.IsCombatDisabled)
                    return false;

                return !CanSkillTargetDeadAlly(actorRuntime, cmd.Target);

            case SlmCommandKind.Move:
                if (cmd.MoveSubtype == SlmMoveSubtype.Escape)
                    return false;

                return cmd.Target == null || cmd.Target.IsCombatDisabled;

            default:
                return false;
        }
    }

    private static bool IsHoldFrontSelfCommand(in SlmUnitCommand cmd, BattleRuntimeUnit actorRuntime)
    {
        return actorRuntime != null
            && cmd.Kind == SlmCommandKind.Move
            && cmd.MoveSubtype == SlmMoveSubtype.HoldFront
            && cmd.Target == actorRuntime;
    }

    private static bool CanSkillTargetDeadAlly(BattleRuntimeUnit actorRuntime, BattleRuntimeUnit targetRuntime)
    {
        if (actorRuntime == null || actorRuntime.State == null)
            return false;

        if (targetRuntime == null || targetRuntime.State == null)
            return false;

        if (!targetRuntime.IsCombatDisabled)
            return false;

        if (!targetRuntime.State.TeamId.Equals(actorRuntime.State.TeamId))
            return false;

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actorRuntime);
        return metadata.canSkillTargetDead;
    }

    // ---------- 액션별 plan 생성 ----------

    // attack: 사거리 안이면 공격, 밖이면 그 적 방향으로 이동한다.
    private static BattleControlPlan BuildAttackPlan(BattleUnitCombatState self, SlmUnitCommand cmd)
    {
        BattleUnitCombatState target = cmd.Target != null ? cmd.Target.State : null;
        bool hasValidTarget = target != null && BattleFieldSnapshot.IsValidEnemyTarget(self, target);
        // 모든 액션이 BattleFieldSnapshot.GetEffectiveAttackDistance로 사거리를 판정해 일관성을 유지한다.
        bool inRange = false;
        if (hasValidTarget)
        {
            Vector3 diff = target.Position - self.Position;
            diff.y = 0f;
            float threshold = BattleFieldSnapshot.GetEffectiveAttackDistance(self, target);
            inRange = diff.sqrMagnitude <= threshold * threshold;
        }

        BattleMove move = inRange || !hasValidTarget ? BattleMove.Hold() : BattleMove.ToTarget(target);
        BattleCombatIntent combatIntent = inRange ? BattleCombatIntent.Attack : BattleCombatIntent.None;
        BattleFacingIntent facingIntent = hasValidTarget
            ? BattleFacingIntent.TargetEnemy
            : BattleFacingIntent.KeepCurrent;

        return new BattleControlPlan(target, null, move, combatIntent, facingIntent);
    }

    // move: subtype/style별 좌표를 산출해 절대 위치 이동으로 둔다.
    // help는 좌표 산출 없이 위협 적을 attack으로 처리한다 (BuildHelpAttackPlan).
    private BattleControlPlan BuildMovePlan(
        BattleUnitCombatState self,
        SlmUnitCommand cmd,
        in BattlePlanningContext context
    )
    {
        BattleRuntimeUnit actor = FindRuntimeUnit(self, context.Units);
        if (actor == null)
            return BuildHoldPlan();

        // help는 좌표 산출 안 함. movementType과 무관하게 위협 적 직접 attack.
        if (cmd.MoveSubtype == SlmMoveSubtype.Help)
        {
            return BuildHelpAttackPlan(self, cmd, context);
        }

        SlmMoveSubtypeResolver.ResolvedTarget resolved;

        if (cmd.MoveSubtype == SlmMoveSubtype.Escape)
        {
            resolved = SlmMoveSubtypeResolver.Resolve(actor, cmd, context.Units);
        }
        else if (cmd.MoveStyle == SlmMoveStyle.Flank)
        {
            // flank 명령 시작 시 1회 초기화: 적 군집 중심 반대편 호 부호 + 시작 actor 위치 고정
            if (!_activeByState.TryGetValue(self, out SlmActiveCommandState entry))
                return BuildHoldPlan();

            if (!entry.FlankInitialized)
            {
                entry.FlankSign = SlmMoveSubtypeResolver.ComputeFlankSign(actor, cmd.Target, context.Units);
                entry.FlankStartActorPos = actor.Position;
                entry.FlankInitialized = true;
            }

            resolved = SlmMoveSubtypeResolver.ResolveFlankArc(
                actor,
                cmd.Target,
                entry.FlankStartActorPos,
                entry.FlankSign
            );
        }
        else
        {
            resolved = SlmMoveSubtypeResolver.Resolve(actor, cmd, context.Units);
        }

        if (!resolved.HasPosition)
            return BuildHoldPlan();

        // flank는 곡선 이동 + 이동 방향 시선 동기화
        BattleFacingIntent facing =
            cmd.MoveStyle == SlmMoveStyle.Flank ? BattleFacingIntent.MoveDirection : BattleFacingIntent.DesiredPosition;

        return new BattleControlPlan(
            null,
            null,
            BattleMove.ToAbsolutePosition(resolved.Position),
            BattleCombatIntent.None,
            facing
        );
    }

    // help: 지정 아군을 PlannedTargetEnemy로 타겟팅하는 적 중 그 아군에게 가장 가까운 적을 attack
    private BattleControlPlan BuildHelpAttackPlan(
        BattleUnitCombatState self,
        SlmUnitCommand cmd,
        in BattlePlanningContext context
    )
    {
        if (cmd.Target == null)
            return BuildHoldPlan();

        int attackerCount = SlmMoveSubtypeResolver.CountAttackersOf(
            cmd.Target,
            context.Units,
            out BattleRuntimeUnit closestAttacker
        );

        if (attackerCount < HelpThreatThreshold || closestAttacker == null)
        {
            // 위협이 부족하면 Hold 상태로 두고, ShouldAdvance가 곧 종료시킨다.
            return BuildHoldPlan();
        }

        BattleUnitCombatState threatState = closestAttacker.State;
        // 모든 액션과 동일하게 BattleFieldSnapshot.GetEffectiveAttackDistance로 사거리를 판정한다.
        bool inRange = false;
        if (BattleFieldSnapshot.IsValidEnemyTarget(self, threatState))
        {
            Vector3 diff = threatState.Position - self.Position;
            diff.y = 0f;
            float threshold = BattleFieldSnapshot.GetEffectiveAttackDistance(self, threatState);
            inRange = diff.sqrMagnitude <= threshold * threshold;
        }

        BattleMove move = inRange ? BattleMove.Hold() : BattleMove.ToTarget(threatState);
        BattleCombatIntent combatIntent = inRange ? BattleCombatIntent.Attack : BattleCombatIntent.None;

        return new BattleControlPlan(threatState, null, move, combatIntent, BattleFacingIntent.TargetEnemy);
    }

    // skill: cooldown 또는 SkillDisabled status면 Hold.
    // 사거리 안이면 Skill 발동 + entry.SkillFired = true.
    // 사거리 부족이면 MoveToTarget으로 자동 접근한다.
    // canSkillTargetDead=true인 스킬은 죽은 아군 target도 실행 대상에서 제거하지 않는다.
    private static BattleControlPlan BuildSkillPlan(
        BattleUnitCombatState self,
        SlmUnitCommand cmd,
        SlmActiveCommandState entry,
        BattleRuntimeUnit actorRuntime
    )
    {
        if (self == null)
            return BuildHoldPlan();

        if (self.IsSkillDisabled)
            return BuildHoldPlan();

        if (self.SkillCooldownRemaining > 0f)
            return BuildHoldPlan();

        BattleUnitCombatState target = cmd.Target != null ? cmd.Target.State : null;
        if (target == null)
            return BuildHoldPlan();

        if (target.IsCombatDisabled && !CanSkillTargetDeadAlly(actorRuntime, cmd.Target))
            return BuildHoldPlan();

        float effectiveRange = BattleFieldSnapshot.GetEffectiveAttackDistance(self, target);
        Vector3 diff = target.Position - self.Position;
        diff.y = 0f;
        bool inRange = diff.sqrMagnitude <= effectiveRange * effectiveRange;

        BattleMove move = inRange ? BattleMove.Hold() : BattleMove.ToTarget(target);
        BattleCombatIntent combatIntent = inRange ? BattleCombatIntent.Skill : BattleCombatIntent.None;

        if (inRange)
            entry.SkillFired = true;

        return new BattleControlPlan(target, null, move, combatIntent, BattleFacingIntent.TargetEnemy);
    }

    private static BattleControlPlan BuildHoldPlan()
    {
        return new BattleControlPlan(
            null,
            null,
            BattleMove.Hold(),
            BattleCombatIntent.None,
            BattleFacingIntent.KeepCurrent
        );
    }

    // 액션 종료 판정.
    private bool ShouldAdvance(
        in SlmUnitCommand current,
        BattleUnitCombatState self,
        in BattlePlanningContext context,
        SlmActiveCommandState entry
    )
    {
        float elapsedSec = entry.ElapsedSecOnCurrent;

        switch (current.Kind)
        {
            case SlmCommandKind.Attack:
                // 타겟 사망은 IsTargetInvalidForCurrent에서 이미 처리됨.
                return elapsedSec >= AttackDurationSec;

            case SlmCommandKind.Move:
                if (elapsedSec >= MoveTimeoutSec)
                    return true;

                BattleRuntimeUnit actor = FindRuntimeUnit(self, context.Units);
                if (actor == null)
                    return true;

                // flank는 approachOpponent와 동일하게 사거리 전체로 종료를 판정한다.
                if (current.MoveStyle == SlmMoveStyle.Flank)
                    return IsApproachOpponentInRange(actor, current.Target);

                switch (current.MoveSubtype)
                {
                    case SlmMoveSubtype.Escape:
                        // 도주 기준점 없으면(적 없음) 즉시 종료.
                        if (
                            !SlmMoveSubtypeResolver.TryGetEscapeReferenceCentroid(
                                actor,
                                current.Target,
                                context.Units,
                                out Vector3 escapeCentroid
                            )
                        )
                        {
                            return true;
                        }
                        // 절대 거리 ≥ EscapeDistance면 종료.
                        Vector3 toCentroid = escapeCentroid - actor.Position;
                        toCentroid.y = 0f;
                        return toCentroid.sqrMagnitude >= EscapeDistance * EscapeDistance;

                    case SlmMoveSubtype.ApproachOpponent:
                        return IsApproachOpponentInRange(actor, current.Target);

                    case SlmMoveSubtype.Help:
                        int count = SlmMoveSubtypeResolver.CountAttackersOf(current.Target, context.Units, out _);
                        return count < HelpThreatThreshold;

                    case SlmMoveSubtype.HoldFront:
                        // approachOpponent와 동일 기준. GetEffectiveAttackDistance가 team 검증을 안 하므로 아군 타겟에도 그대로 적용된다.
                        return IsApproachOpponentInRange(actor, current.Target);

                    default:
                        return true;
                }

            case SlmCommandKind.Skill:
                if (elapsedSec >= SkillTimeoutSec)
                    return true;
                // SkillDisabled / cooldown이면 발동 불가이므로 즉시 종료해 ML에 주도권을 넘긴다.
                if (self.IsSkillDisabled)
                    return true;
                if (self.SkillCooldownRemaining > 0f)
                    return true;
                // 1회 시전이 완료되면 종료.
                return entry.SkillFired;

            case SlmCommandKind.Wait:
            case SlmCommandKind.NoSkill:
            case SlmCommandKind.DeferSkill:
                // 일단 noskill과 비슷하게 처리. 나중에 실제 쿨타임 연장 필요
                float duration = ClampWaitSec(current.DurationSec);
                return elapsedSec >= duration;

            default:
                return true;
        }
    }

    // ApproachOpponent / flank의 공통 종료 판정.
    // 사거리는 BattleFieldSnapshot.GetEffectiveAttackDistance에 위임하고 거리 비교만 직접 수행한다.
    private static bool IsApproachOpponentInRange(BattleRuntimeUnit actor, BattleRuntimeUnit target)
    {
        if (target == null || target.IsCombatDisabled)
            return true;

        Vector3 diff = target.Position - actor.Position;
        diff.y = 0f;
        float threshold = BattleFieldSnapshot.GetEffectiveAttackDistance(actor.State, target.State);
        return diff.sqrMagnitude <= threshold * threshold;
    }

    // BattleUnitCombatState에서 BattleRuntimeUnit으로의 역참조 헬퍼.
    // BattleSimulationManager가 이미 유지하는 dictionary를 그대로 사용해 O(1) 조회한다.
    // Instance가 null이거나 매핑이 없으면 fallback 선형 검색으로 떨어진다.
    private static BattleRuntimeUnit FindRuntimeUnit(
        BattleUnitCombatState state,
        IReadOnlyList<BattleRuntimeUnit> units
    )
    {
        if (state == null)
            return null;

        BattleSimulationManager sim = BattleSimulationManager.Instance;
        if (sim != null && sim.RuntimeUnitByState != null)
        {
            return sim.RuntimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
        }

        // Fallback: BattleSimulationManager 미초기화 상태 등 예외 시 선형 검색.
        // 일반 게임 흐름에서는 도달하지 않는다.
        if (units == null)
            return null;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit u = units[i];
            if (u != null && u.State == state)
                return u;
        }
        return null;
    }
}
