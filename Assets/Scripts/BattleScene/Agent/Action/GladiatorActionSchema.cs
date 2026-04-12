// ML-Agents discrete action branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 명령 종류다.
public enum GladiatorCommand
{
    Move = 0,
    Attack = 1,
}

// ML-Agents role branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 전술 역할이다.
public enum GladiatorActionRole
{
    Engage = 0,
    Assassinate = 1,
    Regroup = 2,
}

// ML-Agents fight mode branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 교전 태세다.
public enum GladiatorFightMode
{
    Neutral = 0,
    Pressure = 1,
    KeepRange = 2,
    Retreat = 3,
}

// ML-Agents anchor kind branch의 raw int 값을 파싱한 뒤 내부 로직에서 사용하는 기준점 종류다.
public enum GladiatorAnchorKind
{
    Enemy = 0,
    Ally = 1,
    TeamCenter = 2,
}

public static class GladiatorActionSchema
{
    public const int ContractVersion = 13;

    public const int ContinuousAnchorStrafe = 0;
    public const int ContinuousAnchorForward = 1;
    public const int ContinuousSize = 2;

    public const int CommandBranch = 0;
    public const int RoleBranch = 1;
    public const int FightModeBranch = 2;
    public const int AnchorBranch = 3;
    public const int DiscreteBranchCount = 4;

    public const int CommandBranchSize = 2;

    public const int RoleBranchSize = 3;

    public const int FightModeBranchSize = 4;

    public const int AnchorKindCount = 3;
    public const int AnchorSlotObservationSize = BattleTeamConstants.MaxUnitsPerTeam;

    public const int TeamCenterAnchorAction = 0;
    public const int AllyAnchorActionOffset = 1;
    public const int EnemyAnchorActionOffset = BattleTeamConstants.MaxUnitsPerTeam;
    public const int AnchorActionBranchSize = BattleTeamConstants.MaxUnitsPerTeam * 2;

    public static int EncodeAnchorAction(GladiatorAnchorKind anchorKind, int anchorSlot)
    {
        switch (anchorKind)
        {
            case GladiatorAnchorKind.TeamCenter:
                return TeamCenterAnchorAction;
            case GladiatorAnchorKind.Ally:
                return AllyAnchorActionOffset + Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 2);
            default:
                return EnemyAnchorActionOffset + Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);
        }
    }

    public static bool TryDecodeAnchorAction(int anchorAction, out GladiatorAnchorKind anchorKind, out int anchorSlot)
    {
        if (anchorAction == TeamCenterAnchorAction)
        {
            anchorKind = GladiatorAnchorKind.TeamCenter;
            anchorSlot = 0;
            return true;
        }

        if (anchorAction >= AllyAnchorActionOffset && anchorAction < EnemyAnchorActionOffset)
        {
            anchorKind = GladiatorAnchorKind.Ally;
            anchorSlot = anchorAction - AllyAnchorActionOffset;
            return true;
        }

        if (anchorAction >= EnemyAnchorActionOffset && anchorAction < AnchorActionBranchSize)
        {
            anchorKind = GladiatorAnchorKind.Enemy;
            anchorSlot = anchorAction - EnemyAnchorActionOffset;
            return true;
        }

        anchorKind = GladiatorAnchorKind.TeamCenter;
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
