// Discrete action schema.
//
// 4 branches (Intent / Move / Target / Rotate)는 Stage 4까지 변경하지 않는다.
// Stage 1~2에서는 IntentUseSkill, IntentDefend를 ActionMask로 봉인하고,
// Stage 3 이후에 풀어서 동일 정책 weight를 재사용한다.
public static class GladiatorActionSchema
{
    // ── Branch 0: Intent ───────────────────────────────────────
    public const int IntentMove = 0;
    public const int IntentAttack = 1;
    public const int IntentUseSkill = 2; // Stage 3+에서 활성
    public const int IntentDefend = 3; // Stage 3+에서 활성 (제자리 방어/도발 등)
    public const int IntentHold = 4;
    public const int IntentBranchSize = 5;

    // ── Branch 1: Move ─────────────────────────────────────────
    public const int MoveNone = 0;
    public const int MoveForward = 1;
    public const int MoveBackward = 2;
    public const int MoveStrafeLeft = 3;
    public const int MoveStrafeRight = 4;
    public const int MoveKeepRange = 5;
    public const int MoveBranchSize = 6;

    // ── Branch 2: Target slot ──────────────────────────────────
    // 공격/스킬 공용. 슬롯 0~(MaxUnitsPerTeam-1)는 OpponentSlots 매핑.
    public const int TargetBranchSize = BattleTeamConstants.MaxUnitsPerTeam;

    // ── Branch 3: Rotate ───────────────────────────────────────
    public const int RotateNone = 0;
    public const int RotateLeft = 1;
    public const int RotateRight = 2;
    public const int RotationBranchSize = 3;
}
