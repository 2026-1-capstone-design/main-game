using System.Collections.Generic;
using UnityEngine;

// 유닛별 과거 위치를 battle time 기준으로 보관한다.
// 되감기류 효과가 특정 시점의 위치를 조회할 수 있도록 12초 보존 기간을 기본값으로 둔다.
public sealed class BattlePositionHistory
{
    private readonly Dictionary<BattleRuntimeUnit, List<PositionSample>> _samplesByUnit =
        new Dictionary<BattleRuntimeUnit, List<PositionSample>>();
    private readonly float _retentionSeconds;

    public BattlePositionHistory(float retentionSeconds = 12f)
    {
        _retentionSeconds = Mathf.Max(0.1f, retentionSeconds);
    }

    public void Clear()
    {
        _samplesByUnit.Clear();
    }

    public void Record(BattleRuntimeUnit unit, float battleTime)
    {
        if (unit == null)
            return;

        if (!_samplesByUnit.TryGetValue(unit, out List<PositionSample> samples))
        {
            samples = new List<PositionSample>(32);
            _samplesByUnit[unit] = samples;
        }

        samples.Add(new PositionSample(Mathf.Max(0f, battleTime), unit.Position));
        Prune(samples, battleTime - _retentionSeconds);
    }

    public void RecordAll(IReadOnlyList<BattleRuntimeUnit> units, float battleTime)
    {
        if (units == null)
            return;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit != null && !unit.IsCombatDisabled)
                Record(unit, battleTime);
        }
    }

    public bool TryGetPositionAt(BattleRuntimeUnit unit, float battleTime, out Vector3 position)
    {
        position = Vector3.zero;
        if (unit == null || !_samplesByUnit.TryGetValue(unit, out List<PositionSample> samples) || samples.Count == 0)
            return false;

        float targetTime = Mathf.Max(0f, battleTime);
        int idx = SortedSearch.NearestIndex(samples, targetTime, s => s.BattleTime);
        position = samples[idx].Position;
        return true;
    }

    private static void Prune(List<PositionSample> samples, float minBattleTime)
    {
        int removeCount = 0;
        while (removeCount < samples.Count && samples[removeCount].BattleTime < minBattleTime)
            removeCount++;

        if (removeCount > 0)
            samples.RemoveRange(0, removeCount);
    }

    private readonly struct PositionSample
    {
        public float BattleTime { get; }
        public Vector3 Position { get; }

        public PositionSample(float battleTime, Vector3 position)
        {
            BattleTime = battleTime;
            Position = position;
        }
    }
}
