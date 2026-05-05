using UnityEngine;

public readonly struct GladiatorRewardEvaluation
{
    public readonly float Reward;
    public readonly bool RequestsBoundaryReset;
    public readonly GladiatorPolicyAction EffectiveAction;

    public GladiatorRewardEvaluation(
        float reward,
        bool requestsBoundaryReset,
        GladiatorPolicyAction effectiveAction
    )
    {
        Reward = reward;
        RequestsBoundaryReset = requestsBoundaryReset;
        EffectiveAction = effectiveAction;
    }
}

public sealed class GladiatorRewardEvaluator
{
    private readonly GladiatorRewardConfig _config;
    private readonly float _hardBoundaryRadiusMultiplier;
    private readonly GladiatorTacticalRewardShaper _tacticalRewardShaper = new GladiatorTacticalRewardShaper();
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
        GladiatorPolicyAction action,
        GladiatorAgentTacticalContext context,
        GladiatorTacticalFeatures features
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
                    action.WithCommand(GladiatorActionSchema.CommandNone)
                );
            }
        }

        GladiatorPolicyAction effectiveAction = action;
        if (action.Command != GladiatorActionSchema.CommandNone && !context.HasValidTarget)
        {
            effectiveAction = effectiveAction.WithCommand(GladiatorActionSchema.CommandNone);
        }

        reward += EvaluateTargetSwitch(context);
        reward += EvaluateStanceSwitch(context);
        reward += _tacticalRewardShaper.Evaluate(context, action, features);

        return new GladiatorRewardEvaluation(
            reward,
            requestsBoundaryReset: false,
            effectiveAction
        );
    }

    private float EvaluateSmoothness(GladiatorPolicyAction action)
    {
        float reward = 0f;
        if (_hasPreviousRawAction)
        {
            float moveDelta = Vector2.Distance(_previousRawMove, action.RelativeMove);
            reward += moveDelta * _config.actionDelta;
        }

        _previousRawMove = action.RelativeMove;
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

}
