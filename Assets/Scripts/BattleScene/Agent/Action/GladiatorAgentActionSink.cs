using System.Collections.Generic;

public sealed class RuntimeUnitAgentActionSink
{
    private readonly BattleUnitCombatState _self;
    private readonly BattleAgentControlBuffer _controlBuffer;

    public RuntimeUnitAgentActionSink(
        BattleUnitCombatState self,
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BattleAgentControlBuffer controlBuffer = null
    )
    {
        _self = self;
        _controlBuffer = controlBuffer;
    }

    public void Apply(GladiatorAction action, BattleUnitCombatState target)
    {
        if (_self == null)
        {
            return;
        }

        _controlBuffer?.SetRawInput(
            _self,
            action.RelativeMove,
            action.Role,
            action.FightMode,
            action.AnchorKind,
            action.AnchorSlot,
            action.Command,
            target
        );
    }

    public void Clear()
    {
        if (_self != null)
        {
            _controlBuffer?.Clear(_self);
        }
    }
}
