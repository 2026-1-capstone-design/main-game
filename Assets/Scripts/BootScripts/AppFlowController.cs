using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AppFlowController : SingletonBehaviour<AppFlowController>
{
    [Header("Boot Flow")]
    [SerializeField]
    private string mainSceneName = "TitleScene"; // 부팅 시퀀스가 끝난 뒤 최초로 진입할 타이틀 씬 이름

    [SerializeField]
    private bool autoBootOnStart = true; // 플레이 시작과 동시에 전체 부팅 절차를 자동으로 실행할지 결정.

    [Header("Debug")]
    [SerializeField]
    private bool useForcedSeedInEditor = false; // 에디터 테스트에서 세션 랜덤 결과를 고정 재현할지 결정.

    [SerializeField]
    private int forcedSeed = 12345;

    [SerializeField]
    private bool verboseLog = true;

    private SceneLoader _sceneLoader;
    private AudioManager _audioManager;
    private RandomManager _randomManager;
    private SessionManager _sessionManager;
    private ContentDatabaseProvider _contentDatabaseProvider;
    private BattleSessionManager _battleSessionManager;

    private bool _bootInProgress; // 부팅 시퀀스 중복 실행 막기 위한 플래그
    private bool _bootCompleted; // 이건 부팅 완료 플래그

    public bool IsBootCompleted => _bootCompleted;

    protected override void Awake()
    {
        base.Awake();
        if (!IsPrimaryInstance)
            return;

        DontDestroyOnLoad(gameObject);
        ResolveDependencies();
    }

    private IEnumerator Start()
    {
        if (!IsPrimaryInstance)
            yield break;
        if (!autoBootOnStart)
            yield break;

        yield return BootSequenceRoutine();
    }

    [ContextMenu("Run Boot Sequence")]
    public void RunBootSequenceFromContextMenu()
    {
        if (!Application.isPlaying)
            return;
        if (!IsPrimaryInstance)
            return;
        if (_bootInProgress || _bootCompleted)
            return;

        StartCoroutine(BootSequenceRoutine());
    }

    private void ResolveDependencies()
    {
        _sceneLoader = GetComponent<SceneLoader>();
        _audioManager = GetComponent<AudioManager>();
        _randomManager = GetComponent<RandomManager>();
        _sessionManager = GetComponent<SessionManager>();
        _contentDatabaseProvider = GetComponent<ContentDatabaseProvider>();
        _battleSessionManager = GetComponent<BattleSessionManager>();
    }

    // AFC에 필수 매니저와 메인 씬 이름이 모두 준비됐는지 검사함
    private bool ValidateDependencies()
    {
        bool ok = true;

        if (_sceneLoader == null)
        {
            Debug.LogError("[AppFlowController] SceneLoader is missing on AFC.", this);
            ok = false;
        }

        if (_audioManager == null)
        {
            Debug.LogError("[AppFlowController] AudioManager is missing on AFC.", this);
            ok = false;
        }

        if (_randomManager == null)
        {
            Debug.LogError("[AppFlowController] RandomManager is missing on AFC.", this);
            ok = false;
        }

        if (_sessionManager == null)
        {
            Debug.LogError("[AppFlowController] SessionManager is missing on AFC.", this);
            ok = false;
        }

        if (_contentDatabaseProvider == null)
        {
            Debug.LogError("[AppFlowController] ContentDatabaseProvider is missing on AFC.", this);
            ok = false;
        }

        if (_battleSessionManager == null)
        {
            Debug.LogError("[AppFlowController] BattleSessionManager is missing on AFC.", this);
            ok = false;
        }

        if (string.IsNullOrWhiteSpace(mainSceneName))
        {
            Debug.LogError("[AppFlowController] Main scene name is empty.", this);
            ok = false;
        }

        return ok;
    }

    // DB, 오디오, 세션, 랜덤을 초기화한 뒤 메인 씬으로 넘기는 앱 전체 부팅 절차
    private IEnumerator BootSequenceRoutine()
    {
        if (_bootInProgress || _bootCompleted)
        {
            yield break;
        }

        if (!ValidateDependencies())
        {
            yield break;
        }

        _bootInProgress = true;

        if (verboseLog)
        {
            Debug.Log("[AppFlowController] Boot sequence started.", this);
        }

        _contentDatabaseProvider.Initialize();
        _audioManager.InitializeTemplate();
        _sessionManager.StartNewSession();

        int? seedOverride = null;

#if UNITY_EDITOR
        if (useForcedSeedInEditor)
        {
            seedOverride = forcedSeed;
        }
#endif

        _randomManager.InitializeForNewSession(seedOverride);

        yield return null;

        yield return _sceneLoader.LoadMainSceneAsync(mainSceneName);

        _bootCompleted = true;
        _bootInProgress = false;

        if (verboseLog)
        {
            Debug.Log("[AppFlowController] Boot sequence completed.", this);
        }
    }
}
