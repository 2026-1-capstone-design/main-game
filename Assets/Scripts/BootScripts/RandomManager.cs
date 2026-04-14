using System;
using UnityEngine;

public enum RandomStreamType
{
    Recruit = 0,
    Equipment = 1,
    BattleEncounter = 2,
    BattleSimulation = 3
}

[DisallowMultipleComponent]
public sealed class RandomManager : SingletonBehaviour<RandomManager>
{
    [SerializeField] private bool verboseLog = true;

    private System.Random _recruitRng;      // 검투사 생성 전용 난수 스트림
    private System.Random _equipmentRng;       // 장비 난수
    private System.Random _battleEncounterRng;      //전투 상대 난수
    private System.Random _battleSimulationRng;     // 실제 전투 난수

    private bool _initialized;
    private bool _loggedAutoInitWarning;

    public int SessionSeed { get; private set; }        // 현재 세션의 기준 시드. 모든 난수 스트림은 이 값을 바탕으로 파생됨
    public bool IsInitialized => _initialized;          // 세션용 난수 스트림 초기화 완료 여부

    // 새 세션의 기준 시드를 정하고
    // 모든 시스템 전용 RNG를 서로 다른 salt로 분리 생성
    public void InitializeForNewSession(int? forcedSeed = null)
    {
        SessionSeed = forcedSeed ?? unchecked((int)DateTime.UtcNow.Ticks);

        _recruitRng = new System.Random(HashSeed(SessionSeed, 101));
        _equipmentRng = new System.Random(HashSeed(SessionSeed, 202));
        _battleEncounterRng = new System.Random(HashSeed(SessionSeed, 303));
        _battleSimulationRng = new System.Random(HashSeed(SessionSeed, 404));

        _initialized = true;
        _loggedAutoInitWarning = false;

        if (verboseLog)
        {
            Debug.Log($"[RandomManager] Initialized. SessionSeed = {SessionSeed}", this);
        }
    }

    public int NextInt(RandomStreamType stream, int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        return GetRng(stream).Next(minInclusive, maxExclusive);
    }

    public float NextFloat01(RandomStreamType stream)
    {
        return (float)GetRng(stream).NextDouble();
    }

    public float NextFloatRange(RandomStreamType stream, float minInclusive, float maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        float t = NextFloat01(stream);
        return Mathf.Lerp(minInclusive, maxInclusive, t);
    }

    public bool Chance(RandomStreamType stream, float probability01)
    {
        probability01 = Mathf.Clamp01(probability01);
        return NextFloat01(stream) < probability01;
    }

    public float NextGaussian(RandomStreamType stream, float mean, float standardDeviation)
    {
        if (standardDeviation <= 0f)
        {
            return mean;
        }

        System.Random rng = GetRng(stream);

        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        return mean + (float)(standardDeviation * randStdNormal);
    }

    public float NextClampedGaussian(RandomStreamType stream, float minInclusive, float maxInclusive)
    {
        if (maxInclusive < minInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        float range = maxInclusive - minInclusive;
        float mean = (minInclusive + maxInclusive) * 0.5f;
        float standardDeviation = range / 5f;

        float value = NextGaussian(stream, mean, standardDeviation);
        return Mathf.Clamp(value, minInclusive, maxInclusive);
    }

    // 여기서 실제로 요청된 시스템에 맞는 전용 RNG를 반환함
    private System.Random GetRng(RandomStreamType stream)
    {
        EnsureInitialized();

        return stream switch
        {
            RandomStreamType.Recruit => _recruitRng,
            RandomStreamType.Equipment => _equipmentRng,
            RandomStreamType.BattleEncounter => _battleEncounterRng,
            RandomStreamType.BattleSimulation => _battleSimulationRng,
            _ => _recruitRng
        };
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        if (!_loggedAutoInitWarning)
        {
            Debug.LogWarning("[RandomManager] Used before InitializeForNewSession(). Auto-initializing with time-based seed.", this);
            _loggedAutoInitWarning = true;
        }

        InitializeForNewSession();
    }

    // 공통 세션 시드에서 스트림별로 다른 시드를 파생시키는 내부 해시 함수
    private static int HashSeed(int baseSeed, int salt)
    {
        unchecked
        {
            int hash = baseSeed;
            hash = (hash * 397) ^ salt;
            hash = (hash * 397) ^ 0x2D2816FE;
            return hash;
        }
    }
}
