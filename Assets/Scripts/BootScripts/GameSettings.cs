using UnityEngine;

public enum GameLanguage
{
    Korean = 0,
    English = 1,
}

public static class GameSettings
{
    // PlayerPrefs에 저장할 전역 설정 키들이다.
    private const string LanguageKey = "GameSettings.Language";
    private const string BgmVolumeKey = "GameSettings.BgmVolume";
    private const string SfxVolumeKey = "GameSettings.SfxVolume";
    private const string BrightnessKey = "GameSettings.Brightness";

    public static GameLanguage Language { get; private set; } = GameLanguage.Korean;
    public static float BgmVolume { get; private set; } = 1f;
    public static float SfxVolume { get; private set; } = 1f;
    public static float Brightness { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeSceneLoad()
    {
        // 씬이 시작되기 전에 저장된 설정을 먼저 읽어 둔다.
        Load();
    }

    // 저장된 전역 설정값을 메모리로 다시 불러온다.
    public static void Load()
    {
        Language = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey, (int)GameLanguage.Korean), 0, 1);
        BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        Brightness = Mathf.Clamp01(PlayerPrefs.GetFloat(BrightnessKey, 1f));
    }

    // 언어 변경값을 전역 상태와 저장소에 동시에 반영한다.
    public static void SetLanguage(GameLanguage value)
    {
        Language = (GameLanguage)Mathf.Clamp((int)value, 0, 1);
        PlayerPrefs.SetInt(LanguageKey, (int)Language);
        PlayerPrefs.Save();
    }

    // BGM 볼륨 변경값을 저장한다.
    public static void SetBgmVolume(float value)
    {
        BgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
        PlayerPrefs.Save();
    }

    // SFX 볼륨 변경값을 저장한다.
    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();
    }

    // 밝기 변경값을 저장한다.
    public static void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
        PlayerPrefs.Save();
    }

    // 현재 씬의 기본 조명을 저장된 밝기 값으로 다시 맞춘다.
    public static void ApplyBrightnessToCurrentScene()
    {
        RenderSettings.ambientIntensity = Mathf.Lerp(0.5f, 1.5f, Brightness);

        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
        {
            DynamicGI.UpdateEnvironment();
        }
    }
}
