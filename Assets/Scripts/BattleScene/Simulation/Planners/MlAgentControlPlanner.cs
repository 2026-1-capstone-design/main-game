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
        Vector2 resolvedMove = Vector2.ClampMagnitude(input.ResolvedRelativeMove, 1f);
        BattleCombatIntent combatIntent = ResolveCombatIntent(input.Command);
        BattleMove move = ResolveMove(target, resolvedMove, combatIntent);
        BattleFacingIntent facingIntent = ResolveFacingIntent(target, move, combatIntent);
        plan = new BattleControlPlan(target, null, move, combatIntent, facingIntent);
        return self != null;
    }

    private static BattleCombatIntent ResolveCombatIntent(BattleCombatCommand command) =>
        command switch
        {
            BattleCombatCommand.BasicAttack => BattleCombatIntent.Attack,
            BattleCombatCommand.Skill => BattleCombatIntent.Skill,
            _ => BattleCombatIntent.None,
        };

    private static BattleMove ResolveMove(
        BattleUnitCombatState target,
        Vector2 resolvedMove,
        BattleCombatIntent combatIntent
    )
    {
        if (combatIntent == BattleCombatIntent.Attack)
            return target != null ? BattleMove.ToTarget(target, resolvedMove) : BattleMove.Hold();

        if (resolvedMove.sqrMagnitude > 0.0001f)
            return target != null ? BattleMove.ToRelativeDirection(target, resolvedMove) : BattleMove.Hold();

        return BattleMove.Hold();
    }

    private static BattleFacingIntent ResolveFacingIntent(
        BattleUnitCombatState target,
        BattleMove move,
        BattleCombatIntent combatIntent
    )
    {
        if (move.Intent == BattleMoveIntent.MoveToRelativeDirection)
            return combatIntent == BattleCombatIntent.Attack && target != null
                ? BattleFacingIntent.TargetEnemy
                : BattleFacingIntent.MoveDirection;

        if (target != null)
            return BattleFacingIntent.TargetEnemy;

        return BattleFacingIntent.KeepCurrent;
    }
}
