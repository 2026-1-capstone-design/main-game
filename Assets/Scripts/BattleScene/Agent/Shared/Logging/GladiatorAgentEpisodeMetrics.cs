using System;
using Unity.MLAgents;
using UnityEngine;

public sealed class GladiatorAgentEpisodeMetrics
{
    private const int MetricFlushDecisionSteps = 10000;
    private const string SmoothnessPenaltyKey = "Combat/SmoothnessPenalty";
    private const string AllyCrowdingPenaltyKey = "Combat/AllyCrowdingPenalty";
    private const string DamageDealtRatioKey = "Combat/DamageDealtRatio";
    private const string AttackOpportunityUseRateKey = "Combat/AttackOpportunityUseRate";
    private const string CommandSwitchKey = "Combat/CommandSwitch";
    private const string AnchorSwitchKey = "Combat/AnchorSwitch";
    private const string StrategySwitchKey = "Combat/StrategySwitch";
    private const string PersonalityAlignmentKey = "PersonalityBehavior/PersonalityAlignment";
    private const string CommandMaintenanceKey = "Combat/CommandMaintenance";
    private const string AnchorMaintenanceKey = "Combat/AnchorMaintenance";
    private const string StrategyMaintenanceKey = "Combat/StrategyMaintenance";
    private const string MeanEnemyRangeOffsetKey = "Combat/MeanEnemyRangeOffset";

    private static readonly string[] CommandShareKeys =
    {
        "Combat/CommandShare/Move",
        "Combat/CommandShare/Attack",
        "Combat/CommandShare/Withdraw",
    };

    private static readonly string[] StrategyShareKeys =
    {
        "Combat/StrategyShare/Neutral",
        "Combat/StrategyShare/Pressure",
        "Combat/StrategyShare/KeepRange",
        "Combat/StrategyShare/Retreat",
    };

    private static readonly string[] StrategyRewardKeys =
    {
        "Combat/StrategyReward/Neutral",
        "Combat/StrategyReward/Pressure",
        "Combat/StrategyReward/KeepRange",
        "Combat/StrategyReward/Retreat",
    };

    private readonly int[] _commandShareCounts = new int[GladiatorActionSchema.CommandBranchSize];
    private readonly int[] _strategyShareCounts = new int[GladiatorActionSchema.StrategyBranchSize];
    private readonly int[] _episodeStrategyCounts = new int[GladiatorActionSchema.StrategyBranchSize];
    private readonly int[] _anchorSlotShareCounts = new int[GladiatorActionSchema.AnchorActionBranchSize];
    private readonly float[] _strategyRewardSums = new float[GladiatorActionSchema.StrategyBranchSize];
    private readonly int[] _strategyRewardSamples = new int[GladiatorActionSchema.StrategyBranchSize];

    private float _totalDamageDealt;
    private float _expectedDamageBudget;
    private float _enemyRangeOffsetSum;
    private int _enemyRangeOffsetSamples;
    private int _attackOpportunityCount;
    private int _attackOpportunityUsedCount;
    private int _episodeStepCount;
    private int _localMetricStepCount;
    private int _commandSwitchCount;
    private int _anchorSwitchCount;
    private int _strategySwitchCount;

    private GladiatorCommand? _previousCommand;
    private int _previousAnchorSlot;
    private GladiatorStrategy? _previousStrategy;

    private int _currentCommandRunLength;
    private int _currentAnchorRunLength;
    private int _currentStrategyRunLength;

    private int _commandRunCount;
    private int _anchorRunCount;
    private int _strategyRunCount;

    private int _commandRunLengthSum;
    private int _anchorRunLengthSum;
    private int _strategyRunLengthSum;

    private GladiatorPersonalityBias _personalityBias = GladiatorPersonalityBias.Neutral;

    private bool _flushed;

    public void Reset(GladiatorPersonalityBias personalityBias = default)
    {
        _personalityBias = personalityBias.IsValid ? personalityBias : GladiatorPersonalityBias.Neutral;
        _totalDamageDealt = 0f;
        _expectedDamageBudget = 0f;
        _enemyRangeOffsetSum = 0f;
        _enemyRangeOffsetSamples = 0;
        Array.Clear(_commandShareCounts, 0, _commandShareCounts.Length);
        Array.Clear(_strategyShareCounts, 0, _strategyShareCounts.Length);
        Array.Clear(_episodeStrategyCounts, 0, _episodeStrategyCounts.Length);
        Array.Clear(_anchorSlotShareCounts, 0, _anchorSlotShareCounts.Length);
        Array.Clear(_strategyRewardSums, 0, _strategyRewardSums.Length);
        Array.Clear(_strategyRewardSamples, 0, _strategyRewardSamples.Length);
        _attackOpportunityCount = 0;
        _attackOpportunityUsedCount = 0;
        _episodeStepCount = 0;
        _localMetricStepCount = 0;
        _commandSwitchCount = 0;
        _anchorSwitchCount = 0;
        _strategySwitchCount = 0;

        _previousCommand = null;
        _previousAnchorSlot = -1;
        _previousStrategy = null;
        _currentCommandRunLength = 0;
        _currentAnchorRunLength = 0;
        _currentStrategyRunLength = 0;
        _commandRunCount = 0;
        _anchorRunCount = 0;
        _strategyRunCount = 0;
        _commandRunLengthSum = 0;
        _anchorRunLengthSum = 0;
        _strategyRunLengthSum = 0;
        _flushed = false;
    }

    public void AddDamageDealt(float damage)
    {
        _totalDamageDealt += Mathf.Max(0f, damage);
    }

    public void RecordAction(
        GladiatorAction action,
        GladiatorTacticalContext context,
        float attackDamage,
        float attackSpeed,
        float stepDurationSeconds
    )
    {
        _episodeStepCount++;
        _expectedDamageBudget +=
            Mathf.Max(0f, attackDamage) * Mathf.Max(0f, attackSpeed) * Mathf.Max(0f, stepDurationSeconds);

        RecordLocalActionShare(action);
        UpdateCommandMetrics(action.Command);
        UpdateAnchorMetrics(action.AnchorSlot);
        UpdateStrategyMetrics(action.Strategy);

        bool hasAttackOpportunity = context.HasValidTarget && !context.IsAttackBlocked;
        if (hasAttackOpportunity)
        {
            _attackOpportunityCount++;
            if (action.Command == GladiatorCommand.Attack)
            {
                _attackOpportunityUsedCount++;
            }
        }

        if (context.HasValidTarget)
        {
            _enemyRangeOffsetSum += context.TargetDistance - context.TargetEffectiveRange;
            _enemyRangeOffsetSamples++;
        }
    }

    public void RecordStrategyReward(GladiatorStrategy strategy, float strategyReward)
    {
        int strategyIndex = (int)strategy;
        if (strategyIndex < 0 || strategyIndex >= _strategyRewardSums.Length)
        {
            return;
        }

        _strategyRewardSums[strategyIndex] += strategyReward;
        _strategyRewardSamples[strategyIndex]++;
    }

    public void RecordSmoothnessReward(float smoothnessReward)
    {
        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        recorder.Add(SmoothnessPenaltyKey, Mathf.Max(0f, -smoothnessReward), StatAggregationMethod.Average);
    }

    public void RecordAllyCrowdingReward(float allyCrowdingReward)
    {
        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        recorder.Add(AllyCrowdingPenaltyKey, Mathf.Max(0f, -allyCrowdingReward), StatAggregationMethod.Average);
    }

    public void Flush()
    {
        if (_flushed)
        {
            return;
        }

        _flushed = true;
        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        FlushLocalAverages(recorder);
        FinalizeOpenRuns();

        recorder.Add(
            DamageDealtRatioKey,
            _expectedDamageBudget > 0f ? _totalDamageDealt / _expectedDamageBudget : 0f,
            StatAggregationMethod.Average
        );

        if (_attackOpportunityCount > 0)
        {
            recorder.Add(
                AttackOpportunityUseRateKey,
                (float)_attackOpportunityUsedCount / _attackOpportunityCount,
                StatAggregationMethod.Average
            );
        }

        if (_episodeStepCount > 0)
        {
            float inverseStepCount = 1f / _episodeStepCount;
            recorder.Add(CommandSwitchKey, _commandSwitchCount * inverseStepCount, StatAggregationMethod.Average);
            recorder.Add(AnchorSwitchKey, _anchorSwitchCount * inverseStepCount, StatAggregationMethod.Average);
            recorder.Add(StrategySwitchKey, _strategySwitchCount * inverseStepCount, StatAggregationMethod.Average);
            recorder.Add(
                PersonalityAlignmentKey,
                CalculatePersonalityAlignment(inverseStepCount),
                StatAggregationMethod.Average
            );
        }

        if (_commandRunCount > 0)
        {
            recorder.Add(
                CommandMaintenanceKey,
                (float)_commandRunLengthSum / _commandRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_anchorRunCount > 0)
        {
            recorder.Add(
                AnchorMaintenanceKey,
                (float)_anchorRunLengthSum / _anchorRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_strategyRunCount > 0)
        {
            recorder.Add(
                StrategyMaintenanceKey,
                (float)_strategyRunLengthSum / _strategyRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_enemyRangeOffsetSamples > 0)
        {
            recorder.Add(
                MeanEnemyRangeOffsetKey,
                _enemyRangeOffsetSum / _enemyRangeOffsetSamples,
                StatAggregationMethod.Average
            );
        }

        RecordStrategyRewardMetrics(recorder);
    }

    private void RecordLocalActionShare(GladiatorAction action)
    {
        _commandShareCounts[(int)action.Command]++;
        _strategyShareCounts[(int)action.Strategy]++;
        _episodeStrategyCounts[(int)action.Strategy]++;
        _anchorSlotShareCounts[action.AnchorSlot]++;
        _localMetricStepCount++;

        if (_localMetricStepCount >= MetricFlushDecisionSteps)
        {
            FlushLocalAverages(Academy.Instance.StatsRecorder);
        }
    }

    private void FlushLocalAverages(StatsRecorder recorder)
    {
        if (_localMetricStepCount <= 0)
        {
            return;
        }

        float inverse = 1f / _localMetricStepCount;
        for (int i = 0; i < _commandShareCounts.Length; i++)
        {
            recorder.Add(CommandShareKeys[i], _commandShareCounts[i] * inverse, StatAggregationMethod.Average);
        }

        for (int i = 0; i < _strategyShareCounts.Length; i++)
        {
            recorder.Add(StrategyShareKeys[i], _strategyShareCounts[i] * inverse, StatAggregationMethod.Average);
        }

        Array.Clear(_commandShareCounts, 0, _commandShareCounts.Length);
        Array.Clear(_strategyShareCounts, 0, _strategyShareCounts.Length);
        Array.Clear(_anchorSlotShareCounts, 0, _anchorSlotShareCounts.Length);
        _localMetricStepCount = 0;
    }

    private void RecordStrategyRewardMetrics(StatsRecorder recorder)
    {
        for (int i = 0; i < StrategyRewardKeys.Length; i++)
        {
            if (_strategyRewardSamples[i] <= 0)
            {
                continue;
            }

            recorder.Add(
                StrategyRewardKeys[i],
                _strategyRewardSums[i] / _strategyRewardSamples[i],
                StatAggregationMethod.Average
            );
        }
    }

    private float CalculatePersonalityAlignment(float inverseStepCount)
    {
        float pressureShare = _episodeStrategyCounts[(int)GladiatorStrategy.Pressure] * inverseStepCount;
        float defensiveShare =
            (
                _episodeStrategyCounts[(int)GladiatorStrategy.KeepRange]
                + _episodeStrategyCounts[(int)GladiatorStrategy.Retreat]
            ) * inverseStepCount;
        float aggressiveBias = 1f - _personalityBias.Passiveness;
        float passiveBias = _personalityBias.Passiveness;
        return aggressiveBias * pressureShare + passiveBias * defensiveShare;
    }

    private void UpdateCommandMetrics(GladiatorCommand command)
    {
        if (!_previousCommand.HasValue)
        {
            _previousCommand = command;
            _currentCommandRunLength = 1;
            return;
        }

        if (_previousCommand == command)
        {
            _currentCommandRunLength++;
            return;
        }

        CloseCommandRun();
        _commandSwitchCount++;
        _previousCommand = command;
        _currentCommandRunLength = 1;
    }

    private void UpdateAnchorMetrics(int anchorSlot)
    {
        if (_previousAnchorSlot < 0)
        {
            _previousAnchorSlot = anchorSlot;
            _currentAnchorRunLength = 1;
            return;
        }

        if (_previousAnchorSlot == anchorSlot)
        {
            _currentAnchorRunLength++;
            return;
        }

        CloseAnchorRun();
        _anchorSwitchCount++;
        _previousAnchorSlot = anchorSlot;
        _currentAnchorRunLength = 1;
    }

    private void UpdateStrategyMetrics(GladiatorStrategy strategy)
    {
        if (!_previousStrategy.HasValue)
        {
            _previousStrategy = strategy;
            _currentStrategyRunLength = 1;
            return;
        }

        if (_previousStrategy == strategy)
        {
            _currentStrategyRunLength++;
            return;
        }

        CloseStrategyRun();
        _strategySwitchCount++;
        _previousStrategy = strategy;
        _currentStrategyRunLength = 1;
    }

    private void FinalizeOpenRuns()
    {
        CloseCommandRun();
        CloseAnchorRun();
        CloseStrategyRun();
    }

    private void CloseCommandRun()
    {
        if (_currentCommandRunLength <= 0)
        {
            return;
        }

        _commandRunLengthSum += _currentCommandRunLength;
        _commandRunCount++;
        _currentCommandRunLength = 0;
    }

    private void CloseAnchorRun()
    {
        if (_currentAnchorRunLength <= 0)
        {
            return;
        }

        _anchorRunLengthSum += _currentAnchorRunLength;
        _anchorRunCount++;
        _currentAnchorRunLength = 0;
    }

    private void CloseStrategyRun()
    {
        if (_currentStrategyRunLength <= 0)
        {
            return;
        }

        _strategyRunLengthSum += _currentStrategyRunLength;
        _strategyRunCount++;
        _currentStrategyRunLength = 0;
    }
}
