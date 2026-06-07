using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct GladiatorRewardEvaluation
{
    public readonly float Reward;
    public readonly float SmoothnessReward;
    public readonly float StrategyReward;
    public readonly float AllyCrowdingReward;

    public GladiatorRewardEvaluation(
        float reward,
        float smoothnessReward,
        float strategyReward,
        float allyCrowdingReward
    )
    {
        Reward = reward;
        SmoothnessReward = smoothnessReward;
        StrategyReward = strategyReward;
        AllyCrowdingReward = allyCrowdingReward;
    }
}

public sealed class GladiatorRewardEvaluator
{
    private const float PersonalityWeightEpsilon = 0.0001f;

    private readonly GladiatorRewardConfig _config;
    private readonly Action<float> _rewardSink;
    private readonly GladiatorTacticalRewardShaper _tacticalRewardShaper;
    private readonly GladiatorPersonalityBias _bias;
    private Vector2 _previousRawMove;
    private bool _hasPreviousRawAction;
    private GladiatorAction _lastAction;
    private GladiatorTacticalContext _lastTacticalContext;
    private bool _hasLastActionContext;

    public GladiatorRewardEvaluator(
        GladiatorRewardConfig config,
        Action<float> rewardSink,
        GladiatorPersonalityBias bias = default
    )
    {
        _config = config;
        // AddReward stays owned by GladiatorAgent, while reward application is coordinated here.
        _rewardSink = rewardSink;
        _tacticalRewardShaper = new GladiatorTacticalRewardShaper(config);
        _bias = bias.IsValid ? bias : GladiatorPersonalityBias.Neutral;
    }

    public void Reset()
    {
        _previousRawMove = Vector2.zero;
        _hasPreviousRawAction = false;
        _hasLastActionContext = false;
    }

    public GladiatorRewardEvaluation EvaluateActionStep(
        GladiatorAction action,
        GladiatorTacticalContext context,
        GladiatorCombatSignalFeatures features,
        GladiatorObservationContext observationContext
    )
    {
        float reward = 0f;
        float smoothnessReward = EvaluateSmoothness(action, context);
        reward += smoothnessReward;

        float allyCrowdingReward = EvaluateAllyCrowding(observationContext.Self, observationContext.Teammates);
        reward += allyCrowdingReward;
        reward += EvaluateCommandSwitch(context);
        reward += EvaluateStrategySwitch(context);
        reward += EvaluateAnchorSwitch(context);
        reward += EvaluateCommitment(context);
        float strategyReward = _tacticalRewardShaper.Evaluate(context, action, features);
        float weightedStrategyReward = strategyReward * StrategyCategoryWeight(action.Strategy);
        reward += weightedStrategyReward;

        RecordLastActionContext(action, context);
        ApplyReward(reward);

        return new GladiatorRewardEvaluation(reward, smoothnessReward, strategyReward, allyCrowdingReward);
    }

    public void RewardDamageTaken(float damage, BattleUnitCombatState selfState)
    {
        float ratio = selfState != null && selfState.MaxHealth > 0f ? Mathf.Max(0f, damage) / selfState.MaxHealth : 0f;

        ApplyReward(
            (ratio * _config.damageTakenRatio + ratio * EvaluateConditionalDamageTakenReward())
                * SurvivalCategoryWeight(_bias)
        );
    }

    public void RewardDeath()
    {
        ApplyReward(_config.death * SurvivalCategoryWeight(_bias));
    }

    public void RewardTerminalSurvival()
    {
        ApplyReward(_config.terminalSurvivalBonus * SurvivalCategoryWeight(_bias));
    }

    public void RewardAttackLanded(BattleRuntimeUnit target, float actualDamage, bool wasKill)
    {
        float ratio =
            target != null && target.State != null && target.State.MaxHealth > 0f
                ? Mathf.Max(0f, actualDamage) / target.State.MaxHealth
                : 0f;

        float reward = (_config.attackLanded + ratio * _config.damageDealtRatio) * DamageCategoryWeight(_bias);
        if (wasKill)
        {
            reward += _config.kill * DamageCategoryWeight(_bias);
        }

        ApplyReward(reward);
    }

    private float EvaluateAllyCrowding(
        BattleUnitCombatState self,
        IReadOnlyList<BattleUnitCombatState> teammates
    )
    {
        float crowdingDistance = Mathf.Max(0f, _config.allyCrowdingDistance);
        if (self == null || teammates == null || crowdingDistance <= 0f)
        {
            return 0f;
        }

        float reward = 0f;
        for (int i = 0; i < teammates.Count; i++)
        {
            BattleUnitCombatState teammate = teammates[i];
            if (teammate == null || teammate.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = teammate.Position - self.Position;
            delta.y = 0f;
            float surfaceDistance = Mathf.Max(0f, delta.magnitude - self.BodyRadius - teammate.BodyRadius);
            if (surfaceDistance >= crowdingDistance)
            {
                continue;
            }

            float proximity = 1f - surfaceDistance / crowdingDistance;
            reward += proximity * _config.allyCrowdingPenalty;
        }

        return reward;
    }

    private void ApplyReward(float reward)
    {
        _rewardSink?.Invoke(reward);
    }

    private float EvaluateSmoothness(GladiatorAction action, GladiatorTacticalContext context)
    {
        float reward = 0f;
        bool isMoveCommandContinuation = IsMoveCommandContinuation(context);
        if (_hasPreviousRawAction && isMoveCommandContinuation)
        {
            float dotted = Vector2.Dot(_previousRawMove, action.RelativeMove);
            reward += -dotted * _config.actionDelta;
        }

        _previousRawMove = action.RelativeMove;
        _hasPreviousRawAction = true;
        return reward;
    }

    private static bool IsMoveCommandContinuation(GladiatorTacticalContext context) =>
        IsMovementCommand(context.PreviousCommand) && IsMovementCommand(context.Command);

    private static bool IsMovementCommand(GladiatorCommand? command) =>
        command == GladiatorCommand.Move || command == GladiatorCommand.Withdraw;

    private float EvaluateCommandSwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousCommand.HasValue || context.Command == context.PreviousCommand)
        {
            return 0f;
        }

        return _config.commandSwitchPenalty;
    }

    private float EvaluateStrategySwitch(GladiatorTacticalContext context)
    {
        if (!context.PreviousStrategy.HasValue || context.Strategy == context.PreviousStrategy)
        {
            return 0f;
        }

        return _config.strategySwitchPenalty;
    }

    private float EvaluateAnchorSwitch(GladiatorTacticalContext context)
    {
        if (context.PreviousTargetSlot < 0 || context.AnchorFallbackApplied)
        {
            return 0f;
        }

        if (context.PreviousTargetSlot == context.TargetSlot)
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

        if (context.CompletedStrategyWindow)
        {
            reward += _config.strategyCommitmentReward;
        }

        if (context.CompletedAnchorWindow)
        {
            reward += _config.anchorCommitmentReward;
        }

        return reward;
    }

    private void RecordLastActionContext(GladiatorAction action, GladiatorTacticalContext context)
    {
        _lastAction = action;
        _lastTacticalContext = context;
        _hasLastActionContext = true;
    }

    private float EvaluateConditionalDamageTakenReward()
    {
        if (!_hasLastActionContext || !_lastTacticalContext.HasValidTarget)
        {
            return 0f;
        }

        switch (_lastAction.Strategy)
        {
            case GladiatorStrategy.Pressure:
                return IsUnsafePressureDamageState() ? _config.pressureUnsafeDamageTakenRatio : 0f;
            case GladiatorStrategy.KeepRange:
                return IsTooCloseKeepRangeDamageState() ? _config.keepRangeTooCloseDamageTakenRatio : 0f;
            default:
                return 0f;
        }
    }

    private float CollectivismIndividualScale(GladiatorPersonalityBias bias)
    {
        float teamWeight = Mathf.Lerp(0.8f, 1.2f, bias.Collectivism);
        float individualWeight = Mathf.Lerp(1.2f, 0.8f, bias.Collectivism);
        float scale = individualWeight / Mathf.Max(PersonalityWeightEpsilon, teamWeight);
        return Mathf.Clamp(scale, _config.personalityCategoryWeightMin, _config.personalityCategoryWeightMax);
    }

    private float DamageCategoryWeight(GladiatorPersonalityBias bias)
    {
        float damageWeight = Mathf.Lerp(1.2f, 0.8f, bias.Passiveness);
        return CollectivismIndividualScale(bias) * damageWeight;
    }

    private float SurvivalCategoryWeight(GladiatorPersonalityBias bias)
    {
        float survivalWeight = Mathf.Lerp(0.8f, 1.2f, bias.Passiveness);
        return CollectivismIndividualScale(bias) * survivalWeight;
    }

    private float StrategyCategoryWeight(GladiatorStrategy strategy)
    {
        switch (strategy)
        {
            case GladiatorStrategy.Pressure:
                return DamageCategoryWeight(_bias);
            case GladiatorStrategy.KeepRange:
            case GladiatorStrategy.Retreat:
                return SurvivalCategoryWeight(_bias);
            default:
                return 1f;
        }
    }

    private bool IsUnsafePressureDamageState()
    {
        return !_lastTacticalContext.IsTargetOutOfAttackRange
            && _lastTacticalContext.SelfThreatToTargetRatio < _lastTacticalContext.TargetThreatToSelfRatio;
    }

    private bool IsTooCloseKeepRangeDamageState()
    {
        float effectiveRange = Mathf.Max(_config.minimumEffectiveRange, _lastTacticalContext.TargetEffectiveRange);
        float distanceRatio = _lastTacticalContext.TargetDistance / effectiveRange;
        return distanceRatio < _config.keepRangeBandMin;
    }
}

public readonly struct GladiatorPersonalityBias
{
    public readonly float Collectivism;
    public readonly float Passiveness;
    public readonly bool IsValid;

    public GladiatorPersonalityBias(float collectivism, float passiveness)
    {
        Collectivism = Mathf.Clamp01(collectivism);
        Passiveness = Mathf.Clamp01(passiveness);
        IsValid = true;
    }

    public static GladiatorPersonalityBias Neutral => new GladiatorPersonalityBias(0.5f, 0.5f);
}
