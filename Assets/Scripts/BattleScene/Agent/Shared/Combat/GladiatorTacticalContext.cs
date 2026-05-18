using System.Collections.Generic;
using UnityEngine;

// 한 step의 의사결정 결과를 reward/metrics가 해석할 수 있도록 정리한 실행 컨텍스트다.
// 정책 contract에서 제거된 Role/AnchorKind는 저장하지 않고 enemy anchor slot과 Strategy만 추적한다.
public readonly struct GladiatorTacticalContext
{
    private const float AttackRangePadding = 0.05f;

    public readonly GladiatorCommand? PreviousCommand;
    public readonly GladiatorCommand Command;
    public readonly GladiatorStrategy? PreviousStrategy;
    public readonly GladiatorStrategy Strategy;
    public readonly int PreviousTargetSlot;
    public readonly int TargetSlot;

    public readonly int CommandCommitmentSteps;
    public readonly int AnchorCommitmentSteps;
    public readonly int StrategyCommitmentSteps;

    public readonly float PreviousTargetDistance;
    public readonly float TargetDistance;
    public readonly float TargetEffectiveRange;
    public readonly float TargetThreatToSelfRatio;
    public readonly float SelfThreatToTargetRatio;

    public readonly bool HasLivingOpponent;
    public readonly bool HasAttackableOpponent;
    public readonly bool HasValidTarget;
    public readonly bool IsTargetOutOfAttackRange;
    public readonly bool IsAttackBlocked;
    public readonly bool AnchorFallbackApplied;

    public readonly bool CompletedCommandWindow;
    public readonly bool CompletedStrategyWindow;
    public readonly bool CompletedAnchorWindow;

    public GladiatorTacticalContext(
        GladiatorCommand? previousCommand,
        GladiatorCommand command,
        GladiatorStrategy? previousStrategy,
        GladiatorStrategy strategy,
        int previousTargetSlot,
        int targetSlot,
        int commandCommitmentSteps,
        int anchorCommitmentSteps,
        int strategyCommitmentSteps,
        float previousTargetDistance,
        float targetDistance,
        float targetEffectiveRange,
        float targetThreatToSelfRatio,
        float selfThreatToTargetRatio,
        bool hasLivingOpponent,
        bool hasAttackableOpponent,
        bool hasValidTarget,
        bool isTargetOutOfAttackRange,
        bool isAttackBlocked,
        bool anchorFallbackApplied,
        bool completedCommandWindow,
        bool completedStrategyWindow,
        bool completedAnchorWindow
    )
    {
        PreviousCommand = previousCommand;
        Command = command;
        PreviousStrategy = previousStrategy;
        Strategy = strategy;
        PreviousTargetSlot = previousTargetSlot;
        TargetSlot = targetSlot;
        CommandCommitmentSteps = commandCommitmentSteps;
        AnchorCommitmentSteps = anchorCommitmentSteps;
        StrategyCommitmentSteps = strategyCommitmentSteps;
        PreviousTargetDistance = previousTargetDistance;
        TargetDistance = targetDistance;
        TargetEffectiveRange = targetEffectiveRange;
        TargetThreatToSelfRatio = targetThreatToSelfRatio;
        SelfThreatToTargetRatio = selfThreatToTargetRatio;
        HasLivingOpponent = hasLivingOpponent;
        HasAttackableOpponent = hasAttackableOpponent;
        HasValidTarget = hasValidTarget;
        IsTargetOutOfAttackRange = isTargetOutOfAttackRange;
        IsAttackBlocked = isAttackBlocked;
        AnchorFallbackApplied = anchorFallbackApplied;
        CompletedCommandWindow = completedCommandWindow;
        CompletedStrategyWindow = completedStrategyWindow;
        CompletedAnchorWindow = completedAnchorWindow;
    }

    public static class Builder
    {
        public static GladiatorTacticalContext Build(
            BattleUnitCombatState self,
            IReadOnlyList<BattleUnitCombatState> opponents,
            GladiatorAction action,
            BattleUnitCombatState target,
            int commitmentWindowSteps,
            GladiatorCommand? previousCommand,
            int previousTargetSlot,
            GladiatorStrategy? previousStrategy,
            int commandCommitmentSteps,
            int anchorCommitmentSteps,
            int strategyCommitmentSteps,
            float previousTargetDistance,
            bool anchorFallbackApplied
        )
        {
            bool attackBlocked = self != null && (self.AttackCooldownRemaining > 0f || self.IsAttacking);
            bool hasValidTarget = target != null && !target.IsCombatDisabled;
            float targetDistance = GetDistanceToTarget(self, target);
            float resolvedPreviousTargetDistance =
                previousTargetSlot == action.AnchorSlot && previousTargetDistance < float.MaxValue
                    ? previousTargetDistance
                    : targetDistance;
            float targetEffectiveRange = GetEffectiveAttackRange(self, target);
            int nextCommandCommitmentSteps = previousCommand == action.Command ? commandCommitmentSteps + 1 : 0;
            int nextAnchorCommitmentSteps =
                !anchorFallbackApplied && previousTargetSlot == action.AnchorSlot ? anchorCommitmentSteps + 1 : 0;
            int nextStrategyCommitmentSteps = previousStrategy == action.Strategy ? strategyCommitmentSteps + 1 : 0;

            return new GladiatorTacticalContext(
                previousCommand,
                action.Command,
                previousStrategy,
                action.Strategy,
                previousTargetSlot,
                action.AnchorSlot,
                nextCommandCommitmentSteps,
                nextAnchorCommitmentSteps,
                nextStrategyCommitmentSteps,
                resolvedPreviousTargetDistance,
                targetDistance,
                targetEffectiveRange,
                GetDamageToMaxHealthRatio(target, self),
                GetDamageToMaxHealthRatio(self, target),
                HasLivingOpponent(opponents),
                HasAttackableOpponent(self, opponents),
                hasValidTarget,
                !hasValidTarget || targetDistance > targetEffectiveRange,
                attackBlocked,
                anchorFallbackApplied,
                previousCommand == action.Command && commandCommitmentSteps + 1 >= commitmentWindowSteps,
                previousStrategy == action.Strategy && strategyCommitmentSteps + 1 >= commitmentWindowSteps,
                !anchorFallbackApplied
                    && previousTargetSlot == action.AnchorSlot
                    && anchorCommitmentSteps + 1 >= commitmentWindowSteps
            );
        }

        private static bool HasAttackableOpponent(
            BattleUnitCombatState self,
            IReadOnlyList<BattleUnitCombatState> opponents
        )
        {
            if (opponents == null)
            {
                return false;
            }

            for (int i = 0; i < opponents.Count; i++)
            {
                BattleUnitCombatState target = opponents[i];
                if (
                    target != null
                    && !target.IsCombatDisabled
                    && GetDistanceToTarget(self, target) <= GetEffectiveAttackRange(self, target)
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLivingOpponent(IReadOnlyList<BattleUnitCombatState> opponents)
        {
            if (opponents == null)
            {
                return false;
            }

            for (int i = 0; i < opponents.Count; i++)
            {
                BattleUnitCombatState target = opponents[i];
                if (target != null && !target.IsCombatDisabled)
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetDistanceToTarget(BattleUnitCombatState self, BattleUnitCombatState target)
        {
            if (target == null || self == null)
            {
                return float.MaxValue;
            }

            Vector3 delta = target.Position - self.Position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static float GetEffectiveAttackRange(BattleUnitCombatState attacker, BattleUnitCombatState target)
        {
            if (attacker == null || target == null)
            {
                return 0f;
            }

            return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attacker.AttackRange) + AttackRangePadding;
        }

        private static float GetDamageToMaxHealthRatio(BattleUnitCombatState attacker, BattleUnitCombatState target)
        {
            if (attacker == null || target == null || target.MaxHealth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Max(0f, attacker.Attack) / target.MaxHealth);
        }
    }
}
