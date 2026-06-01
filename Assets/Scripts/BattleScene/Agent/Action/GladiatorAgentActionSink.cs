using System.Collections.Generic;
using UnityEngine;

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

    public void Apply(GladiatorAction rawAction, GladiatorAction resolvedAction, BattleUnitCombatState target)
    {
        if (_self == null)
        {
            return;
        }

        Vector2 resolvedMove = resolvedAction.IsResolved ? resolvedAction.RelativeMove : Vector2.zero;
        _controlBuffer?.SetResolvedInput(
            _self,
            rawAction.RelativeMove,
            resolvedMove,
            resolvedAction.Strategy,
            resolvedAction.AnchorSlot,
            resolvedAction.Command,
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
