using UnityEngine;

public class BattleTextManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private FloatingText textPrefab; // 만들어둔 텍스트 프리팹 연결

    [SerializeField]
    private int maxTextCount = 50; // 화면에 띄울 최대 텍스트 개수

    // 원형 버퍼(Circular Buffer) 배열
    private FloatingText[] _textPool;
    private int _currentIndex = 0;

    private void Awake()
    {
        // 게임 시작 시 최대 개수만큼 미리 텍스트를 만들어 둡니다.
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
        if (result.FinalAmount <= 0)
            return;

        Vector3 targetPos = result.Target.Position;

        float randomX = Random.Range(-0.5f, 0.5f);
        float randomZ = Random.Range(-0.5f, 0.5f);
        Vector3 offset = new Vector3(randomX, 2.0f, randomZ);

        // 3. 최종 소환 위치
        Vector3 spawnPos = targetPos + offset;

        SpawnText(spawnPos, Mathf.RoundToInt(result.FinalAmount).ToString(), Color.red);
    }

    private void HandleHealText(BattleHealResult result)
    {
        if (result.FinalAmount <= 0)
            return;

        Vector3 targetPos = result.Target.Position;

        float randomX = Random.Range(-0.5f, 0.5f);
        float randomZ = Random.Range(-0.5f, 0.5f);
        Vector3 offset = new Vector3(randomX, 2.0f, randomZ);

        Vector3 spawnPos = targetPos + offset;

        SpawnText(spawnPos, "+" + Mathf.RoundToInt(result.FinalAmount).ToString(), Color.green);
    }

    // 텍스트 생성 및 가장 오래된 것 덮어쓰기 로직
    private void SpawnText(Vector3 position, string text, Color color)
    {
        Debug.Log(text + "만큼 데미지!");

        if (_textPool == null || _textPool.Length == 0)
            return;

        FloatingText floatingText = _textPool[_currentIndex];

        floatingText.Setup(position, text, color);

        //다음 사용할 텍스트들.
        _currentIndex = (_currentIndex + 1) % maxTextCount;
    }
}
