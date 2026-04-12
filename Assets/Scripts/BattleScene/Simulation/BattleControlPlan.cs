using UnityEngine;

public readonly struct BattleControlPlan
{
    public readonly BattleActionType ActionType;
    public readonly BattleUnitCombatState TargetEnemy;
    public readonly BattleUnitCombatState TargetAlly;
    public readonly Vector3 DesiredPosition;
    public readonly bool HasDesiredPosition;
    public readonly Vector2 LocalMove;
    public readonly float Turn;
    public readonly BattleCombatCommand Command;
    public readonly BattleControlStance Stance;
    public readonly bool UsesExplicitCombatCommands;

    public BattleControlPlan(
        BattleActionType actionType,
        BattleUnitCombatState targetEnemy,
        BattleUnitCombatState targetAlly,
        Vector3 desiredPosition,
        bool hasDesiredPosition,
        Vector2 localMove,
        float turn,
        BattleCombatCommand command,
        BattleControlStance stance,
        bool usesExplicitCombatCommands
    )
    {
        ActionType = actionType;
        TargetEnemy = targetEnemy;
        TargetAlly = targetAlly;
        DesiredPosition = desiredPosition;
        HasDesiredPosition = hasDesiredPosition;
        LocalMove = Vector2.ClampMagnitude(localMove, 1f);
        Turn = Mathf.Clamp(turn, -1f, 1f);
        Command = command;
        Stance = stance;
        UsesExplicitCombatCommands = usesExplicitCombatCommands;
    }

    public static BattleControlPlan FromExecutionPlan(BattleActionType actionType, BattleActionExecutionPlan plan) =>
        new BattleControlPlan(
            actionType,
            plan.TargetEnemy,
            plan.TargetAlly,
            plan.DesiredPosition,
            plan.HasDesiredPosition,
            Vector2.zero,
            0f,
            BattleCombatCommand.None,
            BattleControlStance.Neutral,
            usesExplicitCombatCommands: false
        );

    public static BattleControlPlan FromAgentInput(BattleUnitCombatState self, BattleAgentControlInput input)
    {
        BattleUnitCombatState target = BattleFieldSnapshot.IsValidEnemyTarget(self, input.Target) ? input.Target : null;
        return new BattleControlPlan(
            BattleActionType.EngageNearest,
            target,
            null,
            target != null ? target.Position : Vector3.zero,
            false,
            input.SmoothedLocalMove,
            input.SmoothedTurn,
            input.Command,
            input.Stance,
            usesExplicitCombatCommands: true
        );
    }
}
