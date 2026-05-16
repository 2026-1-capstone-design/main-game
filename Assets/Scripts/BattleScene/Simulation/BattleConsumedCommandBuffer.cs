// BattleConsumedCommandBuffer는 combat system이 실제 처리한 일회성 명령을 simulation owner에게 전달한다.
// BattleCombatSystem은 provider나 agent buffer를 모르고, 명령 소비 후처리는 registry가 담당한다.
public sealed class BattleConsumedCommandBuffer
{
    private readonly BattleConsumedCommand[] _items;

    public BattleConsumedCommandBuffer(int capacity)
    {
        _items = new BattleConsumedCommand[UnityEngine.Mathf.Max(0, capacity)];
    }

    public BattleConsumedCommand[] Items => _items;

    public int Count { get; private set; }

    public void Clear()
    {
        Count = 0;
    }

    public void Record(BattleUnitCombatState state, BattleCombatCommand command)
    {
        if (state == null || command == BattleCombatCommand.None || Count >= _items.Length)
        {
            return;
        }

        _items[Count++] = new BattleConsumedCommand(state, command);
    }
}

public readonly struct BattleConsumedCommand
{
    public readonly BattleUnitCombatState State;
    public readonly BattleCombatCommand Command;

    public BattleConsumedCommand(BattleUnitCombatState state, BattleCombatCommand command)
    {
        State = state;
        Command = command;
    }
}
