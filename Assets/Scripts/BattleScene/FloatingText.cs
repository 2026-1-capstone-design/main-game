using UnityEngine;
using TMPro;

public class FloatingText : MonoBehaviour
{
    [SerializeField] private TextMeshPro textMesh; // UI 캔버스가 아니라면 일반 TextMeshPro를 씁니다.
    [SerializeField] private float moveSpeed = 2f; // 위로 올라가는 속도
    [SerializeField] private float lifeTime = 1.0f; // 화면에 유지되는 시간

    private float _timer;
    private Color _color;

    // 매니저가 이 함수를 불러서 텍스트를 켭니다.
    public void Setup(Vector3 position, string text, Color color)
    {
        transform.position = position;
        textMesh.text = text;
        _color = color;
        textMesh.color = _color;

        _timer = lifeTime;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        transform.rotation = Camera.main.transform.rotation;
        if (_timer > 0)
        {
            // 위로 둥둥 떠오름
            transform.position += Vector3.up * moveSpeed * Time.deltaTime;

            // 서서히 투명해짐 (페이드 아웃)
            _timer -= Time.deltaTime;
            float alpha = _timer / lifeTime;
            _color.a = alpha;
            textMesh.color = _color;

            // 수명이 다하면 스스로를 끔 (파괴하지 않음!)
            if (_timer <= 0)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
