using UnityEngine;

public readonly struct GladiatorRewardEvaluation
{
    public readonly float Reward;
    public readonly bool RequestsBoundaryReset;
    public readonly GladiatorAgentAction EffectiveAction;
    public readonly bool UpdatesTargetDistance;
    public readonly float TargetDistance;

    public GladiatorRewardEvaluation(
        float reward,
        bool requestsBoundaryReset,
        GladiatorAgentAction effectiveAction,
        bool updatesTargetDistance,
        float targetDistance
    )
    {
        Reward = reward;
        RequestsBoundaryReset = requestsBoundaryReset;
        EffectiveAction = effectiveAction;
        UpdatesTargetDistance = updatesTargetDistance;
        TargetDistance = targetDistance;
    }
}

public sealed class GladiatorRewardEvaluator
{
    private readonly GladiatorRewardConfig _config;
    private readonly float _hardBoundaryRadiusMultiplier;
    private Vector2 _previousRawMove;
    private bool _hasPreviousRawAction;

    public GladiatorRewardEvaluator(GladiatorRewardConfig config, float hardBoundaryRadiusMultiplier)
    {
        _config = config;
        _hardBoundaryRadiusMultiplier = hardBoundaryRadiusMultiplier;
    }

    public void Reset()
    {
        _previousRawMove = Vector2.zero;
        _hasPreviousRawAction = false;
    }

    public GladiatorRewardEvaluation EvaluateActionStep(
        GladiatorAgentAction action,
        GladiatorAgentTacticalContext context
    )
    {
        float reward = _config.step;
        reward += EvaluateSmoothness(action);

        if (context.DistanceFromCenter >= context.PlayableRadius)
        {
            reward += _config.boundary;
            if (context.DistanceFromCenter >= context.PlayableRadius * _hardBoundaryRadiusMultiplier)
            {
                return new GladiatorRewardEvaluation(
                    reward,
                    requestsBoundaryReset: true,
                    action.WithCommand(GladiatorActionSchema.CommandNone),
                    updatesTargetDistance: false,
                    context.TargetDistance
                );
            }
        }

        GladiatorAgentAction effectiveAction = action;
        if (action.Command != GladiatorActionSchema.CommandNone && !context.HasValidTarget)
        {
            effectiveAction = effectiveAction.WithCommand(GladiatorActionSchema.CommandNone);
        }

        reward += EvaluateTargetSwitch(context);
        reward += EvaluateStanceSwitch(context);
        reward += EvaluateTargetEngagement(action, context, ref effectiveAction);

        return new GladiatorRewardEvaluation(
            reward,
            requestsBoundaryReset: false,
            effectiveAction,
            updatesTargetDistance: true,
            context.TargetDistance
        );
    }

    private float EvaluateSmoothness(GladiatorAgentAction action)
    {
        float reward = 0f;
        if (_hasPreviousRawAction)
        {
            float moveDelta = Vector2.Distance(_previousRawMove, action.WorldMove);
            reward += moveDelta * _config.actionDelta;
        }

        _previousRawMove = action.WorldMove;
        _hasPreviousRawAction = true;
        return reward;
    }

    private float EvaluateTargetSwitch(GladiatorAgentTacticalContext context)
    {
        if (
            !context.HasValidTarget
            || context.PreviousTargetSlot < 0
            || context.TargetSlot == context.PreviousTargetSlot
        )
        {
            return 0f;
        }

        return _config.targetSwitchPenalty;
    }

    private float EvaluateStanceSwitch(GladiatorAgentTacticalContext context)
    {
        if (context.PreviousStance < 0 || context.Stance == context.PreviousStance)
        {
            return 0f;
        }

        return _config.stanceSwitchPenalty;
    }

    private float EvaluateTargetEngagement(
        GladiatorAgentAction action,
        GladiatorAgentTacticalContext context,
        ref GladiatorAgentAction effectiveAction
    )
    {
        if (!context.HasValidTarget)
        {
            return action.WantsBasicAttack ? _config.invalidAction : 0f;
        }

        float reward = 0f;
        if (context.IsTargetOutOfAttackRange)
        {
            float delta = context.PreviousTargetDistance - context.TargetDistance;
            reward += delta >= 0f ? delta * _config.targetApproach : -delta * _config.targetDrift;

            if (action.WantsBasicAttack)
            {
                reward += _config.outOfRangeAttackPenalty;
                effectiveAction = effectiveAction.WithCommand(GladiatorActionSchema.CommandNone);
            }

            return reward;
        }

        bool keepRangeStance = context.Stance == GladiatorActionSchema.StanceKeepRange;

        if (!context.IsAttackBlocked)
        {
            if (action.WantsBasicAttack)
                reward += _config.attackIntentReward;
            else if (!keepRangeStance)
                reward += _config.inRangeNoAttack;
        }
        else if (context.IsAttackBlocked && action.WantsBasicAttack)
        {
            reward += _config.attackBlockedPenalty;
        }

        if (!keepRangeStance && context.TargetEffectiveRange > 0f)
        {
            float distanceRatio = context.TargetDistance / context.TargetEffectiveRange;
            if (context.Stance == GladiatorActionSchema.StanceNeutral)
            {
                const float neutralThreshold = 0.75f;
                if (distanceRatio < neutralThreshold)
                {
                    float penetration = (neutralThreshold - distanceRatio) / neutralThreshold;
                    reward += penetration * _config.neutralTooClosePenalty;
                }
            }
            else if (context.Stance == GladiatorActionSchema.StancePressure)
            {
                const float pressureThreshold = 0.5f;
                if (distanceRatio < pressureThreshold)
                {
                    float penetration = (pressureThreshold - distanceRatio) / pressureThreshold;
                    reward += penetration * _config.pressureTooClosePenalty;
                }
            }
        }

        return reward;
    }
}
