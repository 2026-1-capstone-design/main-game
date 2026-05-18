// Action:
//   Continuous Actions = GladiatorActionSchema.ContinuousSize (= 2)
//     0=anchor strafe, 1=anchor forward
//   Discrete Branches  = 3
//     Branch 0 Size = GladiatorActionSchema.CommandBranchSize (= 3)
//     Branch 1 Size = GladiatorActionSchema.StrategyBranchSize (= 4)
//     Branch 2 Size = GladiatorActionSchema.AnchorActionBranchSize (= 6)

//   Continuous 0/1:     anchor strafe / anchor forward
//   Branch 0 (명령):     0=없음  1=기본공격  2=후퇴이동
//   Branch 1 (strategy): 0=중립  1=압박  2=거리유지  3=후퇴
//   Branch 2 (anchor):   0~5=적 슬롯
// ML-Agents discrete action branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 명령 종류다.
public enum GladiatorCommand
{
    Move = 0,
    Attack = 1,
    Withdraw = 2,
}

// ML-Agents strategy branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 단기 교전 태세다.
public enum GladiatorStrategy
{
    Neutral = 0,
    Pressure = 1,
    KeepRange = 2,
    Retreat = 3,
}

public static class GladiatorActionSchema
{
    public const int ContractVersion = 15;

    public const int ContinuousAnchorStrafe = 0;
    public const int ContinuousAnchorForward = 1;
    public const int ContinuousSize = 2;

    public const int CommandBranch = 0;
    public const int StrategyBranch = 1;
    public const int AnchorBranch = 2;
    public const int DiscreteBranchCount = 3;

    public const int CommandBranchSize = 3;
    public const int StrategyBranchSize = 4;
    public const int AnchorActionBranchSize = BattleTeamConstants.MaxUnitsPerTeam;

    public static readonly int[] DiscreteBranchSizes =
    {
        CommandBranchSize,
        StrategyBranchSize,
        AnchorActionBranchSize,
    };

    public static int EncodeEnemyAnchorAction(int anchorSlot) =>
        Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);

    public static bool TryDecodeEnemyAnchorAction(int anchorAction, out int anchorSlot)
    {
        if (anchorAction >= 0 && anchorAction < AnchorActionBranchSize)
        {
            anchorSlot = anchorAction;
            return true;
        }

        anchorSlot = 0;
        return false;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
