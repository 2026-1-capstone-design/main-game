// PersonalitySO SOT 메타데이터를 일괄 적용
// Assets/Content/Personalities 아래의 지정된 에셋만 수정함
// 순응율 총합은 obedienceRates 배열에서 계산해 저장
// Unity Editor 메뉴에서 1회 실행하는 용도임

using UnityEditor;
using UnityEngine;

public static class ApplyPersonalitySotMetadata
{
    private readonly struct PersonalityMetadata
    {
        public readonly string Path;
        public readonly int SpeechStyle;
        public readonly int[] ObedienceRates;
        public readonly int[] FallbackWeights;

        public PersonalityMetadata(
            string path,
            int speechStyle,
            int[] obedienceRates,
            int[] fallbackWeights
        )
        {
            Path = path;
            SpeechStyle = speechStyle;
            ObedienceRates = obedienceRates;
            FallbackWeights = fallbackWeights;
        }
    }

    private static readonly PersonalityMetadata[] Metadatas =
    {
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 1.asset",
            1,
            new[] { 95, 85, 85, 90, 95, 85, 95, 95 },
            new[] { 20, 20, 25, 35, 90, 40, 5, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 2.asset",
            0,
            new[] { 50, 100, 90, 60, 65, 100, 90, 95 },
            new[] { 5, 100, 40, 10, 20, 50, 0, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 3.asset",
            0,
            new[] { 100, 50, 60, 85, 100, 50, 100, 60 },
            new[] { 50, 5, 5, 20, 100, 5, 50, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 4.asset",
            0,
            new[] { 100, 60, 70, 80, 100, 75, 100, 65 },
            new[] { 35, 5, 5, 15, 100, 5, 30, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 5.asset",
            1,
            new[] { 98, 95, 100, 98, 98, 95, 98, 98 },
            new[] { 25, 20, 60, 40, 100, 25, 10, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 6.asset",
            0,
            new[] { 75, 70, 50, 55, 80, 45, 85, 40 },
            new[] { 40, 35, 10, 10, 100, 10, 25, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 7.asset",
            1,
            new[] { 70, 90, 95, 60, 75, 100, 95, 100 },
            new[] { 10, 30, 40, 5, 100, 30, 5, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 8.asset",
            2,
            new[] { 100, 50, 80, 100, 100, 50, 100, 70 },
            new[] { 50, 0, 5, 40, 100, 0, 40, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 9.asset",
            0,
            new[] { 70, 90, 80, 60, 80, 100, 80, 90 },
            new[] { 10, 35, 25, 5, 100, 35, 5, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 10.asset",
            0,
            new[] { 85, 80, 90, 80, 85, 65, 90, 85 },
            new[] { 30, 35, 40, 20, 90, 20, 25, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 11.asset",
            0,
            new[] { 100, 50, 75, 85, 100, 55, 100, 85 },
            new[] { 40, 5, 10, 15, 100, 5, 30, 0 }
        ),
        new PersonalityMetadata(
            "Assets/Content/Personalities/Personality 12.asset",
            0,
            new[] { 90, 80, 80, 80, 90, 80, 95, 90 },
            new[] { 30, 20, 30, 20, 100, 30, 5, 0 }
        ),
    };

    [MenuItem("Tools/Battle/Apply Personality SOT Metadata")]
    public static void Apply()
    {
        int appliedCount = 0;
        int missingCount = 0;

        foreach (PersonalityMetadata metadata in Metadatas)
        {
            PersonalitySO asset = AssetDatabase.LoadAssetAtPath<PersonalitySO>(metadata.Path);

            if (asset == null)
            {
                Debug.LogError($"[Personality SOT Metadata] Asset not found or invalid: {metadata.Path}");
                missingCount++;
                continue;
            }

            Undo.RecordObject(asset, "Apply Personality SOT Metadata");

            asset.speechStyle = metadata.SpeechStyle;
            asset.obedienceRates = CopyArray(metadata.ObedienceRates);
            asset.fallbackWeights = CopyArray(metadata.FallbackWeights);
            asset.obedienceRateSum = Sum(metadata.ObedienceRates);

            EditorUtility.SetDirty(asset);
            appliedCount++;
        }

        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[Personality SOT Metadata] Applied: {appliedCount}, Missing/Invalid: {missingCount}"
        );
    }

    private static int[] CopyArray(int[] source)
    {
        if (source == null)
            return null;

        int[] copy = new int[source.Length];

        for (int i = 0; i < source.Length; i++)
            copy[i] = source[i];

        return copy;
    }

    private static int Sum(int[] values)
    {
        if (values == null)
            return 0;

        int sum = 0;

        for (int i = 0; i < values.Length; i++)
            sum += values[i];

        return sum;
    }
}
