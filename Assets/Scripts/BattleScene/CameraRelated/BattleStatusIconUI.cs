using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleStatusIconUI : MonoBehaviour
{
    [SerializeField]
    private Image iconImage; // 버프/디버프 아이콘

    [SerializeField]
    private TextMeshProUGUI duration;

    [SerializeField]
    private TextMeshProUGUI levelText; // 중첩 수 (Level)

    [SerializeField]
    private GameObject debuffOutline; // 디버프일 경우 빨간 테두리 등 (선택사항)

    public void Setup(BattleStatusInstance status, Sprite iconSprite)
    {
        gameObject.SetActive(true);

        if (iconImage != null)
            iconImage.sprite = iconSprite;

        // 레벨(중첩)이 1보다 크면 텍스트 표시, 아니면 숨김
        if (levelText != null)
        {
            levelText.text = status.Level.ToString();
            levelText.gameObject.SetActive(status.Level > 1);
        }

        // 디버프 여부에 따라 테두리 활성화
        if (debuffOutline != null)
            debuffOutline.SetActive(status.IsDebuff);
    }

    public void UpdateDuration(float remaining)
    {
        duration.text = Mathf.Max(0f, remaining).ToString("F1");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
