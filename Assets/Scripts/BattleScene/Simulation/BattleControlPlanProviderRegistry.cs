using System.Collections.Generic;

// 유닛별 control plan provider override를 관리한다.
// override가 없으면 기본 provider를 반환해 simulation system이 제어 출처를 몰라도 되게 한다.
public sealed class BattleControlPlanProviderRegistry
{
    private readonly Dictionary<BattleUnitCombatState, IBattleControlPlanProvider> _overrides =
        new Dictionary<BattleUnitCombatState, IBattleControlPlanProvider>();

    public IBattleControlPlanProvider DefaultProvider { get; set; }

    public void SetOverride(BattleUnitCombatState state, IBattleControlPlanProvider provider)
    {
        if (state == null)
        {
            return;
        }

        if (provider == null)
        {
            _overrides.Remove(state);
            return;
        }

        _overrides[state] = provider;
    }

    public bool TryGet(BattleUnitCombatState state, out IBattleControlPlanProvider provider)
    {
        if (state == null)
        {
            provider = null;
            return false;
        }

        if (_overrides.TryGetValue(state, out provider))
        {
            return true;
        }

        provider = DefaultProvider;
        return provider != null;
    }

    public void Consume(BattleUnitCombatState state, BattleCombatCommand command)
    {
        if (TryGet(state, out IBattleControlPlanProvider provider))
        {
            provider.ConsumeCommand(state, command);
        }
    }

    public void ConsumeAll(BattleConsumedCommandBuffer consumedCommands)
    {
        if (consumedCommands == null)
        {
            return;
        }

        for (int i = 0; i < consumedCommands.Count; i++)
        {
            BattleConsumedCommand consumed = consumedCommands.Items[i];
            Consume(consumed.State, consumed.Command);
        }
    }

    public void Clear()
    {
        _overrides.Clear();
    }
}
