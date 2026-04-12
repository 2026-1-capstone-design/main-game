using System;
using Unity.MLAgents;
using UnityEngine;

public sealed class GladiatorAgentEpisodeMetrics
{
    private static readonly GladiatorCommand[] CommandShareValues = (GladiatorCommand[])
        Enum.GetValues(typeof(GladiatorCommand));
    private static readonly GladiatorActionRole[] RoleShareValues = (GladiatorActionRole[])
        Enum.GetValues(typeof(GladiatorActionRole));
    private static readonly GladiatorFightMode[] FightModeShareValues = (GladiatorFightMode[])
        Enum.GetValues(typeof(GladiatorFightMode));
    private static readonly GladiatorAnchorKind[] AnchorKindShareValues = (GladiatorAnchorKind[])
        Enum.GetValues(typeof(GladiatorAnchorKind));

    private float _totalDamageDealt;
    private float _expectedDamageBudget;
    private float _enemyRangeOffsetSum;
    private int _enemyRangeOffsetSamples;
    private readonly float[] _fightModeAnchorRangeOffsetSums = new float[GladiatorActionSchema.FightModeBranchSize];
    private readonly int[] _fightModeAnchorRangeOffsetSamples = new int[GladiatorActionSchema.FightModeBranchSize];
    private int _attackOpportunityCount;
    private int _attackOpportunityUsedCount;
    private int _episodeStepCount;
    private int _commandSwitchCount;
    private int _anchorSwitchCount;
    private int _roleSwitchCount;
    private int _fightModeSwitchCount;

    private GladiatorCommand? _previousCommand;
    private GladiatorAnchorKind? _previousAnchorKind;
    private int _previousAnchorSlot;
    private GladiatorActionRole? _previousRole;
    private GladiatorFightMode? _previousFightMode;

    private int _currentCommandRunLength;
    private int _currentAnchorRunLength;
    private int _currentRoleRunLength;
    private int _currentFightModeRunLength;

    private int _commandRunCount;
    private int _anchorRunCount;
    private int _roleRunCount;
    private int _fightModeRunCount;

    private int _commandRunLengthSum;
    private int _anchorRunLengthSum;
    private int _roleRunLengthSum;
    private int _fightModeRunLengthSum;

    private bool _flushed;

    public void Reset()
    {
        _totalDamageDealt = 0f;
        _expectedDamageBudget = 0f;
        _enemyRangeOffsetSum = 0f;
        _enemyRangeOffsetSamples = 0;
        Array.Clear(_fightModeAnchorRangeOffsetSums, 0, _fightModeAnchorRangeOffsetSums.Length);
        Array.Clear(_fightModeAnchorRangeOffsetSamples, 0, _fightModeAnchorRangeOffsetSamples.Length);
        _attackOpportunityCount = 0;
        _attackOpportunityUsedCount = 0;
        _episodeStepCount = 0;
        _commandSwitchCount = 0;
        _anchorSwitchCount = 0;
        _roleSwitchCount = 0;
        _fightModeSwitchCount = 0;

        _previousCommand = null;
        _previousAnchorKind = null;
        _previousAnchorSlot = -1;
        _previousRole = null;
        _previousFightMode = null;
        _currentCommandRunLength = 0;
        _currentAnchorRunLength = 0;
        _currentRoleRunLength = 0;
        _currentFightModeRunLength = 0;
        _commandRunCount = 0;
        _anchorRunCount = 0;
        _roleRunCount = 0;
        _fightModeRunCount = 0;
        _commandRunLengthSum = 0;
        _anchorRunLengthSum = 0;
        _roleRunLengthSum = 0;
        _fightModeRunLengthSum = 0;
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
        float attackRange,
        float stepDurationSeconds
    )
    {
        _episodeStepCount++;
        _expectedDamageBudget +=
            Mathf.Max(0f, attackDamage) * Mathf.Max(0f, attackSpeed) * Mathf.Max(0f, stepDurationSeconds);

        RecordActionShares(Academy.Instance.StatsRecorder, action);

        UpdateCommandMetrics(action.Command);
        UpdateAnchorMetrics(action.AnchorKind, action.AnchorSlot);
        UpdateRoleMetrics(action.Role);
        UpdateFightModeMetrics(action.FightMode);

        bool hasAttackOpportunity = context.HasValidTarget && !context.IsAttackBlocked;
        if (hasAttackOpportunity)
        {
            _attackOpportunityCount++;
            if (action.WantsBasicAttack)
            {
                _attackOpportunityUsedCount++;
            }
        }

        if (action.AnchorKind == GladiatorAnchorKind.Enemy && context.HasValidTarget)
        {
            _enemyRangeOffsetSum += context.TargetDistance - context.TargetEffectiveRange;
            _enemyRangeOffsetSamples++;
        }

        RecordFightModeAnchorRangeOffset(action.FightMode, context.TargetDistance, attackRange);
    }

    public void RecordSmoothnessReward(float smoothnessReward)
    {
        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        recorder.Add("Combat/SmoothnessPenalty", Mathf.Max(0f, -smoothnessReward), StatAggregationMethod.Average);
    }

    public void Flush()
    {
        if (_flushed)
        {
            return;
        }

        _flushed = true;
        FinalizeOpenRuns();

        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        // 에피소드 동안 낸 총 실제 피해량이, 현재 공격력/공격속도 기준 기대 누적 피해량 대비 어느 정도였는지 나타낸다.
        recorder.Add(
            "Combat/DamageDealtRatio",
            _expectedDamageBudget > 0f ? _totalDamageDealt / _expectedDamageBudget : 0f,
            StatAggregationMethod.Average
        );

        if (_attackOpportunityCount > 0)
        {
            // 공격 가능한 step 중 실제로 공격 command를 사용한 비율이다.
            recorder.Add(
                "Combat/AttackOpportunityUseRate",
                (float)_attackOpportunityUsedCount / _attackOpportunityCount,
                StatAggregationMethod.Average
            );
        }

        if (_episodeStepCount > 0)
        {
            float inverseStepCount = 1f / _episodeStepCount;
            // command를 얼마나 자주 바꾸는지 step 비율로 기록한다.
            recorder.Add("Combat/CommandSwitch", _commandSwitchCount * inverseStepCount, StatAggregationMethod.Average);
            // anchor kind 또는 slot을 얼마나 자주 바꾸는지 step 비율로 기록한다.
            recorder.Add("Combat/AnchorSwitch", _anchorSwitchCount * inverseStepCount, StatAggregationMethod.Average);
            // role을 얼마나 자주 바꾸는지 step 비율로 기록한다.
            recorder.Add("Combat/RoleSwitch", _roleSwitchCount * inverseStepCount, StatAggregationMethod.Average);
            // fight mode를 얼마나 자주 바꾸는지 step 비율로 기록한다.
            recorder.Add(
                "Combat/FightModeSwitch",
                _fightModeSwitchCount * inverseStepCount,
                StatAggregationMethod.Average
            );
        }

        if (_commandRunCount > 0)
        {
            // 한 번 정한 command를 평균 몇 step 연속 유지하는지 나타낸다.
            recorder.Add(
                "Combat/CommandMaintenance",
                (float)_commandRunLengthSum / _commandRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_anchorRunCount > 0)
        {
            // 한 번 정한 anchor를 평균 몇 step 연속 유지하는지 나타낸다.
            recorder.Add(
                "Combat/AnchorMaintenance",
                (float)_anchorRunLengthSum / _anchorRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_roleRunCount > 0)
        {
            // 한 번 정한 role을 평균 몇 step 연속 유지하는지 나타낸다.
            recorder.Add(
                "Combat/RoleMaintenance",
                (float)_roleRunLengthSum / _roleRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_fightModeRunCount > 0)
        {
            // 한 번 정한 fight mode를 평균 몇 step 연속 유지하는지 나타낸다.
            recorder.Add(
                "Combat/FightModeMaintenance",
                (float)_fightModeRunLengthSum / _fightModeRunCount,
                StatAggregationMethod.Average
            );
        }

        if (_enemyRangeOffsetSamples > 0)
        {
            // enemy anchor 기준으로 실제 거리에서 유효 사거리를 뺀 평균값이다. 양수면 주로 사거리 밖, 음수면 사거리 안에 있었다는 뜻이다.
            recorder.Add(
                "Combat/MeanEnemyRangeOffset",
                _enemyRangeOffsetSum / _enemyRangeOffsetSamples,
                StatAggregationMethod.Average
            );
        }

        RecordFightModeAnchorRangeOffsetMetrics(recorder);
    }

    private static void RecordActionShares(StatsRecorder recorder, GladiatorAction action)
    {
        // 각 action branch 선택을 one-hot으로 매 step 기록해 summary 구간의 평균이 100% 점유율이 되도록 한다.
        RecordEnumShare(recorder, "Combat/CommandShare", action.Command, CommandShareValues);
        RecordEnumShare(recorder, "Combat/RoleShare", action.Role, RoleShareValues);
        RecordEnumShare(recorder, "Combat/FightModeShare", action.FightMode, FightModeShareValues);
        RecordEnumShare(recorder, "Combat/AnchorKindShare", action.AnchorKind, AnchorKindShareValues);
    }

    private static void RecordEnumShare<TValue>(
        StatsRecorder recorder,
        string metricPrefix,
        TValue selected,
        TValue[] values
    )
        where TValue : struct
    {
        for (int i = 0; i < values.Length; i++)
        {
            TValue value = values[i];
            recorder.Add($"{metricPrefix}/{value}", value.Equals(selected) ? 1f : 0f, StatAggregationMethod.Average);
        }
    }

    private void RecordFightModeAnchorRangeOffset(GladiatorFightMode fightMode, float anchorDistance, float attackRange)
    {
        int fightModeIndex = (int)fightMode;
        if (
            fightModeIndex < 0
            || fightModeIndex >= _fightModeAnchorRangeOffsetSums.Length
            || anchorDistance >= float.MaxValue
        )
        {
            return;
        }

        _fightModeAnchorRangeOffsetSums[fightModeIndex] += anchorDistance - Mathf.Max(0f, attackRange);
        _fightModeAnchorRangeOffsetSamples[fightModeIndex]++;
    }

    private void RecordFightModeAnchorRangeOffsetMetrics(StatsRecorder recorder)
    {
        for (int i = 0; i < FightModeShareValues.Length; i++)
        {
            GladiatorFightMode fightMode = FightModeShareValues[i];
            int fightModeIndex = (int)fightMode;
            if (
                fightModeIndex < 0
                || fightModeIndex >= _fightModeAnchorRangeOffsetSamples.Length
                || _fightModeAnchorRangeOffsetSamples[fightModeIndex] <= 0
            )
            {
                continue;
            }

            // anchor까지의 실제 거리에서 자신의 공격 사거리를 뺀 평균값이다. 양수면 사거리 밖, 음수면 사거리 안이다.
            recorder.Add(
                $"Combat/FightModeAnchorRangeOffset/{fightMode}",
                _fightModeAnchorRangeOffsetSums[fightModeIndex] / _fightModeAnchorRangeOffsetSamples[fightModeIndex],
                StatAggregationMethod.Average
            );
        }
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

    private void UpdateAnchorMetrics(GladiatorAnchorKind anchorKind, int anchorSlot)
    {
        if (!_previousAnchorKind.HasValue)
        {
            _previousAnchorKind = anchorKind;
            _previousAnchorSlot = anchorSlot;
            _currentAnchorRunLength = 1;
            return;
        }

        if (_previousAnchorKind == anchorKind && _previousAnchorSlot == anchorSlot)
        {
            _currentAnchorRunLength++;
            return;
        }

        CloseAnchorRun();
        _anchorSwitchCount++;
        _previousAnchorKind = anchorKind;
        _previousAnchorSlot = anchorSlot;
        _currentAnchorRunLength = 1;
    }

    private void UpdateRoleMetrics(GladiatorActionRole role)
    {
        if (!_previousRole.HasValue)
        {
            _previousRole = role;
            _currentRoleRunLength = 1;
            return;
        }

        if (_previousRole == role)
        {
            _currentRoleRunLength++;
            return;
        }

        CloseRoleRun();
        _roleSwitchCount++;
        _previousRole = role;
        _currentRoleRunLength = 1;
    }

    private void UpdateFightModeMetrics(GladiatorFightMode fightMode)
    {
        if (!_previousFightMode.HasValue)
        {
            _previousFightMode = fightMode;
            _currentFightModeRunLength = 1;
            return;
        }

        if (_previousFightMode == fightMode)
        {
            _currentFightModeRunLength++;
            return;
        }

        CloseFightModeRun();
        _fightModeSwitchCount++;
        _previousFightMode = fightMode;
        _currentFightModeRunLength = 1;
    }

    private void FinalizeOpenRuns()
    {
        CloseCommandRun();
        CloseAnchorRun();
        CloseRoleRun();
        CloseFightModeRun();
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

    private void CloseRoleRun()
    {
        if (_currentRoleRunLength <= 0)
        {
            return;
        }

        _roleRunLengthSum += _currentRoleRunLength;
        _roleRunCount++;
        _currentRoleRunLength = 0;
    }

    private void CloseFightModeRun()
    {
        if (_currentFightModeRunLength <= 0)
        {
            return;
        }

        _fightModeRunLengthSum += _currentFightModeRunLength;
        _fightModeRunCount++;
        _currentFightModeRunLength = 0;
    }
}
