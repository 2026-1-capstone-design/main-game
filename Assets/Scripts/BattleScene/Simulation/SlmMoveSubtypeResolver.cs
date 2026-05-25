using System.Collections.Generic;
using UnityEngine;

// SLM 이동 명령의 subtype별 목표 좌표를 산출한다.
// SlmCommandUnitPlanner가 종료 판정과 위협 검사에 쓰는 헬퍼도 같이 제공한다.
public static class SlmMoveSubtypeResolver
{
    public struct ResolvedTarget
    {
        public Vector3 Position;
        public bool HasPosition;
    }

    private static SlmCommandTuningSO _tuning;

    public static void SetTuning(SlmCommandTuningSO tuning) => _tuning = tuning;

    private static float ThreatNearRadius => _tuning != null ? _tuning.threatNearRadius : 10f;
    private static float EscapeDistance => _tuning != null ? _tuning.escapeDistance : 10f;
    private static float FlankStepDistance => _tuning != null ? _tuning.flankStepDistance : 3f;

    // 진입점. flank는 상태가 필요해 Planner에서 ResolveFlankArc를 직접 호출하므로 여기서는 처리하지 않는다.
    public static ResolvedTarget Resolve(
        BattleRuntimeUnit actor,
        SlmUnitCommand cmd,
        IReadOnlyList<BattleRuntimeUnit> units
    )
    {
        if (actor == null || actor.IsCombatDisabled || units == null)
            return Invalid();

        switch (cmd.MoveSubtype)
        {
            case SlmMoveSubtype.Escape:
                return ResolveEscape(actor, cmd.Target, units);

            case SlmMoveSubtype.ApproachOpponent:
                return ResolveApproachOpponent(actor, cmd.Target);

            case SlmMoveSubtype.Help:
                // help는 좌표 산출 대신 Planner의 attack 처리로 위임된다.
                return Invalid();

            case SlmMoveSubtype.HoldFront:
                // target팀에 따라 적군/아군 분기. 알고리즘은 approachOpponent 한 가지를 공유한다.
                if (cmd.Target == null || cmd.Target.IsCombatDisabled)
                    return Invalid();
                if (cmd.Target.State.TeamId.Equals(actor.State.TeamId))
                    return ResolveApproachTeam(actor, cmd.Target);
                return ResolveApproachOpponent(actor, cmd.Target);

            default:
                return Invalid();
        }
    }

    // 도주 기준점의 반대 방향으로 EscapeDistance만큼 떨어진 좌표를 목적지로 반환한다.
    // 경기장 경계 근처에서 외부로 향하면 접선 방향으로 보정한다. 적이 없으면 Invalid 반환.
    private static ResolvedTarget ResolveEscape(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit slmTarget,
        IReadOnlyList<BattleRuntimeUnit> units
    )
    {
        if (!TryGetEscapeReferenceCentroid(actor, slmTarget, units, out Vector3 referenceCentroid))
            return Invalid();

        Vector3 actorPos = actor.Position;
        Vector3 toReference = referenceCentroid - actorPos;
        toReference.y = 0f;

        Vector3 awayDirection =
            toReference.sqrMagnitude < 0.0001f ? GetFallbackForwardDirection(actor) : -toReference.normalized;

        // 경기장 경계 처리: actor가 경계 근처에서 외부 향한 도주 방향이면 접선 방향으로 보정.
        awayDirection = AdjustDirectionAtBoundary(actorPos, awayDirection, actor.State.BodyRadius);

        Vector3 targetPos = actorPos + awayDirection * EscapeDistance;
        return new ResolvedTarget { Position = targetPos, HasPosition = true };
    }

    // 경기장 경계 BoundaryNearMargin 이내에서 외부로 향한 방향을 접선 방향으로 클램프한다.
    // BattleSimulationManager.BattlefieldCollider가 null이면 원래 방향을 그대로 반환한다.
    private const float BoundaryNearMargin = 1f;

    private static Vector3 AdjustDirectionAtBoundary(Vector3 actorPos, Vector3 dir, float bodyRadius)
    {
        SphereCollider battlefield =
            BattleSimulationManager.Instance != null ? BattleSimulationManager.Instance.BattlefieldCollider : null;
        if (battlefield == null)
            return dir;

        Vector3 center = battlefield.bounds.center;
        float arenaRadius = battlefield.bounds.extents.x;
        float boundaryRadius = arenaRadius - bodyRadius;
        if (boundaryRadius <= 0f)
            return dir;

        Vector3 fromCenter = actorPos - center;
        fromCenter.y = 0f;
        float distFromCenter = fromCenter.magnitude;

        // 경계에서 충분히 안쪽이면 원래 방향 그대로 유지.
        if (distFromCenter < boundaryRadius - BoundaryNearMargin)
            return dir;

        if (distFromCenter < 0.0001f)
            return dir; // actor가 정확히 중심이면 보정 불가

        Vector3 outward = fromCenter / distFromCenter; // 중심에서 actor 방향 (경계 바깥쪽)
        float outwardComponent = Vector3.Dot(dir, outward);

        // 도주 방향이 안쪽 향하거나 접선 방향이면 보정 불필요.
        if (outwardComponent <= 0f)
            return dir;

        // 외부 성분을 제거하면 접선 성분만 남는다.
        Vector3 tangent = dir - outward * outwardComponent;

        if (tangent.sqrMagnitude < 0.0001f)
        {
            // 도주 방향이 완전히 외부 향하면 시계 방향 접선으로 강제한다.
            tangent = new Vector3(-outward.z, 0f, outward.x);
        }

        tangent.y = 0f;
        return tangent.normalized;
    }

    // 목표 좌표를 적 위치 그 자체로 둔다.
    // PhysicsSystem의 stop distance가 사거리 안에서 actor를 멈춰주고, Planner가 종료를 판정한다.
    private static ResolvedTarget ResolveApproachOpponent(BattleRuntimeUnit actor, BattleRuntimeUnit target)
    {
        if (target == null || target.IsCombatDisabled)
            return Invalid();

        Vector3 actorPos = actor.Position;
        Vector3 targetPos = target.Position;
        Vector3 fromTargetToActor = actorPos - targetPos;
        fromTargetToActor.y = 0f;

        float currentDistance = fromTargetToActor.magnitude;
        // 사거리는 게임 본체의 GetEffectiveAttackDistance를 그대로 사용 (자체 합산 X).
        float effectiveRange = BattleFieldSnapshot.GetEffectiveAttackDistance(actor.State, target.State);

        if (currentDistance <= effectiveRange)
        {
            // 이미 사거리 내. 현재 위치를 유지하고 Planner의 종료 판정에 맡긴다.
            return new ResolvedTarget { Position = actorPos, HasPosition = true };
        }

        return new ResolvedTarget { Position = targetPos, HasPosition = true };
    }

    // holdFront의 아군 분기에서만 호출되는 wrapper.
    // GetEffectiveAttackDistance가 team 검증을 안 하므로 ResolveApproachOpponent를 그대로 사용한다.
    private static ResolvedTarget ResolveApproachTeam(BattleRuntimeUnit actor, BattleRuntimeUnit ally)
    {
        return ResolveApproachOpponent(actor, ally);
    }

    // 탈레스 원의 호를 따라 우회 접근한다.
    // 호의 양 끝은 (명령 시작 시 actor 위치)와 (현재 target 위치), 굴곡 방향은 flankSign으로 고정한다.
    // 매 tick 호를 재산출해 actor 현재 위치에서 가까운 호 점의 접선 방향으로 FlankStepDistance만큼 전진시킨다.
    public static ResolvedTarget ResolveFlankArc(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit target,
        Vector3 actorStartPos,
        int flankSign
    )
    {
        if (target == null || target.IsCombatDisabled)
            return Invalid();

        Vector3 actorPos = actor.Position;
        Vector3 targetPos = target.Position;

        // 호 양 끝점 사이 벡터 (시작점 고정 + 현재 target 위치)
        Vector3 along = targetPos - actorStartPos;
        along.y = 0f;
        float atDist = along.magnitude;
        if (atDist < 0.0001f)
        {
            return new ResolvedTarget { Position = actorPos, HasPosition = true };
        }

        Vector3 uAlong = along / atDist;
        // 좌수직 (xz 평면 90도 회전) × 부호. flankSign이 ±1.
        Vector3 uPerp = new Vector3(-uAlong.z, 0f, uAlong.x) * flankSign;

        Vector3 circleCenter = (actorStartPos + targetPos) * 0.5f;
        float circleRadius = atDist * 0.5f;

        // actor 현재 위치에서 원 위 가장 가까운 점 (= 원 중심에서 actor 방향으로 r 떨어진 점).
        Vector3 fromCenter = actorPos - circleCenter;
        fromCenter.y = 0f;

        Vector3 onArcDir;
        if (fromCenter.sqrMagnitude < 0.0001f)
        {
            // actor가 원 중심에 있으면 호 정점 방향으로 출발한다.
            onArcDir = uPerp;
        }
        else
        {
            onArcDir = fromCenter.normalized;
            // uPerp 쪽 호로 강제하고, 반대편 호로 빠지면 미러시킨다.
            if (Vector3.Dot(onArcDir, uPerp) < 0f)
            {
                // uAlong 축 기준 반사 = uPerp 성분 부호 반전.
                float perpComponent = Vector3.Dot(onArcDir, uPerp);
                onArcDir = (onArcDir - 2f * perpComponent * uPerp).normalized;
            }
        }

        Vector3 nearestOnArc = circleCenter + onArcDir * circleRadius;

        // 호 접선 = 원 중심에서 P_near 방향에 수직 + T 쪽으로 진행.
        Vector3 tangent = new Vector3(-onArcDir.z, 0f, onArcDir.x);
        if (Vector3.Dot(tangent, uAlong) < 0f)
            tangent = -tangent;

        // 다음 waypoint 후보: P_near에서 접선 방향 step만큼.
        Vector3 candidateWaypoint = nearestOnArc + tangent * FlankStepDistance;

        // 경기장 boundary 보정: actor 위치 기준 진행 방향을 접선으로 클램프.
        Vector3 toWaypoint = candidateWaypoint - actorPos;
        toWaypoint.y = 0f;
        Vector3 dir = toWaypoint.sqrMagnitude < 0.0001f ? tangent : toWaypoint.normalized;
        dir = AdjustDirectionAtBoundary(actorPos, dir, actor.State.BodyRadius);

        Vector3 waypoint = actorPos + dir * FlankStepDistance;
        return new ResolvedTarget { Position = waypoint, HasPosition = true };
    }

    // 명령 시작 시점에 1회만 호출한다.
    // 적 군집 중심이 actor-target 직선의 좌/우 어느 쪽에 있는지 외적으로 판정해 반대편 호 부호를 반환한다.
    // 적이 없으면 +1을 기본값으로 반환한다.
    public static int ComputeFlankSign(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit target,
        IReadOnlyList<BattleRuntimeUnit> units
    )
    {
        if (actor == null || target == null)
            return 1;

        if (!TryComputeEnemyCentroid(actor, units, out Vector3 enemyCentroid))
            return 1;

        Vector3 actorPos = actor.Position;
        Vector3 along = target.Position - actorPos;
        along.y = 0f;
        Vector3 toCentroid = enemyCentroid - actorPos;
        toCentroid.y = 0f;

        // xz 평면 외적의 y 성분이 양수면 적 군집이 along 기준 좌측이므로 우측 호(부호 -1),
        // 음수 또는 0이면 좌측 호(부호 +1)를 선택한다.
        float crossY = along.x * toCentroid.z - along.z * toCentroid.x;
        return crossY > 0f ? -1 : 1;
    }

    // escape의 우선순위로 도주 기준점(중심)을 산출한다. ShouldAdvance가 종료 판정에 사용한다.
    public static bool TryGetEscapeReferenceCentroid(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit slmTarget,
        IReadOnlyList<BattleRuntimeUnit> units,
        out Vector3 centroid
    )
    {
        centroid = Vector3.zero;
        if (actor == null || units == null)
            return false;

        // 1) SLM 지정 적이 살아있으면 최우선.
        if (slmTarget != null && !slmTarget.IsCombatDisabled)
        {
            centroid = slmTarget.Position;
            return true;
        }

        Vector3 actorPos = actor.Position;
        BattleTeamId actorTeam = actor.State.TeamId;

        Vector3 threatCentroid = Vector3.zero;
        int threatCount = 0;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit u = units[i];
            if (u == null || u.IsCombatDisabled)
                continue;
            if (u.State.TeamId.Equals(actorTeam))
                continue;

            bool isTargetingActor = u.State.PlannedTargetEnemy != null && u.State.PlannedTargetEnemy == actor.State;

            Vector3 diff = u.Position - actorPos;
            diff.y = 0f;
            bool isNear = diff.sqrMagnitude <= (ThreatNearRadius * ThreatNearRadius);

            // 위협 = actor를 노리면서 동시에 ThreatNearRadius 이내인 적.
            if (isTargetingActor && isNear)
            {
                threatCentroid += u.Position;
                threatCount++;
            }
        }

        // 2) 위협(자기를 노리고 가까운 적) 평균
        if (threatCount > 0)
        {
            centroid = threatCentroid / threatCount;
            return true;
        }

        // 3) 위협 없으면 적 군집 전체 중심으로 fallback.
        return TryComputeEnemyCentroid(actor, units, out centroid);
    }

    // help의 위협 검사 헬퍼.
    // "ally를 PlannedTargetEnemy로 타겟팅 중인 적의 수"를 반환하고,
    // 그 중 ally에게 가장 가까운 적을 closestAttacker로 반환한다.
    public static int CountAttackersOf(
        BattleRuntimeUnit ally,
        IReadOnlyList<BattleRuntimeUnit> units,
        out BattleRuntimeUnit closestAttacker
    )
    {
        closestAttacker = null;
        if (ally == null || units == null)
            return 0;

        BattleTeamId allyTeam = ally.State.TeamId;
        Vector3 allyPos = ally.Position;
        int count = 0;
        float minDistSqr = float.MaxValue;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit u = units[i];
            if (u == null || u.IsCombatDisabled)
                continue;
            if (u.State.TeamId.Equals(allyTeam))
                continue;
            if (u.State.PlannedTargetEnemy != ally.State)
                continue;

            count++;

            Vector3 diff = u.Position - allyPos;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closestAttacker = u;
            }
        }

        return count;
    }

    // 적 군집 전체 중심 (살아있는 적 평균 위치).
    public static bool TryComputeEnemyCentroid(
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> units,
        out Vector3 centroid
    )
    {
        centroid = Vector3.zero;
        if (actor == null || units == null)
            return false;

        BattleTeamId actorTeam = actor.State.TeamId;
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit u = units[i];
            if (u == null || u.IsCombatDisabled)
                continue;
            if (u.State.TeamId.Equals(actorTeam))
                continue;

            sum += u.Position;
            count++;
        }

        if (count == 0)
            return false;

        centroid = sum / count;
        return true;
    }

    // 위협 위치와 액터 위치가 사실상 겹친 경우 사용하는 fallback 방향.
    private static Vector3 GetFallbackForwardDirection(BattleRuntimeUnit actor)
    {
        Vector3 forward = actor.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        return forward.normalized;
    }

    private static ResolvedTarget Invalid() => new ResolvedTarget { Position = Vector3.zero, HasPosition = false };
}
