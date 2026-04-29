using UnityEngine;

public readonly struct GladiatorRewardEvaluation
{
    public readonly float Reward;
    public readonly bool RequestsBoundaryReset;
    public readonly GladiatorAgentAction EffectiveAction;
    public readonly bool UpdatesNearestOpponentDistance;
    public readonly float NearestOpponentDistance;

    public GladiatorRewardEvaluation(
        float reward,
        bool requestsBoundaryReset,
        GladiatorAgentAction effectiveAction,
        bool updatesNearestOpponentDistance,
        float nearestOpponentDistance
    )
    {
        Reward = reward;
        RequestsBoundaryReset = requestsBoundaryReset;
        EffectiveAction = effectiveAction;
        UpdatesNearestOpponentDistance = updatesNearestOpponentDistance;
        NearestOpponentDistance = nearestOpponentDistance;
    }
}

public sealed class GladiatorRewardEvaluator
{
    private readonly GladiatorRewardConfig _config;
    private readonly float _hardBoundaryRadiusMultiplier;
    private Vector2 _previousRawMove;
    private float _previousRawTurn;
    private bool _hasPreviousRawAction;

    public GladiatorRewardEvaluator(GladiatorRewardConfig config, float hardBoundaryRadiusMultiplier)
    {
        _config = config;
        _hardBoundaryRadiusMultiplier = hardBoundaryRadiusMultiplier;
    }

    public void Reset()
    {
        _previousRawMove = Vector2.zero;
        _previousRawTurn = 0f;
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
                    updatesNearestOpponentDistance: false,
                    context.NearestOpponentDistance
                );
            }
        }

        reward += EvaluateEngagement(action, context);

        GladiatorAgentAction effectiveAction = action;
        if (action.Command != GladiatorActionSchema.CommandNone && !context.HasValidTarget)
        {
            reward += _config.invalidAction;
            effectiveAction = effectiveAction.WithCommand(GladiatorActionSchema.CommandNone);
        }

        if (action.IsSpacingMove)
        {
            reward += context.ShouldRetreat ? _config.goodRetreat : _config.badRetreat;
        }

        if (effectiveAction.WantsBasicAttack)
        {
            if (context.ShouldRetreat)
            {
                reward += _config.dangerousAttack;
            }

            if (context.IsTargetOutOfAttackRange)
            {
                reward += _config.chaseTarget;
            }

            if (context.IsAttackBlocked)
            {
                reward += _config.invalidSkill * 0.25f;
            }
        }

        if (effectiveAction.WantsSkill && !context.CanRequestSkill)
        {
            reward += _config.invalidSkill;
            effectiveAction = effectiveAction.WithCommand(GladiatorActionSchema.CommandNone);
        }

        return new GladiatorRewardEvaluation(
            reward,
            requestsBoundaryReset: false,
            effectiveAction,
            updatesNearestOpponentDistance: true,
            context.NearestOpponentDistance
        );
    }

    private float EvaluateSmoothness(GladiatorAgentAction action)
    {
        float reward = 0f;
        if (_hasPreviousRawAction)
        {
            float moveDelta = Vector2.Distance(_previousRawMove, action.LocalMove);
            float turnDelta = Mathf.Abs(_previousRawTurn - action.Turn);
            reward += moveDelta * _config.actionDelta;
            reward += turnDelta * _config.turnDelta;

            if (action.LocalMove.sqrMagnitude <= 0.0001f && Mathf.Abs(action.Turn) > 0.1f)
            {
                reward += Mathf.Abs(action.Turn) * _config.idleJitter;
            }
        }

        _previousRawMove = action.LocalMove;
        _previousRawTurn = action.Turn;
        _hasPreviousRawAction = true;
        return reward;
    }

    private float EvaluateEngagement(GladiatorAgentAction action, GladiatorAgentTacticalContext context)
    {
        float reward = 0f;
        if (
            context.PreviousNearestOpponentDistance < float.MaxValue
            && context.NearestOpponentDistance < float.MaxValue
        )
        {
            float approachDelta = context.PreviousNearestOpponentDistance - context.NearestOpponentDistance;
            if (Mathf.Abs(approachDelta) > 0.0001f)
            {
                reward += approachDelta < 0f && context.ShouldRetreat
                    ? -approachDelta * _config.retreatDistance
                    : approachDelta * _config.approach;
            }
        }

        if (
            !context.IsAttackBlocked
            && !action.IsSpacingMove
            && !action.WantsBasicAttack
            && context.HasAttackableOpponent
        )
        {
            reward += _config.inRangeNoAttack;
        }

        if (
            !context.IsAttackBlocked
            && action.LocalMove.sqrMagnitude <= 0.0001f
            && action.Command == GladiatorActionSchema.CommandNone
            && context.HasLivingOpponent
        )
        {
            reward += _config.disengaged;
        }

        return reward;
    }
}
