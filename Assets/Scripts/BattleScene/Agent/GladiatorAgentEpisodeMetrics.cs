using Unity.MLAgents;

public sealed class GladiatorAgentEpisodeMetrics
{
    private float _damageDealtRatio;
    private float _damageTakenRatio;
    private float _targetDistanceSum;
    private int _targetDistanceSamples;
    private int _attackIntentCount;
    private int _attackLandedCount;
    private int _attackOpportunityCount;
    private int _attackOpportunityUsedCount;
    private int _inRangeNoAttackCount;
    private int _outOfRangeAttackCount;
    private int _invalidTargetAttackCount;
    private int _targetSwitchCount;
    private float _finalSelfHealthRatio;
    private float _finalEnemyHealthRatio;
    private bool _hasFinalHealthRatios;
    private bool _flushed;

    public void Reset()
    {
        _damageDealtRatio = 0f;
        _damageTakenRatio = 0f;
        _targetDistanceSum = 0f;
        _targetDistanceSamples = 0;
        _attackIntentCount = 0;
        _attackLandedCount = 0;
        _attackOpportunityCount = 0;
        _attackOpportunityUsedCount = 0;
        _inRangeNoAttackCount = 0;
        _outOfRangeAttackCount = 0;
        _invalidTargetAttackCount = 0;
        _targetSwitchCount = 0;
        _finalSelfHealthRatio = 0f;
        _finalEnemyHealthRatio = 0f;
        _hasFinalHealthRatios = false;
        _flushed = false;
    }

    public void AddDamageDealtRatio(float ratio)
    {
        _damageDealtRatio += ratio;
    }

    public void AddDamageTakenRatio(float ratio)
    {
        _damageTakenRatio += ratio;
    }

    public void RecordAttackLanded()
    {
        _attackLandedCount++;
    }

    public void RecordAction(GladiatorAgentAction action, GladiatorAgentTacticalContext context)
    {
        if (context.HasValidTarget && context.TargetDistance < float.MaxValue)
        {
            _targetDistanceSum += context.TargetDistance;
            _targetDistanceSamples++;
        }

        if (
            context.HasValidTarget
            && context.PreviousTargetSlot >= 0
            && context.TargetSlot != context.PreviousTargetSlot
        )
        {
            _targetSwitchCount++;
        }

        bool hasAttackOpportunity =
            context.HasValidTarget && !context.IsTargetOutOfAttackRange && !context.IsAttackBlocked;

        if (hasAttackOpportunity)
        {
            _attackOpportunityCount++;
            if (action.WantsBasicAttack)
            {
                _attackOpportunityUsedCount++;
            }
        }

        if (!action.WantsBasicAttack)
        {
            if (hasAttackOpportunity)
            {
                _inRangeNoAttackCount++;
            }

            return;
        }

        if (!context.HasValidTarget)
        {
            _invalidTargetAttackCount++;
            return;
        }

        if (context.IsTargetOutOfAttackRange)
        {
            _outOfRangeAttackCount++;
            return;
        }

        if (!context.IsAttackBlocked)
        {
            _attackIntentCount++;
        }
    }

    public void RecordFinalHealthRatios(float selfHealthRatio, float enemyHealthRatio)
    {
        _finalSelfHealthRatio = selfHealthRatio;
        _finalEnemyHealthRatio = enemyHealthRatio;
        _hasFinalHealthRatios = true;
    }

    public void Flush()
    {
        if (_flushed)
        {
            return;
        }

        _flushed = true;
        StatsRecorder recorder = Academy.Instance.StatsRecorder;
        // 이 검투사가 적에게 입힌 총 피해량. 적 최대 체력 기준으로 환산.
        recorder.Add("Combat/DamageDealtRatio", _damageDealtRatio, StatAggregationMethod.Average);
        // 이 검투사가 받은 총 피해량. 자기 최대 체력 기준으로 환산.
        recorder.Add("Combat/DamageTakenRatio", _damageTakenRatio, StatAggregationMethod.Average);
        // 공격할 수 있는 상황에서 기본공격을 선택한 횟수
        recorder.Add("Combat/AttackIntent", _attackIntentCount, StatAggregationMethod.Average);
        // 기본공격이 실제 피해로 이어진 횟수
        recorder.Add("Combat/AttackLanded", _attackLandedCount, StatAggregationMethod.Average);
        // 적이 사거리 안에 있고 공격이 막히지 않아 기본공격을 할 수 있었던 기회 수
        recorder.Add("Combat/AttackOpportunity", _attackOpportunityCount, StatAggregationMethod.Average);
        if (_attackOpportunityCount > 0)
        {
            // 기본공격 기회 중 실제로 기본공격을 선택한 비율 1에 가까울수록 기회를 덜 놓친다.
            recorder.Add(
                "Combat/AttackOpportunityUseRate",
                (float)_attackOpportunityUsedCount / _attackOpportunityCount,
                StatAggregationMethod.Average
            );
        }

        // 기본공격을 할 수 있었는데도 공격하지 않은 횟수
        recorder.Add("Combat/InRangeNoAttack", _inRangeNoAttackCount, StatAggregationMethod.Average);
        // 적이 사거리 밖에 있는데 기본공격을 선택한 횟수
        recorder.Add("Combat/OutOfRangeAttack", _outOfRangeAttackCount, StatAggregationMethod.Average);
        // 공격할 수 있는 적이 없는데 기본공격을 선택한 횟수
        recorder.Add("Combat/InvalidTargetAttack", _invalidTargetAttackCount, StatAggregationMethod.Average);
        // 한 경기 안에서 선택한 적 타겟을 바꾼 횟수
        recorder.Add("Combat/TargetSwitch", _targetSwitchCount, StatAggregationMethod.Average);
        if (_hasFinalHealthRatios)
        {
            // 경기가 끝났을 때 이 검투사에게 남아 있던 체력 비율
            recorder.Add("Combat/FinalSelfHealthRatio", _finalSelfHealthRatio, StatAggregationMethod.Average);
            // 경기가 끝났을 때 적들에게 남아 있던 평균 체력 비율
            recorder.Add("Combat/FinalEnemyHealthRatio", _finalEnemyHealthRatio, StatAggregationMethod.Average);
        }

        if (_targetDistanceSamples > 0)
        {
            // 경기 중 선택한 적과의 평균 거리
            recorder.Add(
                "Combat/MeanTargetDistance",
                _targetDistanceSum / _targetDistanceSamples,
                StatAggregationMethod.Average
            );
        }
    }
}
