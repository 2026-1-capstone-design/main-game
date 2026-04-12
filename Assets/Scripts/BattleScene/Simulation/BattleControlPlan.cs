using UnityEngine;

public enum BattleMoveIntent
{
    None = 0,
    Hold = 1,
    MoveToTarget = 2,
    MoveToPosition = 3,
    MoveByTacticalInput = 4,
}

public enum BattleCombatIntent
{
    None = 0,
    Attack = 1,
    Skill = 2,
}

public enum BattleFacingIntent
{
    KeepCurrent = 0,
    TargetEnemy = 1,
    DesiredPosition = 2,
    MoveDirection = 3,
}

// BattleControlPlan은 planner가 결정한 tick 단위 최종 실행 명세다.
// 하위 시스템은 explicit/built-in 분기 없이 intent만 읽고 집행한다.
public readonly struct BattleControlPlan
{
    public readonly BattleActionType ActionType;
    public readonly BattleUnitCombatState TargetEnemy;
    public readonly BattleUnitCombatState TargetAlly;
    public readonly Vector3 DesiredPosition;
    public readonly bool HasDesiredPosition;
    public readonly BattleTacticalCommand TacticalCommand;
    public readonly BattleMoveIntent MoveIntent;
    public readonly BattleCombatIntent CombatIntent;
    public readonly BattleFacingIntent FacingIntent;

    public BattleControlPlan(
        BattleActionType actionType,
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        Vector3 desiredPosition,
        bool hasDesiredPosition,
        BattleTacticalCommand tacticalCommand,
        BattleMoveIntent moveIntent,
        BattleCombatIntent combatIntent,
        BattleFacingIntent facingIntent
    )
    {
        ActionType = actionType;
        TargetEnemy = targetEnemy;
        TargetAlly = targetAlly;
        DesiredPosition = desiredPosition;
        HasDesiredPosition = hasDesiredPosition;
        TacticalCommand = tacticalCommand;
        MoveIntent = moveIntent;
        CombatIntent = combatIntent;
        FacingIntent = facingIntent;
    }

    public static BattleControlPlan FromExecutionPlan(
        BattleUnitCombatState self,
        BattleActionType actionType,
        BattleActionExecutionPlan plan
    )
    {
        bool hasValidTarget = BattleFieldSnapshot.IsValidEnemyTarget(self, plan.TargetEnemy);
        bool inAttackRange =
            hasValidTarget && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(self, plan.TargetEnemy);

        BattleMoveIntent moveIntent;
        BattleCombatIntent combatIntent;
        BattleFacingIntent facingIntent;
        if (inAttackRange)
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.Attack;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else if (plan.HasDesiredPosition)
        {
            moveIntent = BattleMoveIntent.MoveToPosition;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.DesiredPosition;
        }
        else if (hasValidTarget)
        {
            moveIntent = BattleMoveIntent.MoveToTarget;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.TargetEnemy;
        }
        else
        {
            moveIntent = BattleMoveIntent.Hold;
            combatIntent = BattleCombatIntent.None;
            facingIntent = BattleFacingIntent.KeepCurrent;
        }

        return new BattleControlPlan(
            actionType,
            plan.TargetEnemy,
            plan.TargetAlly,
            plan.DesiredPosition,
            plan.HasDesiredPosition,
            default,
            moveIntent,
            combatIntent,
            facingIntent
        );
    }

    public static BattleControlPlan FromAgentInput(BattleUnitCombatState self, BattleAgentControlInput input)
    {
        BattleUnitCombatState target = BattleFieldSnapshot.IsValidEnemyTarget(self, input.Target) ? input.Target : null;
        BattleUnitCombatState anchorTarget = input.AnchorTarget;
        BattleCombatIntent combatIntent = ResolveCombatIntent(input.Command);
        BattleMoveIntent moveIntent = ResolveAgentMoveIntent(target, input, combatIntent);
        BattleFacingIntent facingIntent = ResolveAgentFacingIntent(target, moveIntent, combatIntent);
        return new BattleControlPlan(
            BattleActionType.EngageNearest,
            target,
            input.AnchorKind == GladiatorAnchorKind.Ally ? anchorTarget : null,
            Vector3.zero,
            false,
            BuildTacticalCommand(self, input, anchorTarget),
            moveIntent,
            combatIntent,
            facingIntent
        );
    }

    private static BattleCombatIntent ResolveCombatIntent(BattleCombatCommand command) =>
        command switch
        {
            BattleCombatCommand.BasicAttack => BattleCombatIntent.Attack,
            BattleCombatCommand.Skill => BattleCombatIntent.Skill,
            _ => BattleCombatIntent.None,
        };

    private static BattleMoveIntent ResolveAgentMoveIntent(
        BattleUnitCombatState target,
        BattleAgentControlInput input,
        BattleCombatIntent combatIntent
    )
    {
        if (combatIntent == BattleCombatIntent.Attack)
        {
            return target != null ? BattleMoveIntent.MoveToTarget : BattleMoveIntent.Hold;
        }

        if (input.SmoothedLocalMove.sqrMagnitude > 0.0001f)
            return BattleMoveIntent.MoveByTacticalInput;

        return BattleMoveIntent.Hold;
    }

    private static BattleFacingIntent ResolveAgentFacingIntent(
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

    private static BattleTacticalCommand BuildTacticalCommand(
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

        BattleAnchor anchor = new BattleAnchor(
            kind,
            input.AnchorSlot,
            target,
            target != null ? target.Position : (self != null ? self.Position : Vector3.zero),
            target != null
        );

        return new BattleTacticalCommand(anchor, input.SmoothedLocalMove, input.Command, input.FightMode);
    }
}
