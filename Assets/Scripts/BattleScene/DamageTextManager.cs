using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private FloatingText textPrefab; // 만들어둔 텍스트 프리팹 연결
    [SerializeField] private int maxTextCount = 50;   // 화면에 띄울 최대 텍스트 개수

    // 원형 버퍼(Circular Buffer) 배열
    private FloatingText[] _textPool;
    private int _currentIndex = 0;

    private void Awake()
    {
        // 🌟 게임 시작 시 최대 개수만큼 미리 텍스트를 만들어 둡니다.
        _textPool = new FloatingText[maxTextCount];
        for (int i = 0; i < maxTextCount; i++)
        {
            FloatingText obj = Instantiate(textPrefab, transform);
            obj.gameObject.SetActive(false); // 일단 꺼둠
            _textPool[i] = obj;
        }
    }

    public void Initialize(BattleEffectSystem effectSystem)
    {
        effectSystem.OnDamageProcessed += HandleDamageText;
        effectSystem.OnHealProcessed += HandleHealText;
    }

    private void HandleDamageText(BattleDamageResult result)
    {
        if (result.FinalAmount <= 0) return;

        // (주의: result.Target 객체에서 3D 공간상의 좌표를 정상적으로 가져올 수 있다고 가정합니다.)
        // Vector3 spawnPos = result.Target.Position + Vector3.up * 2f;

        // 예시를 위해 임시 좌표 할당 (실제 적용 시 위 주석 해제)
        Vector3 spawnPos = Vector3.up * 2f;

        SpawnText(spawnPos, Mathf.RoundToInt(result.FinalAmount).ToString(), Color.red);
    }

    private void HandleHealText(BattleHealResult result)
    {
        if (result.FinalAmount <= 0) return;

        // Vector3 spawnPos = result.Target.Position + Vector3.up * 2f;
        Vector3 spawnPos = Vector3.up * 2f;

        SpawnText(spawnPos, "+" + Mathf.RoundToInt(result.FinalAmount).ToString(), Color.green);
    }

    // 🌟 텍스트 생성 및 가장 오래된 것 덮어쓰기 로직
    private void SpawnText(Vector3 position, string text, Color color)
    {
        if (_textPool == null || _textPool.Length == 0) return;

        // 1. 현재 순번의 텍스트 객체를 배열에서 꺼냅니다.
        FloatingText floatingText = _textPool[_currentIndex];

        // 2. 새로운 위치, 글자, 색상으로 덮어씌우고 화면에 켭니다.
        // (만약 아직 안 사라진 오래된 텍스트라면, 즉시 이 위치로 순간이동하며 새 텍스트가 됩니다.)
        floatingText.Setup(position, text, color);

        // 3. 다음 텍스트가 사용할 인덱스를 가리킵니다.
        // % (나머지 연산자)를 사용해 maxTextCount에 도달하면 다시 0번으로 돌아갑니다!
        _currentIndex = (_currentIndex + 1) % maxTextCount;
    }
}
