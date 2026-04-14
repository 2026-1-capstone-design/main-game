using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceManager : SingletonBehaviour<ResourceManager>
{
    [SerializeField] private bool verboseLog = true;

    private BalanceSO _balance;         // 초기 골드 등 자원 관련 기본 수치를 제공하는 밸런스 데이터 참조
    private int _currentGold;           // 현재 플레이어가 실제로 보유 중인 골드
    private bool _initialized;

    public event Action<int> GoldChanged;

    public int CurrentGold => _currentGold;

    protected override void Awake()
    {
        base.Awake();

        if (!IsPrimaryInstance)
        {
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    // 자원 매니저 최초 초기화임
    // 첫 1회만 initialGold를 실제 보유 골드로 세팅함.
    // 이후 메인씬 재진입에서는 기존 골드를 유지한 채 UI 동기화만 계속 다시 보낸다.
    public void Initialize(BalanceSO balance)
    {
        if (_initialized)
        {
            GoldChanged?.Invoke(_currentGold);
            return;
        }

        _balance = balance;

        if (_balance == null)
        {
            Debug.LogError("[ResourceManager] BalanceSO is null.", this);
            return;
        }

        _currentGold = Mathf.Max(0, _balance.initialGold);
        _initialized = true;

        GoldChanged?.Invoke(_currentGold);

        if (verboseLog)
        {
            Debug.Log($"[ResourceManager] Initialized. CurrentGold={_currentGold}", this);
        }
    }

    public bool CanAfford(int amount)
    {
        amount = Mathf.Max(0, amount);
        return _currentGold >= amount;
    }

    // 골드가 충분할 때만 실제 골드를 차감함.
    // 시장에서 구매할 때 소비 가능 여부를 보는 함수
    public bool TrySpendGold(int amount)
    {
        amount = Mathf.Max(0, amount);

        if (!CanAfford(amount))
        {
            return false;
        }

        _currentGold -= amount;
        GoldChanged?.Invoke(_currentGold);

        if (verboseLog)
        {
            Debug.Log($"[ResourceManager] Gold spent. Amount={amount}, CurrentGold={_currentGold}", this);
        }

        return true;
    }

    // 실제 보유 골드를 즉시 증가
    public void AddGold(int amount)
    {
        amount = Mathf.Max(0, amount);

        _currentGold += amount;
        GoldChanged?.Invoke(_currentGold);

        if (verboseLog)
        {
            Debug.Log($"[ResourceManager] Gold added. Amount={amount}, CurrentGold={_currentGold}", this);
        }
    }

    // SessionManager에 저장돼 있던 펜딩 전투 보상을 소비해서 실제 골드로 지급
    // 즉, 보상을 임시로 저장은 SessionManager,
    // 실제 지급은 이 함수가 함
    public int GrantPendingBattleReward(SessionManager sessionManager)
    {
        if (sessionManager == null)
        {
            Debug.LogError("[ResourceManager] sessionManager is null.", this);
            return 0;
        }

        int pendingReward = sessionManager.ConsumePendingBattleReward();

        if (pendingReward > 0)
        {
            AddGold(pendingReward);
        }
        else
        {
            GoldChanged?.Invoke(_currentGold);
        }

        return pendingReward;
    }
}
