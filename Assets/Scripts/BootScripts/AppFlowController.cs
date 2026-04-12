using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AppFlowController : SingletonBehaviour<AppFlowController>
{
    [Header("Boot Flow")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private bool autoBootOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool useForcedSeedInEditor = false;
    [SerializeField] private int forcedSeed = 12345;
    [SerializeField] private bool verboseLog = true;

    private SceneLoader _sceneLoader;
    private AudioManager _audioManager;
    private RandomManager _randomManager;
    private SessionManager _sessionManager;
    private ContentDatabaseProvider _contentDatabaseProvider;
    private BattleSessionManager _battleSessionManager;

    private bool _bootInProgress;
    private bool _bootCompleted;

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
