using System.Collections.Generic;
using UnityEngine;

// 한 step의 의사결정 결과를 reward/metrics가 해석할 수 있도록 정리한 실행 컨텍스트다.
// Observation feature와 달리, "무슨 형세를 봤는가"보다 "이번 step에서 무엇을 선택했고 직전 대비 무엇이 바뀌었는가"를 담는다.
public readonly struct GladiatorTacticalContext
{
    private const float AttackRangePadding = 0.05f;

    // 이전 선택과 현재 선택을 함께 보관해 switch penalty나 commitment 보상을 계산한다.
    public readonly GladiatorCommand? PreviousCommand;
    public readonly GladiatorCommand Command;
    public readonly GladiatorActionRole? PreviousRole;
    public readonly GladiatorActionRole Role;
    public readonly GladiatorAnchorKind? PreviousAnchorKind;
    public readonly GladiatorAnchorKind AnchorKind;
    public readonly int PreviousTargetSlot;
    public readonly int TargetSlot;
    public readonly GladiatorFightMode? PreviousFightMode;
    public readonly GladiatorFightMode FightMode;

    // commitment step은 현재 선택을 몇 step 연속 유지했는지 나타낸다.
    public readonly int CommandCommitmentSteps;
    public readonly int AnchorCommitmentSteps;
    public readonly int RoleCommitmentSteps;
    public readonly int FightModeCommitmentSteps;

    // 이전 거리와 현재 거리를 함께 둬야 "접근 중인지"를 한 step 단위로 판단할 수 있다.
    public readonly float PreviousTargetDistance;
    public readonly float TargetDistance;
    public readonly float TargetEffectiveRange;
    public readonly float TargetThreatToSelfRatio;
    public readonly float SelfThreatToTargetRatio;

    // 전투 가능 여부와 target 유효성은 액션 무효화, reward 분기, fallback 처리의 공통 입력이다.
    public readonly bool HasLivingOpponent;
    public readonly bool HasAttackableOpponent;
    public readonly bool HasValidTarget;
    public readonly bool IsTargetOutOfAttackRange;
    public readonly bool IsAttackBlocked;
    public readonly bool AnchorFallbackApplied;

    // commitment 결과는 evaluator가 별도 상태 추적 없이 즉시 보상을 적용하도록 미리 계산해 둔다.
    public readonly bool CompletedCommandWindow;
    public readonly bool CompletedRoleWindow;
    public readonly bool CompletedFightModeWindow;
    public readonly bool CompletedAnchorWindow;

    public GladiatorTacticalContext(
        GladiatorCommand? previousCommand,
        GladiatorCommand command,
        GladiatorActionRole? previousRole,
        GladiatorActionRole role,
        GladiatorAnchorKind? previousAnchorKind,
        GladiatorAnchorKind anchorKind,
        int previousTargetSlot,
        int targetSlot,
        GladiatorFightMode? previousFightMode,
        GladiatorFightMode fightMode,
        int commandCommitmentSteps,
        int anchorCommitmentSteps,
        int roleCommitmentSteps,
        int fightModeCommitmentSteps,
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
        bool completedRoleWindow,
        bool completedFightModeWindow,
        bool completedAnchorWindow
    )
    {
        PreviousCommand = previousCommand;
        Command = command;
        PreviousRole = previousRole;
        Role = role;
        PreviousAnchorKind = previousAnchorKind;
        AnchorKind = anchorKind;
        PreviousTargetSlot = previousTargetSlot;
        TargetSlot = targetSlot;
        PreviousFightMode = previousFightMode;
        FightMode = fightMode;
        CommandCommitmentSteps = commandCommitmentSteps;
        AnchorCommitmentSteps = anchorCommitmentSteps;
        RoleCommitmentSteps = roleCommitmentSteps;
        FightModeCommitmentSteps = fightModeCommitmentSteps;
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
        CompletedRoleWindow = completedRoleWindow;
        CompletedFightModeWindow = completedFightModeWindow;
        CompletedAnchorWindow = completedAnchorWindow;
    }

    // Builder는 agent 내부에 흩어져 있던 전술 컨텍스트 조립 규칙을 한 곳에 모은다.
    public static class Builder
    {
        public static GladiatorTacticalContext Build(
            BattleUnitCombatState self,
            IReadOnlyList<BattleUnitCombatState> opponents,
            GladiatorAction action,
            BattleUnitCombatState target,
            int commitmentWindowSteps,
            GladiatorCommand? previousCommand,
            GladiatorActionRole? previousRole,
            GladiatorAnchorKind? previousAnchorKind,
            int previousTargetSlot,
            GladiatorFightMode? previousFightMode,
            int commandCommitmentSteps,
            int anchorCommitmentSteps,
            int roleCommitmentSteps,
            int fightModeCommitmentSteps,
            float previousTargetDistance,
            bool anchorFallbackApplied
        )
        {
            bool attackBlocked = self != null && (self.AttackCooldownRemaining > 0f || self.IsAttacking);
            bool hasValidTarget =
                action.AnchorKind == GladiatorAnchorKind.Enemy && target != null && !target.IsCombatDisabled;
            float targetDistance = GetDistanceToTarget(self, target);
            float resolvedPreviousTargetDistance =
                previousTargetSlot == action.AnchorSlot
                && previousAnchorKind == action.AnchorKind
                && previousTargetDistance < float.MaxValue
                    ? previousTargetDistance
                    : targetDistance;
            float targetEffectiveRange = GetEffectiveAttackRange(self, target);
            float targetThreatToSelfRatio = GetDamageToMaxHealthRatio(target, self);
            float selfThreatToTargetRatio = GetDamageToMaxHealthRatio(self, target);
            int nextAnchorCommitmentSteps = ComputeAnchorCommitmentSteps(
                action,
                previousAnchorKind,
                previousTargetSlot,
                anchorCommitmentSteps,
                anchorFallbackApplied
            );
            int nextCommandCommitmentSteps = ComputeCommandCommitmentSteps(
                action,
                previousCommand,
                commandCommitmentSteps
            );
            int nextRoleCommitmentSteps = ComputeRoleCommitmentSteps(action, previousRole, roleCommitmentSteps);
            int nextFightModeCommitmentSteps = ComputeFightModeCommitmentSteps(
                action,
                previousFightMode,
                fightModeCommitmentSteps
            );

            return new GladiatorTacticalContext(
                previousCommand,
                action.Command,
                previousRole,
                action.Role,
                previousAnchorKind,
                action.AnchorKind,
                previousTargetSlot,
                action.AnchorSlot,
                previousFightMode,
                action.FightMode,
                nextCommandCommitmentSteps,
                nextAnchorCommitmentSteps,
                nextRoleCommitmentSteps,
                nextFightModeCommitmentSteps,
                resolvedPreviousTargetDistance,
                targetDistance,
                targetEffectiveRange,
                targetThreatToSelfRatio,
                selfThreatToTargetRatio,
                HasLivingOpponent(opponents),
                HasAttackableOpponent(self, opponents),
                hasValidTarget,
                !hasValidTarget || targetDistance > targetEffectiveRange,
                attackBlocked,
                anchorFallbackApplied,
                CompletedCommandWindow(action, commitmentWindowSteps, previousCommand, commandCommitmentSteps),
                CompletedRoleWindow(action, commitmentWindowSteps, previousRole, roleCommitmentSteps),
                CompletedFightModeWindow(action, commitmentWindowSteps, previousFightMode, fightModeCommitmentSteps),
                CompletedAnchorWindow(
                    action,
                    commitmentWindowSteps,
                    previousAnchorKind,
                    previousTargetSlot,
                    anchorCommitmentSteps,
                    anchorFallbackApplied
                )
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

        private static int ComputeCommandCommitmentSteps(
            GladiatorAction action,
            GladiatorCommand? previousCommand,
            int commandCommitmentSteps
        )
        {
            return previousCommand == action.Command ? commandCommitmentSteps + 1 : 0;
        }

        private static int ComputeAnchorCommitmentSteps(
            GladiatorAction action,
            GladiatorAnchorKind? previousAnchorKind,
            int previousTargetSlot,
            int anchorCommitmentSteps,
            bool anchorFallbackApplied
        )
        {
            if (anchorFallbackApplied)
            {
                return 0;
            }

            bool keptSameAnchor = previousTargetSlot == action.AnchorSlot && previousAnchorKind == action.AnchorKind;
            return keptSameAnchor ? anchorCommitmentSteps + 1 : 0;
        }

        private static int ComputeRoleCommitmentSteps(
            GladiatorAction action,
            GladiatorActionRole? previousRole,
            int roleCommitmentSteps
        )
        {
            return previousRole == action.Role ? roleCommitmentSteps + 1 : 0;
        }

        private static int ComputeFightModeCommitmentSteps(
            GladiatorAction action,
            GladiatorFightMode? previousFightMode,
            int fightModeCommitmentSteps
        )
        {
            return previousFightMode == action.FightMode ? fightModeCommitmentSteps + 1 : 0;
        }

        private static bool CompletedCommandWindow(
            GladiatorAction action,
            int commitmentWindowSteps,
            GladiatorCommand? previousCommand,
            int commandCommitmentSteps
        )
        {
            return previousCommand == action.Command && commandCommitmentSteps + 1 >= commitmentWindowSteps;
        }

        private static bool CompletedRoleWindow(
            GladiatorAction action,
            int commitmentWindowSteps,
            GladiatorActionRole? previousRole,
            int roleCommitmentSteps
        )
        {
            return previousRole == action.Role && roleCommitmentSteps + 1 >= commitmentWindowSteps;
        }

        private static bool CompletedFightModeWindow(
            GladiatorAction action,
            int commitmentWindowSteps,
            GladiatorFightMode? previousFightMode,
            int fightModeCommitmentSteps
        )
        {
            return previousFightMode == action.FightMode && fightModeCommitmentSteps + 1 >= commitmentWindowSteps;
        }

        private static bool CompletedAnchorWindow(
            GladiatorAction action,
            int commitmentWindowSteps,
            GladiatorAnchorKind? previousAnchorKind,
            int previousTargetSlot,
            int anchorCommitmentSteps,
            bool anchorFallbackApplied
        )
        {
            if (anchorFallbackApplied)
            {
                return false;
            }

            return previousTargetSlot == action.AnchorSlot
                && previousAnchorKind == action.AnchorKind
                && anchorCommitmentSteps + 1 >= commitmentWindowSteps;
        }
    }
}
