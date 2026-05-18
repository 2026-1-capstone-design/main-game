public readonly struct BattleCombatResult
{
    public BattleRuntimeUnit Attacker { get; }
    public BattleRuntimeUnit Target { get; }
    public float Damage { get; }
    public bool WasKill { get; }
    public bool WasSkill { get; }

    public BattleCombatResult(
        BattleRuntimeUnit attacker,
        BattleRuntimeUnit target,
        float damage,
        bool wasKill,
        bool wasSkill
    )
    {
        Attacker = attacker;
        Target = target;
        Damage = damage;
        WasKill = wasKill;
        WasSkill = wasSkill;
    }
}

public sealed class BattleCombatResultBuffer
{
    private BattleCombatResult[] _items;

    public BattleCombatResultBuffer(int initialCapacity)
    {
        _items = new BattleCombatResult[initialCapacity];
    }

    public BattleCombatResult[] Items => _items;
    public int Count { get; private set; }

    public void Clear()
    {
        Count = 0;
    }

    public void Add(BattleCombatResult result)
    {
        if (Count >= _items.Length)
            Grow();

        _items[Count] = result;
        Count++;
    }

    private void Grow()
    {
        int nextCapacity = _items.Length > 0 ? _items.Length * 2 : 4;
        BattleCombatResult[] nextItems = new BattleCombatResult[nextCapacity];
        System.Array.Copy(_items, nextItems, _items.Length);
        _items = nextItems;
    }
}
