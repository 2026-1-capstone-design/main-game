using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResourceManager : SingletonBehaviour<ResourceManager>
{
    [SerializeField] private bool verboseLog = true;

    private BalanceSO _balance;
    private int _currentGold;
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
