using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleSceneUIManager : MonoBehaviour
{
    [Header("Title Scene")]
    [SerializeField]
    private string mainSceneName = "MainScene"; // New Game 버튼에서 이동할 메인 씬 이름

    [Header("Buttons")]
    [SerializeField]
    private Button newGameButton;

    [SerializeField]
    private Button loadGameButton;

    [SerializeField]
    private Button settingsButton;

    [SerializeField]
    private Button quitButton;

    [Header("Settings Modal")]
    [SerializeField]
    private GameObject settingsModalRoot;

    [SerializeField]
    private Button settingsCloseButton;

    [SerializeField]
    private Dropdown languageDropdown;

    [SerializeField]
    private Slider bgmVolumeSlider;

    [SerializeField]
    private Slider sfxVolumeSlider;

    [SerializeField]
    private Slider brightnessSlider;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private SceneLoader _sceneLoader;
    private bool _initialized; // 버튼 이벤트 중복 바인딩 방지
    private bool _isNavigating; // 씬 이동 중 중복 클릭 방지
    private Button _settingsBackdropButton;

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _sceneLoader = SceneLoader.Instance;

        BindButton(newGameButton, OnNewGameClicked);
        BindButton(loadGameButton, OnLoadGameClicked);
        BindButton(settingsButton, OnSettingsClicked);
        BindButton(settingsCloseButton, OnCloseSettingsClicked);
        BindButton(quitButton, OnQuitClicked);

        CacheSettingsControls();
        BindSettingsControls();
        SyncSettingsControlsFromGlobalValues();

        if (settingsModalRoot != null)
        {
            settingsModalRoot.SetActive(false);
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Title scene initialized.", this);
        }
    }

    // 새 게임 시작 버튼: 메인 씬으로 진입
    private void OnNewGameClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (_sceneLoader == null)
        {
            Debug.LogError("[TitleSceneUIManager] SceneLoader.Instance is null.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogError("[TitleSceneUIManager] mainSceneName is empty.", this);
            return;
        }

        _isNavigating = true;

        bool started = _sceneLoader.TryLoadMainScene(mainSceneName);
        if (!started)
        {
            _isNavigating = false;
            Debug.LogWarning("[TitleSceneUIManager] Failed to start MainScene load.", this);
            return;
        }

        if (verboseLog)
        {
            Debug.Log($"[TitleSceneUIManager] New game clicked. Loading scene: {mainSceneName}", this);
        }
    }

    // 로드 게임 버튼: 저장 데이터 연동 시 구현 예정
    private void OnLoadGameClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        Debug.Log("[TitleSceneUIManager] Load game button clicked. Not implemented yet.", this);
    }

    // 설정 버튼: 옵션 UI 연동 시 구현 예정
    private void OnSettingsClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (settingsModalRoot == null)
        {
            Debug.LogError("[TitleSceneUIManager] settingsModalRoot is null.", this);
            return;
        }

        settingsModalRoot.SetActive(true);
        SyncSettingsControlsFromGlobalValues();

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Settings modal opened.", this);
        }
    }

    // 모달 바깥의 어두운 배경을 눌러도 같은 닫기 흐름을 타게 한다.
    private void OnCloseSettingsClicked()
    {
        if (settingsModalRoot == null)
        {
            Debug.LogError("[TitleSceneUIManager] settingsModalRoot is null.", this);
            return;
        }

        settingsModalRoot.SetActive(false);

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Settings modal closed.", this);
        }
    }

    // 언어 변경값을 전역 설정에 저장한다.
    private void OnLanguageChanged(int selectedIndex)
    {
        GameSettings.SetLanguage((GameLanguage)selectedIndex);
    }

    // BGM 슬라이더 변화값을 전역 설정과 오디오에 반영한다.
    private void OnBgmVolumeChanged(float value)
    {
        GameSettings.SetBgmVolume(value);
        ApplyAudioSettings();
    }

    // SFX 슬라이더 변화값을 전역 설정과 오디오에 반영한다.
    private void OnSfxVolumeChanged(float value)
    {
        GameSettings.SetSfxVolume(value);
        ApplyAudioSettings();
    }

    // 밝기 슬라이더 변화값을 전역 설정과 현재 씬 조명에 반영한다.
    private void OnBrightnessChanged(float value)
    {
        GameSettings.SetBrightness(value);
        GameSettings.ApplyBrightnessToCurrentScene();
    }

    // 종료 버튼: 에디터에서는 플레이 모드 종료, 빌드에서는 앱 종료
    private void OnQuitClicked()
    {
        if (_isNavigating)
        {
            return;
        }

        if (verboseLog)
        {
            Debug.Log("[TitleSceneUIManager] Quit button clicked.", this);
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.AddListener(action);
    }

    private void CacheSettingsControls()
    {
        if (settingsModalRoot == null)
        {
            return;
        }

        Transform modalRootTransform = settingsModalRoot.transform;

        if (languageDropdown == null)
        {
            languageDropdown = FindChildComponent<Dropdown>(modalRootTransform, "LanguageDropdown");
        }

        if (bgmVolumeSlider == null)
        {
            bgmVolumeSlider = FindChildComponent<Slider>(modalRootTransform, "BgmSlider");
        }

        if (sfxVolumeSlider == null)
        {
            sfxVolumeSlider = FindChildComponent<Slider>(modalRootTransform, "SfxSlider");
        }

        if (brightnessSlider == null)
        {
            brightnessSlider = FindChildComponent<Slider>(modalRootTransform, "BrightnessSlider");
        }

        Transform backdropTransform = FindChildTransform(modalRootTransform, "DimBackground");
        if (backdropTransform != null)
        {
            Image backdropImage = backdropTransform.GetComponent<Image>();
            _settingsBackdropButton = backdropTransform.GetComponent<Button>();

            if (_settingsBackdropButton == null)
            {
                _settingsBackdropButton = backdropTransform.gameObject.AddComponent<Button>();
            }

            _settingsBackdropButton.transition = Selectable.Transition.None;
            _settingsBackdropButton.targetGraphic = backdropImage;
        }
    }

    // 모달 안의 실제 UI 컴포넌트를 씬 계층 이름으로 찾아 둔다.
    private void BindSettingsControls()
    {
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.onValueChanged.RemoveListener(OnBrightnessChanged);
            brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        }

        if (_settingsBackdropButton != null)
        {
            _settingsBackdropButton.onClick.RemoveListener(OnCloseSettingsClicked);
            _settingsBackdropButton.onClick.AddListener(OnCloseSettingsClicked);
        }
    }

    // 저장된 전역 설정값을 현재 UI 상태로 되돌린다.
    private void SyncSettingsControlsFromGlobalValues()
    {
        if (languageDropdown != null)
        {
            languageDropdown.SetValueWithoutNotify((int)GameSettings.Language);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        }

        if (brightnessSlider != null)
        {
            brightnessSlider.SetValueWithoutNotify(GameSettings.Brightness);
        }
    }

    // 자식 계층을 이름으로 재귀 탐색한다.
    private static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nestedChild = FindChildTransform(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    // 이름으로 찾은 자식에서 원하는 컴포넌트를 꺼낸다.
    private static T FindChildComponent<T>(Transform parent, string childName)
        where T : Component
    {
        Transform child = FindChildTransform(parent, childName);
        if (child == null)
        {
            return null;
        }

        return child.GetComponent<T>();
    }

    // 오디오 매니저가 있으면 전역 볼륨값을 다시 적용한다.
    private void ApplyAudioSettings()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return;
        }

        audioManager.ApplyFromGlobalSettings();
    }
}
