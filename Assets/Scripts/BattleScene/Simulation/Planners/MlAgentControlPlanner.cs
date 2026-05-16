using UnityEngine;

// ML-Agents 입력 버퍼를 저수준 BattleControlPlan으로 변환하는 controller다.
public sealed class MlAgentControlPlanner : IBattleControlPlanner
{
    private readonly BattleAgentControlBuffer _buffer;

    public MlAgentControlPlanner(BattleAgentControlBuffer buffer)
    {
        _buffer = buffer;
    }

    public bool IsActive(BattleUnitCombatState self, in BattlePlanningContext context) => self != null;

    public bool TryBuildPlan(BattleUnitCombatState self, in BattlePlanningContext context, out BattleControlPlan plan)
    {
        BattleAgentControlInput input = _buffer != null ? _buffer.GetInput(self) : default;
        BattleUnitCombatState target = BattleFieldSnapshot.IsValidEnemyTarget(self, input.Target) ? input.Target : null;
        BattleUnitCombatState anchorTarget = input.AnchorTarget;
        BattleAnchor anchor = BuildAnchor(self, input, anchorTarget);
        Vector2 relativeMove = Vector2.ClampMagnitude(input.RawLocalMove, 1f);
        BattleCombatIntent combatIntent = ResolveCombatIntent(input.Command);
        BattleMoveIntent moveIntent = ResolveMoveIntent(target, relativeMove, combatIntent);
        BattleFacingIntent facingIntent = ResolveFacingIntent(target, moveIntent, combatIntent);
        plan = new BattleControlPlan(
            target,
            input.AnchorKind == GladiatorAnchorKind.Ally ? anchorTarget : null,
            Vector3.zero,
            false,
            anchor,
            relativeMove,
            moveIntent,
            combatIntent,
            facingIntent
        );
        return self != null;
    }

    private static BattleAnchor BuildAnchor(
        BattleUnitCombatState self,
        BattleAgentControlInput input,
        BattleUnitCombatState target
    )
    {
        BattleAnchorKind kind = input.AnchorKind switch
        {
            GladiatorAnchorKind.Ally => BattleAnchorKind.Ally,
            GladiatorAnchorKind.TeamCenter => BattleAnchorKind.TeamCenter,
            _ => BattleAnchorKind.Enemy,
        };

        return new BattleAnchor(
            kind,
            input.AnchorSlot,
            target,
            target != null ? target.Position : (self != null ? self.Position : UnityEngine.Vector3.zero),
            target != null
        );
    }

    private static BattleCombatIntent ResolveCombatIntent(BattleCombatCommand command) =>
        command switch
        {
            BattleCombatCommand.BasicAttack => BattleCombatIntent.Attack,
            BattleCombatCommand.Skill => BattleCombatIntent.Skill,
            _ => BattleCombatIntent.None,
        };

    private static BattleMoveIntent ResolveMoveIntent(
        BattleUnitCombatState target,
        Vector2 relativeMove,
        BattleCombatIntent combatIntent
    )
    {
        if (combatIntent == BattleCombatIntent.Attack)
            return target != null ? BattleMoveIntent.MoveToTarget : BattleMoveIntent.Hold;

        if (relativeMove.sqrMagnitude > 0.0001f)
            return BattleMoveIntent.MoveByTacticalInput;

        return BattleMoveIntent.Hold;
    }

    private static BattleFacingIntent ResolveFacingIntent(
        BattleUnitCombatState target,
        BattleMoveIntent moveIntent,
        BattleCombatIntent combatIntent
    )
    {
        if (moveIntent == BattleMoveIntent.MoveByTacticalInput)
        {
            return combatIntent == BattleCombatIntent.Attack && target != null
                ? BattleFacingIntent.TargetEnemy
                : BattleFacingIntent.MoveDirection;
        }

        if (target != null)
            return BattleFacingIntent.TargetEnemy;

        return BattleFacingIntent.KeepCurrent;
    }
}
