using System.Collections.Generic;
using UnityEngine;

// 전투 효과의 실제 적용 지점이다.
// 피해 보정 장신구를 먼저 통과시킨 뒤 HP/치유/버프/넉백과 전투 결과 기록을 수행한다.
public sealed class BattleEffectSystem : IBattleEffectSink
{
    private readonly BattleArtifactSystem _artifacts;
    private BattleCombatResultBuffer _combatResults;
    private IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> _runtimeUnitByState;
    private SphereCollider _battlefieldCollider;
    private BattleScheduledEffectSystem _scheduledEffects;
    private BattleDamageLifecycle _damageLifecycle;
    private IBattleRosterMutationSink _rosterMutations;

    public BattleEffectSystem(BattleArtifactSystem artifacts)
    {
        _artifacts = artifacts;
    }

    public void Configure(
        BattleCombatResultBuffer combatResults,
        IReadOnlyDictionary<BattleUnitCombatState, BattleRuntimeUnit> runtimeUnitByState,
        SphereCollider battlefieldCollider = null
    )
    {
        _combatResults = combatResults;
        _runtimeUnitByState = runtimeUnitByState;
        if (battlefieldCollider != null)
            _battlefieldCollider = battlefieldCollider;
    }

    public IBattleRosterMutationSink RosterMutations => _rosterMutations;

    public void ConfigureLongRunningSystems(
        BattleScheduledEffectSystem scheduledEffects,
        BattleDamageLifecycle damageLifecycle,
        IBattleRosterMutationSink rosterMutations
    )
    {
        _scheduledEffects = scheduledEffects;
        _damageLifecycle = damageLifecycle;
        _rosterMutations = rosterMutations;
    }

    public void DealDamage(BattleDamageRequest request)
    {
        BattleDamageResolution resolution =
            _damageLifecycle != null
                ? _damageLifecycle.BeforeDamage(ref request, this)
                : BattleDamageResolution.Continue;
        if (resolution == BattleDamageResolution.Ignore)
            return;
        if (resolution == BattleDamageResolution.Redirect)
        {
            request.IsRedirected = true;
            DealDamage(request);
            return;
        }

        BattleUnitCombatState target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        // 피해량을 바꾸는 장신구는 ApplyDamage 직전에만 개입한다.
        _artifacts?.ModifyDamage(ref request);

        float requestedAmount = request.Amount;
        float finalAmount = Mathf.Max(0f, requestedAmount);
        target.GetDamageScaleFactors(out float takenPercent, out float reductionPercent);
        finalAmount *= Mathf.Max(0f, 1f + takenPercent / 100f);
        finalAmount *= Mathf.Max(0f, 1f - reductionPercent / 100f);
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);

        bool preventedLethalDamage =
            finalAmount >= target.CurrentHealth
            && _damageLifecycle != null
            && _damageLifecycle.TryPreventLethalDamage(targetRuntime, ref request, this);

        if (resolution == BattleDamageResolution.ClampToMinimumHealth || preventedLethalDamage)
            finalAmount = Mathf.Max(0f, target.CurrentHealth - 1f);

        target.ApplyDamage(finalAmount);

        BattleRuntimeUnit sourceRuntime = ResolveRuntimeUnit(request.Source);
        BattleDamageResult result = new BattleDamageResult(
            request.Source,
            target,
            requestedAmount,
            finalAmount,
            target.IsCombatDisabled
        );

        _combatResults?.Add(
            new BattleCombatResult(sourceRuntime, targetRuntime, finalAmount, target.IsCombatDisabled, request.IsSkill)
        );

        _damageLifecycle?.Accumulate(result);
        _artifacts?.AfterDamage(result, this);
        if (result.TargetDied)
            _artifacts?.OnUnitKilled(new BattleKillEvent(request.Source, target), this);
    }

    public void Heal(BattleHealRequest request)
    {
        BattleUnitCombatState target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        float finalAmount = Mathf.Max(0f, request.Amount);
        target.ApplyHeal(finalAmount);
        _artifacts?.AfterHeal(new BattleHealResult(request.Source, target, request.Amount, finalAmount), this);
    }

    public void ApplyStatus(BattleStatusRequest request)
    {
        BattleUnitCombatState target = request.Target;
        if (target == null || target.IsCombatDisabled)
            return;

        target.ApplyStatus(request);
    }

    public void ApplyBuff(
        BattleUnitCombatState source,
        BattleUnitCombatState target,
        BuffType type,
        int level,
        float duration
    )
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.ApplyStatus(
            new BattleStatusRequest
            {
                Source = source,
                Target = target,
                Type = BattleUnitCombatState.ConvertBuffType(type),
                Level = level,
                Duration = duration,
                IsDebuff = level < 0 || type == BuffType.BleedDamage || type == BuffType.Taunt || type == BuffType.Stun,
                IsDispelAllowed = true,
            }
        );
    }

    public void Dispel(BattleUnitCombatState target, BattleDispelFilter filter)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.Dispel(filter);
    }

    public void RefreshStatuses(BattleUnitCombatState target, BattleStatusFilter filter, float duration)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        target.RefreshStatuses(filter, duration);
    }

    public void Revive(BattleUnitCombatState target, float health)
    {
        if (target == null)
            return;

        target.Revive(health);
    }

    public void AddKnockback(BattleUnitCombatState target, Vector3 direction, float force)
    {
        if (target == null || target.IsCombatDisabled)
            return;

        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        BattleForcedMovementRequest request = new BattleForcedMovementRequest
        {
            Source = null,
            Target = targetRuntime,
            Direction = direction,
            Distance = Mathf.Max(0f, force),
            IsKnockback = true,
            IsTeleport = false,
        };
        if (_artifacts != null && _artifacts.CanIgnoreForcedMovement(targetRuntime, request))
            return;

        target.AddKnockback(direction, force);
    }

    public void Teleport(BattleUnitCombatState target, Vector3 destination)
    {
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        if (targetRuntime == null || targetRuntime.IsCombatDisabled)
            return;

        BattleForcedMovementRequest request = new BattleForcedMovementRequest
        {
            Target = targetRuntime,
            Destination = destination,
            Direction = destination - targetRuntime.Position,
            Distance = Vector3.Distance(targetRuntime.Position, destination),
            IsTeleport = true,
        };
        if (_artifacts != null && _artifacts.CanIgnoreForcedMovement(targetRuntime, request))
            return;

        targetRuntime.SetPosition(destination);
        targetRuntime.ClampInsideBattlefield(_battlefieldCollider);
    }

    public void PullTo(BattleUnitCombatState source, BattleUnitCombatState target, float stopDistance)
    {
        BattleRuntimeUnit sourceRuntime = ResolveRuntimeUnit(source);
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        if (sourceRuntime == null || targetRuntime == null || targetRuntime.IsCombatDisabled)
            return;

        Vector3 delta = sourceRuntime.Position - targetRuntime.Position;
        delta.y = 0f;
        float distance = delta.magnitude;
        float clampedStop = Mathf.Max(0f, stopDistance);
        if (distance <= clampedStop)
            return;

        Vector3 destination = sourceRuntime.Position - delta.normalized * clampedStop;
        BattleForcedMovementRequest request = new BattleForcedMovementRequest
        {
            Source = sourceRuntime,
            Target = targetRuntime,
            Destination = destination,
            Direction = delta.normalized,
            Distance = distance - clampedStop,
        };
        if (_artifacts != null && _artifacts.CanIgnoreForcedMovement(targetRuntime, request))
            return;

        targetRuntime.SetPosition(destination);
        targetRuntime.ClampInsideBattlefield(_battlefieldCollider);
    }

    public void PushToArenaEdge(BattleUnitCombatState source, BattleUnitCombatState target, float slowDuration)
    {
        BattleRuntimeUnit sourceRuntime = ResolveRuntimeUnit(source);
        BattleRuntimeUnit targetRuntime = ResolveRuntimeUnit(target);
        if (targetRuntime == null || targetRuntime.IsCombatDisabled || _battlefieldCollider == null)
            return;

        Vector3 center = _battlefieldCollider.transform.position;
        Vector3 direction = targetRuntime.Position - center;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f && sourceRuntime != null)
        {
            direction = targetRuntime.Position - sourceRuntime.Position;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        float maxRadius = Mathf.Max(
            0f,
            _battlefieldCollider.radius * _battlefieldCollider.transform.lossyScale.x - targetRuntime.BodyRadius
        );
        Vector3 destination = center + direction.normalized * maxRadius;
        destination.y = targetRuntime.Position.y;

        BattleForcedMovementRequest request = new BattleForcedMovementRequest
        {
            Source = sourceRuntime,
            Target = targetRuntime,
            Destination = destination,
            Direction = direction.normalized,
            Distance = Vector3.Distance(targetRuntime.Position, destination),
        };
        if (_artifacts != null && _artifacts.CanIgnoreForcedMovement(targetRuntime, request))
            return;

        targetRuntime.SetPosition(destination);
        if (slowDuration > 0f)
        {
            target.ApplyStatus(
                new BattleStatusRequest
                {
                    Source = source,
                    Target = target,
                    Type = BattleStatusType.Slow,
                    Level = 1,
                    Duration = slowDuration,
                    IsDebuff = true,
                    IsDispelAllowed = true,
                }
            );
        }
    }

    public void PlayVisual(BattleVisualEffectRequest request)
    {
        // TODO
        // Presentation Layer 연결 지점.
        // 실제 파티클/사운드 재생기는 후속 PresentationSystem에서 구독한다.
    }

    public int ScheduleEffect(
        float delay,
        BattleRuntimeUnit source,
        BattleRuntimeUnit target,
        in BattleEffectContext context,
        System.Action<BattleEffectContext, IBattleEffectSink> execute
    )
    {
        if (_scheduledEffects == null)
            return 0;

        return _scheduledEffects.Schedule(delay, source, target, context, execute);
    }

    public void NotifySkillCast(in BattleSkillCastEvent skillCastEvent)
    {
        _artifacts?.OnSkillCast(skillCastEvent, this);
    }

    private BattleRuntimeUnit ResolveRuntimeUnit(BattleUnitCombatState state)
    {
        if (state == null || _runtimeUnitByState == null)
            return null;

        return _runtimeUnitByState.TryGetValue(state, out BattleRuntimeUnit runtime) ? runtime : null;
    }
}
