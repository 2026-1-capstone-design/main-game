using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneLoader : SingletonBehaviour<SceneLoader>
{
    [SerializeField] private bool verboseLog = true;

    private bool _isLoading;        // 현재 씬 로드가 진행 중인지 나타내는 플래그. 중복 로드를 막음

    public bool IsLoading => _isLoading;

    public IEnumerator LoadMainSceneAsync(string sceneName)
    {
        yield return LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    public bool TryLoadMainScene(string sceneName)
    {
        return TryLoadScene(sceneName, LoadSceneMode.Single);
    }

    // SceneLoader 자기 자신이 코루틴 호스트가 되어 씬 로드를 시작함
    public bool TryLoadScene(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
    {
        if (_isLoading)
        {
            if (verboseLog)
            {
                Debug.LogWarning($"[SceneLoader] Already loading a scene. Ignoring request: {sceneName}", this);
            }

            return false;
        }

        StartCoroutine(LoadSceneAsync(sceneName, loadMode));
        return true;
    }
    // 실제 비동기 씬 로드 본체
    public IEnumerator LoadSceneAsync(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single)
    {
        if (_isLoading)
        {
            Debug.LogWarning($"[SceneLoader] Already loading a scene. Ignoring request: {sceneName}", this);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneLoader] Scene name is null or empty.", this);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] Scene '{sceneName}' is not in Build Settings or cannot be loaded.", this);
            yield break;
        }

        _isLoading = true;

        if (verboseLog)
        {
            Debug.Log($"[SceneLoader] Loading scene: {sceneName}", this);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, loadMode);

        if (operation == null)
        {
            _isLoading = false;
            Debug.LogError($"[SceneLoader] Failed to create AsyncOperation for scene '{sceneName}'.", this);
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        _isLoading = false;

        if (verboseLog)
        {
            Debug.Log($"[SceneLoader] Scene loaded: {sceneName}", this);
        }
    }
}
