using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SoundManager : MonoBehaviour
{
    private AudioSource _audioSource;

    [Header("UI Settings")]
    [Tooltip("색상을 변경할 버튼의 이미지 컴포넌트를 여기에 연결하세요.")]
    public Image buttonImage;

    public Color soundOnColor = Color.white;
    public Color soundOffColor = new Color(1f, 1f, 1f, 0.5f);

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        if (buttonImage == null)
        {
            Debug.LogWarning("SoundManager: Button Image가 인스펙터에서 할당되지 않았습니다.", this);
        }

        UpdateButtonColor();
    }

    public void ToggleSound()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.ToggleBgmMute();
            UpdateButtonColor();
            ClearSelectedUiObject();
            return;
        }

        if (_audioSource != null)
        {
            _audioSource.mute = !_audioSource.mute;
            UpdateButtonColor();
            ClearSelectedUiObject();
        }
    }

    private static void ClearSelectedUiObject()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void UpdateButtonColor()
    {
        if (buttonImage == null)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            buttonImage.color = audioManager.IsBgmMuted ? soundOffColor : soundOnColor;
            return;
        }

        if (_audioSource != null)
        {
            buttonImage.color = _audioSource.mute ? soundOffColor : soundOnColor;
        }
    }
}
