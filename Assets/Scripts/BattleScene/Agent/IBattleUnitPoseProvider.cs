public interface IBattleUnitPoseProvider
{
    BattleUnitPose CurrentPose { get; }
}

public sealed class RuntimeBattleUnitPoseProvider : IBattleUnitPoseProvider
{
    private readonly BattleRuntimeUnit _unit;

    public RuntimeBattleUnitPoseProvider(BattleRuntimeUnit unit)
    {
        _unit = unit;
    }

    public BattleUnitPose CurrentPose =>
        _unit != null ? new BattleUnitPose(_unit.transform.right, _unit.transform.forward) : BattleUnitPose.Default;
}
