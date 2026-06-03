using UnityEngine;

public class SceneBgmPlayer : MonoBehaviour
{
    public AudioClip sceneBgm;

    private void Start()
    {
        // 씬이 시작될 때 AudioManager가 존재하고, 클립이 비어있지 않다면 BGM을 재생합니다.
        if (AudioManager.Instance != null && sceneBgm != null)
        {
            AudioManager.Instance.PlayBgm(sceneBgm);
        }
    }
}
