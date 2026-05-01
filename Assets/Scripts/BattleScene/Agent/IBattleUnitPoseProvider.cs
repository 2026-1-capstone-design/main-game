public interface IBattleUnitPoseProvider
{
    BattleUnitPose CurrentPose { get; }
}

// ML 관찰/휴리스틱이 Transform 대신 BattleUnitCombatState에 동기화된 방향을 읽도록 하는 provider다.
public sealed class BattleUnitStatePoseProvider : IBattleUnitPoseProvider
{
    private readonly BattleUnitCombatState _state;

    public BattleUnitStatePoseProvider(BattleUnitCombatState state)
    {
        _state = state;
    }

    public BattleUnitPose CurrentPose =>
        _state != null ? new BattleUnitPose(_state.RightDirection, _state.FacingDirection) : BattleUnitPose.Default;
}
