using System.Collections.Generic;
using UnityEngine;

public class BattleTextManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField]
    private FloatingText textPrefab;

    [SerializeField]
    private int maxTextCount = 50;

    [SerializeField]
    private float statusTextInterval = 0.2f;

    private FloatingText[] _textPool;
    private int _currentIndex = 0;

    private struct PendingStatusText
    {
        public Vector3 BasePosition;
        public string Text;
        public Color TextColor;
    }

    //유닛 한 명이 가질 '큐'와 '타이머'
    private class UnitTextQueue
    {
        public Queue<PendingStatusText> Queue = new Queue<PendingStatusText>();
        public float Timer = 0f;
    }

    // 유닛(State)을 식별표로 삼아 각자의 큐를 찾아주는 딕셔너리
    private Dictionary<BattleUnitCombatState, UnitTextQueue> _unitQueues =
        new Dictionary<BattleUnitCombatState, UnitTextQueue>();

    // 안전한 딕셔너리 삭제를 위한 캐싱 리스트 (최적화용)
    private List<BattleUnitCombatState> _emptyKeys = new List<BattleUnitCombatState>();

    private void Awake()
    {
        _textPool = new FloatingText[maxTextCount];
        for (int i = 0; i < maxTextCount; i++)
        {
            FloatingText obj = Instantiate(textPrefab, transform);
            obj.gameObject.SetActive(false);
            _textPool[i] = obj;
        }
    }

    public void Initialize(BattleEffectSystem effectSystem)
    {
        effectSystem.OnDamageProcessed += HandleDamageText;
        effectSystem.OnHealProcessed += HandleHealText;
        effectSystem.OnStatusApplied += HandleStatusText;
    }

    private void Update()
    {
        _emptyKeys.Clear();

        //딕셔너리에 등록된 "모든 유닛의 큐"를 동시에 검사합니다.
        foreach (var kvp in _unitQueues)
        {
            BattleUnitCombatState unit = kvp.Key;
            UnitTextQueue unitData = kvp.Value;

            if (unitData.Queue.Count > 0)
            {
                // 유닛 각자의 타이머를 줄입니다.
                unitData.Timer -= Time.deltaTime;

                if (unitData.Timer <= 0f)
                {
                    unitData.Timer = statusTextInterval; // 타이머 리셋

                    PendingStatusText data = unitData.Queue.Dequeue();

                    float randomX = Random.Range(-0.7f, 0.7f);
                    float randomZ = Random.Range(-0.7f, 0.7f);
                    Vector3 spawnPos = data.BasePosition + new Vector3(randomX, 2.5f, randomZ);

                    SpawnText(spawnPos, data.Text, data.TextColor);
                }
            }
            else
            {
                // 큐가 텅 비었다면 더 이상 관리할 필요가 없으므로 삭제 예약
                _emptyKeys.Add(unit);
            }
        }

        //할 일이 끝난 유닛의 큐는 딕셔너리에서 깔끔하게 지워줍니다. (메모리 관리)
        for (int i = 0; i < _emptyKeys.Count; i++)
        {
            _unitQueues.Remove(_emptyKeys[i]);
        }
    }

    private void HandleStatusText(BattleStatusRequest request)
    {
        if (request.Target == null)
            return;

        Color textColor = request.IsDebuff ? new Color(1f, 0.6f, 0f) : Color.cyan;
        string statusName = request.Type.ToString().ToUpper() + "!";
        Vector3 targetPos = request.Target.Position;

        // 유닛의 큐가 딕셔너리에 없다면 새로 만들어줍니다.
        if (!_unitQueues.ContainsKey(request.Target))
        {
            _unitQueues[request.Target] = new UnitTextQueue();
        }

        // "그 유닛 전용 큐"에만 텍스트를 집어넣습니다.
        _unitQueues[request.Target]
            .Queue.Enqueue(
                new PendingStatusText
                {
                    BasePosition = targetPos,
                    Text = statusName,
                    TextColor = textColor,
                }
            );
    }

    private void HandleDamageText(BattleDamageResult result)
    {
        if (result.FinalAmount <= 0)
            return;

        Vector3 targetPos = result.Target.Position;

        float randomX = Random.Range(-0.5f, 0.5f);
        float randomZ = Random.Range(-0.5f, 0.5f);
        Vector3 offset = new Vector3(randomX, 2.0f, randomZ);

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

    private void SpawnText(Vector3 position, string text, Color color)
    {
        if (_textPool == null || _textPool.Length == 0)
            return;

        FloatingText floatingText = _textPool[_currentIndex];
        floatingText.Setup(position, text, color);
        _currentIndex = (_currentIndex + 1) % maxTextCount;
    }
}
