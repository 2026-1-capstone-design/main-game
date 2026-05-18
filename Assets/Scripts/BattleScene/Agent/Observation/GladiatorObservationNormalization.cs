using UnityEngine;

// 관측과 전술 feature가 공유하는 스케일 정규화 규칙을 모아둔다.
public static class GladiatorObservationNormalization
{
    private const float Epsilon = 1e-6f;

    public static float NormalizeSignedByArenaRadius(float value, float arenaRadius)
    {
        if (!IsValidReference(arenaRadius))
        {
            return 0f;
        }

        return Mathf.Clamp(value / arenaRadius, -1f, 1f);
    }

    public static float NormalizeByArenaRadius(float value, float arenaRadius)
    {
        return NormalizePositiveByReference(value, arenaRadius);
    }

    public static float NormalizeDistanceByArenaRadius(float distance, float arenaRadius)
    {
        if (distance >= float.MaxValue)
        {
            return 0f;
        }

        return NormalizePositiveByReference(distance, arenaRadius);
    }

    public static float NormalizePositiveByReference(float value, float reference)
    {
        return IsValidReference(reference) ? Mathf.Clamp01(Mathf.Max(0f, value) / reference) : 0f;
    }

    public static bool IsValidReference(float value)
    {
        return value > Epsilon && value < float.MaxValue;
    }
}
