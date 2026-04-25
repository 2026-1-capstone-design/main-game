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

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    private SceneLoader _sceneLoader;
    private bool _initialized; // 버튼 이벤트 중복 바인딩 방지
    private bool _isNavigating; // 씬 이동 중 중복 클릭 방지

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
        BindButton(quitButton, OnQuitClicked);

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

        Debug.Log("[TitleSceneUIManager] Settings button clicked. Not implemented yet.", this);
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
}
