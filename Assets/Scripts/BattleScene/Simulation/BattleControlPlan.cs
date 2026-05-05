using UnityEngine;

public readonly struct BattleControlPlan
{
    public readonly BattleActionType ActionType;
    public readonly BattleUnitCombatState TargetEnemy;
    public readonly BattleUnitCombatState TargetAlly;
    public readonly Vector3 DesiredPosition;
    public readonly bool HasDesiredPosition;
    public readonly Vector2 LocalMove;
    public readonly BattleTacticalCommand TacticalCommand;
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
        BattleTacticalCommand tacticalCommand,
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
        TacticalCommand = tacticalCommand;
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
            default,
            0f,
            BattleCombatCommand.None,
            BattleControlStance.Neutral,
            usesExplicitCombatCommands: false
        );

    public static BattleControlPlan FromAgentInput(BattleUnitCombatState self, BattleAgentControlInput input)
    {
        BattleUnitCombatState target = BattleFieldSnapshot.IsValidEnemyTarget(self, input.Target) ? input.Target : null;
        BattleUnitCombatState anchorTarget = input.AnchorTarget;
        return new BattleControlPlan(
            BattleActionType.EngageNearest,
            target,
            input.AnchorKind == GladiatorActionSchema.AnchorKindAlly ? anchorTarget : null,
            target != null ? target.Position : Vector3.zero,
            false,
            input.SmoothedLocalMove,
            BuildTacticalCommand(self, input, anchorTarget),
            0f,
            input.Command,
            input.Stance,
            usesExplicitCombatCommands: true
        );
    }

    private static BattleTacticalCommand BuildTacticalCommand(
        BattleUnitCombatState self,
        BattleAgentControlInput input,
        BattleUnitCombatState target
    )
    {
        BattleAnchorKind kind = input.AnchorKind switch
        {
            GladiatorActionSchema.AnchorKindAlly => BattleAnchorKind.Ally,
            GladiatorActionSchema.AnchorKindTeamCenter => BattleAnchorKind.TeamCenter,
            _ => BattleAnchorKind.Enemy,
        };

        BattleAnchor anchor = new BattleAnchor(
            kind,
            input.AnchorSlot,
            target,
            target != null ? target.Position : (self != null ? self.Position : Vector3.zero),
            target != null
        );

        BattlePathMode pathMode = input.PathMode switch
        {
            GladiatorActionSchema.PathModeFlankLeft => BattlePathMode.FlankLeft,
            GladiatorActionSchema.PathModeFlankRight => BattlePathMode.FlankRight,
            GladiatorActionSchema.PathModeRegroup => BattlePathMode.Regroup,
            _ => BattlePathMode.Direct,
        };

        return new BattleTacticalCommand(anchor, pathMode, input.SmoothedLocalMove, input.Command, input.Stance);
    }
}
