using System.Collections.Generic;

public sealed class RuntimeUnitAgentActionSink
{
    private readonly BattleRuntimeUnit _unit;
    private readonly BattleRuntimeUnitResolver _runtimeResolver;
    private readonly BattleAgentControlBuffer _controlBuffer;

    public RuntimeUnitAgentActionSink(
        BattleRuntimeUnit unit,
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        BattleAgentControlBuffer controlBuffer = null
    )
    {
        _unit = unit;
        _runtimeResolver = new BattleRuntimeUnitResolver(runtimeUnits);
        _controlBuffer = controlBuffer;
    }

    public void Apply(GladiatorPolicyAction action, BattleUnitCombatState target)
    {
        if (_unit == null)
        {
            return;
        }

        BattleRuntimeUnit targetRuntime = _runtimeResolver != null ? _runtimeResolver.Resolve(target) : null;
        _controlBuffer?.SetRawInput(
            _unit.State,
            action.RelativeMove,
            action.AnchorKind,
            action.AnchorSlot,
            action.PathMode,
            action.Command,
            action.Stance,
            target
        );
        _unit.SetAgentControlInput(action.RelativeMove, 0f, action.Command, action.Stance, targetRuntime);
    }

    public void Clear()
    {
        if (_unit != null)
        {
            _controlBuffer?.Clear(_unit.State);
        }

        _unit?.ClearAgentControlInput();
    }
}
