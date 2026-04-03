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

    private System.Random _recruitRng;
    private System.Random _equipmentRng;
    private System.Random _battleEncounterRng;
    private System.Random _battleSimulationRng;

    private bool _initialized;
    private bool _loggedAutoInitWarning;

    public int SessionSeed { get; private set; }
    public bool IsInitialized => _initialized;

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
        if (_initialized) return;

        if (!_loggedAutoInitWarning)
        {
            Debug.LogWarning("[RandomManager] Used before InitializeForNewSession(). Auto-initializing with time-based seed.", this);
            _loggedAutoInitWarning = true;
        }

        InitializeForNewSession();
    }

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