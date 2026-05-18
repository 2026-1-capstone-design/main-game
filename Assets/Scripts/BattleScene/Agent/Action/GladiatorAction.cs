using System.Collections.Generic;
using UnityEngine;

// GladiatorAgent의 정책 출력 action을 시스템 내부에서 공유하는 값 타입이다.
// ActionBuffers의 raw branch 값은 이 contract로 정규화된 뒤 reward, tactical context, sink, metrics가 함께 해석한다.
public readonly struct GladiatorAction
{
    // Normalized anchor-relative movement. Resolve() applies command-specific scaling before planner execution.
    public readonly Vector2 RelativeMove;
    public readonly GladiatorStrategy Strategy;
    public readonly int AnchorSlot;
    public readonly GladiatorCommand Command;
    public readonly bool IsResolved;

    public GladiatorAction(
        Vector2 relativeMove,
        GladiatorStrategy strategy,
        int anchorSlot,
        GladiatorCommand command,
        bool isResolved = false
    )
    {
        RelativeMove = relativeMove.normalized;
        Strategy = strategy;
        AnchorSlot = Mathf.Clamp(anchorSlot, 0, BattleTeamConstants.MaxUnitsPerTeam - 1);
        Command = command;
        IsResolved = isResolved;
    }

    public GladiatorAction Copy(
        Vector2? relativeMove = null,
        GladiatorStrategy? strategy = null,
        int? anchorSlot = null,
        GladiatorCommand? command = null,
        bool isResolved = false
    )
    {
        return new GladiatorAction(
            relativeMove ?? RelativeMove,
            strategy ?? Strategy,
            anchorSlot ?? AnchorSlot,
            command ?? Command,
            isResolved
        );
    }

    public GladiatorAction Resolve(
        Vector3 selfPosition,
        IReadOnlyList<BattleUnitCombatState> hostiles,
        BattleUnitCombatState selectedTarget,
        out BattleUnitCombatState resolvedTarget,
        out bool anchorFallbackApplied
    )
    {
        resolvedTarget = IsValidTarget(selectedTarget) ? selectedTarget : null;
        int resolvedAnchorSlot = AnchorSlot;
        anchorFallbackApplied = false;

        if (
            resolvedTarget == null
            && TryResolveNearestHostile(
                selfPosition,
                hostiles,
                out BattleUnitCombatState fallbackTarget,
                out int fallbackSlot
            )
        )
        {
            resolvedTarget = fallbackTarget;
            resolvedAnchorSlot = fallbackSlot;
            anchorFallbackApplied = true;
        }

        GladiatorCommand resolvedCommand = Command;
        if (resolvedCommand != GladiatorCommand.Move && resolvedTarget == null)
        {
            resolvedCommand = GladiatorCommand.Move;
        }

        Vector2 resolvedMove = resolvedCommand switch
        {
            GladiatorCommand.Attack => BuildAttackApproachRelativeMove(RelativeMove),
            GladiatorCommand.Withdraw => BuildWithdrawRelativeMove(RelativeMove),
            _ => Vector2.ClampMagnitude(RelativeMove, 1f),
        };

        return new GladiatorAction(resolvedMove, Strategy, resolvedAnchorSlot, resolvedCommand, isResolved: true);
    }

    private static bool IsValidTarget(BattleUnitCombatState target) => target != null && !target.IsCombatDisabled;

    private static bool TryResolveNearestHostile(
        Vector3 selfPosition,
        IReadOnlyList<BattleUnitCombatState> hostiles,
        out BattleUnitCombatState target,
        out int slot
    )
    {
        target = null;
        slot = 0;
        if (hostiles == null)
        {
            return false;
        }

        float bestDistanceSqr = float.MaxValue;
        for (int i = 0; i < hostiles.Count && i < GladiatorObservationSchema.OpponentSlots; i++)
        {
            BattleUnitCombatState candidate = hostiles[i];
            if (!IsValidTarget(candidate))
            {
                continue;
            }

            Vector3 delta = candidate.Position - selfPosition;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            target = candidate;
            slot = i;
        }

        return target != null;
    }

    private static Vector2 BuildWithdrawRelativeMove(Vector2 relativeMove)
    {
        float rawAngle = Mathf.Atan2(relativeMove.x, relativeMove.y) * Mathf.Rad2Deg;
        float compressedAngle =
            rawAngle == 0f ? 180f
            : rawAngle > 0 ? 90f + rawAngle * 0.5f
            : -90f + rawAngle * 0.5f;
        float angleFromAnchorForward = compressedAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angleFromAnchorForward), Mathf.Cos(angleFromAnchorForward));
    }

    private static Vector2 BuildAttackApproachRelativeMove(Vector2 relativeMove)
    {
        float rawAngle = Mathf.Atan2(relativeMove.x, relativeMove.y) * Mathf.Rad2Deg;
        float compressedAngle = rawAngle * 0.25f;
        float angleFromAnchorForward = compressedAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(angleFromAnchorForward), Mathf.Cos(angleFromAnchorForward));
    }
}
