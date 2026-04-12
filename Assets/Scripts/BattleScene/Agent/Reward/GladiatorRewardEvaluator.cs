using UnityEngine;

public readonly struct GladiatorRewardEvaluation
{
    public readonly float Reward;
    public readonly GladiatorAction EffectiveAction;
    public readonly float SmoothnessReward;

    public GladiatorRewardEvaluation(float reward, GladiatorAction effectiveAction, float smoothnessReward)
    {
        Reward = reward;
        EffectiveAction = effectiveAction;
        SmoothnessReward = smoothnessReward;
    }
}

public sealed class GladiatorRewardEvaluator
{
    private const int MoveOscillationWindowSize = 4;
    private const float MoveOscillationMinSqrMagnitude = 0.01f;
    private const float MoveOscillationOppositeDot = -0.5f;

    private readonly GladiatorRewardConfig _config;
    private readonly GladiatorTacticalRewardShaper _tacticalRewardShaper;
    private readonly Vector2[] _recentRawMoves = new Vector2[MoveOscillationWindowSize];
    private Vector2 _previousRawMove;
    private bool _hasPreviousRawAction;
    private int _recentRawMoveCursor;
    private int _recentRawMoveCount;

    public GladiatorRewardEvaluator(GladiatorRewardConfig config)
    {
        _config = config;
        _tacticalRewardShaper = new GladiatorTacticalRewardShaper(config);
    }

    public void Reset()
    {
        _previousRawMove = Vector2.zero;
        _hasPreviousRawAction = false;
        ResetRawMoveHistory();
    }

    public GladiatorRewardEvaluation EvaluateActionStep(
        GladiatorAction action,
        GladiatorTacticalContext context,
        GladiatorCombatSignalFeatures features
    )
    {
        float reward = _config.step;
        float smoothnessReward = EvaluateSmoothness(action, context);
        reward += smoothnessReward;

        GladiatorAction effectiveAction = action;
        if (action.Command != GladiatorCommand.Move && !context.HasValidTarget)
        {
            effectiveAction = effectiveAction.WithCommand(GladiatorCommand.Move);
        }

        reward += EvaluateCommandSwitch(context);
        reward += EvaluateRoleSwitch(context);
        reward += EvaluateFightModeSwitch(context);
        reward += EvaluateAnchorSwitch(context);
        reward += EvaluateCommitment(context);
        reward += _tacticalRewardShaper.Evaluate(context, action, features);

        return new GladiatorRewardEvaluation(reward, effectiveAction, smoothnessReward);
    }

    private float EvaluateSmoothness(GladiatorAction action, GladiatorTacticalContext context)
    {
        float reward = 0f;
        bool isMoveCommandContinuation = IsMoveCommandContinuation(context);
        if (_hasPreviousRawAction && isMoveCommandContinuation)
        {
            float moveDelta = Vector2.Distance(_previousRawMove, action.RelativeMove);
            reward += moveDelta * _config.actionDelta;

            int repeatedReversals = CountRecentMoveReversals(action.RelativeMove);
            if (repeatedReversals > 1)
            {
                reward += moveDelta * _config.actionDelta * (repeatedReversals - 1);
            }
        }

        _previousRawMove = action.RelativeMove;
        _hasPreviousRawAction = true;
        UpdateRawMoveHistory(action.RelativeMove, context, isMoveCommandContinuation);
        return reward;
    }

    private static bool IsMoveCommandContinuation(GladiatorTacticalContext context) =>
        context.PreviousCommand == GladiatorCommand.Move && context.Command == GladiatorCommand.Move;

    private int CountRecentMoveReversals(Vector2 currentMove)
    {
        int reversalCount = 0;
        Vector2 nextMove = currentMove;
        int pairsToCheck = Mathf.Min(_recentRawMoveCount, MoveOscillationWindowSize - 1);

        for (int index = 0; index < pairsToCheck; index++)
        {
            int previousIndex =
                (_recentRawMoveCursor - 1 - index + MoveOscillationWindowSize) % MoveOscillationWindowSize;
            Vector2 previousMove = _recentRawMoves[previousIndex];
            if (IsOppositeMove(previousMove, nextMove))
            {
                reversalCount++;
            }

            nextMove = previousMove;
        }

        return reversalCount;
    }

    private void UpdateRawMoveHistory(Vector2 rawMove, GladiatorTacticalContext context, bool isMoveCommandContinuation)
    {
        if (context.Command != GladiatorCommand.Move)
        {
            ResetRawMoveHistory();
            return;
        }

        if (!isMoveCommandContinuation)
        {
            ResetRawMoveHistory();
        }

        RecordRawMove(rawMove);
    }

    private void ResetRawMoveHistory()
    {
        _recentRawMoveCursor = 0;
        _recentRawMoveCount = 0;
    }

    private static bool IsOppositeMove(Vector2 previousMove, Vector2 currentMove)
    {
        if (
            previousMove.sqrMagnitude < MoveOscillationMinSqrMagnitude
            || currentMove.sqrMagnitude < MoveOscillationMinSqrMagnitude
        )
        {
            return false;
        }

        return Vector2.Dot(previousMove.normalized, currentMove.normalized) <= MoveOscillationOppositeDot;
    }

    private void RecordRawMove(Vector2 rawMove)
    {
        _recentRawMoves[_recentRawMoveCursor] = rawMove;
        _recentRawMoveCursor = (_recentRawMoveCursor + 1) % MoveOscillationWindowSize;
        _recentRawMoveCount = Mathf.Min(_recentRawMoveCount + 1, MoveOscillationWindowSize);
    }

    private float EvaluateCommandSwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousCommand.HasValue || context.Command == context.PreviousCommand)
        {
            return 0f;
        }

        return _config.commandSwitchPenalty;
    }

    private float EvaluateRoleSwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousRole.HasValue || context.Role == context.PreviousRole)
        {
            return 0f;
        }

        return _config.roleSwitchPenalty;
    }

    private float EvaluateFightModeSwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousFightMode.HasValue || context.FightMode == context.PreviousFightMode)
        {
            return 0f;
        }

        return _config.fightModeSwitchPenalty;
    }

    private float EvaluateAnchorSwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousAnchorKind.HasValue || context.AnchorFallbackApplied)
        {
            return 0f;
        }

        if (context.PreviousAnchorKind == context.AnchorKind && context.PreviousTargetSlot == context.TargetSlot)
        {
            return 0f;
        }

        return _config.anchorSwitchPenalty;
    }

    private float EvaluateCommitment(GladiatorTacticalContext context)
    {
        float reward = 0f;
        if (context.CompletedCommandWindow)
        {
            reward += _config.commandCommitmentReward;
        }

        if (context.CompletedRoleWindow)
        {
            reward += _config.roleCommitmentReward;
        }

        if (context.CompletedFightModeWindow)
        {
            reward += _config.fightModeCommitmentReward;
        }

        if (context.CompletedAnchorWindow)
        {
            reward += _config.anchorCommitmentReward;
        }

        return reward;
    }
}
