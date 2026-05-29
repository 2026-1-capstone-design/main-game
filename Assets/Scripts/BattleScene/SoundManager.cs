using UnityEngine;
using UnityEngine.UI; // UI 컴포넌트를 제어하기 위해 반드시 추가해야 합니다.

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    private AudioSource _audioSource;

    [Header("UI Settings")]
    [Tooltip("색상을 변경할 버튼의 이미지 컴포넌트를 여기에 연결하세요.")]
    public Image buttonImage;

    // 인스펙터에서 원하는 색상으로 직접 수정할 수 있습니다.
    public Color soundOnColor = Color.white; // 소리가 켜져 있을 때 (기본 흰색/불투명)
    public Color soundOffColor = new Color(1f, 1f, 1f, 0.5f); // 소리가 꺼져 있을 때 (투명도 50%의 흰색)

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        // 시작할 때 현재 소리 상태에 맞춰 버튼 색상을 초기화합니다.
        UpdateButtonColor();
    }

    public void ToggleSound()
    {
        if (_audioSource != null)
        {
            _audioSource.mute = !_audioSource.mute;
            UpdateButtonColor();
        }
    }

    private void UpdateButtonColor()
    {
        // 버튼 이미지가 정상적으로 연결되어 있을 때만 색상을 변경합니다.
        if (buttonImage != null)
        {
            buttonImage.color = _audioSource.mute ? soundOffColor : soundOnColor;
        }
    }
}
