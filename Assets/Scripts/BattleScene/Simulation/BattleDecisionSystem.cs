using System.Collections.Generic;
using UnityEngine;

public sealed class BattleDecisionSystem
{
    private const float CommitmentEnterMultiplier = 1.2f;

    public void Decide(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleAITuningSO aiTuning,
        float tickDeltaTime,
        BattleActionType[] decisions,
        BattleSkillChannelSystem channelSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (units == null || decisions == null)
            return;

        float decay = aiTuning != null ? aiTuning.commitmentDecayPerSecond : 0.5f;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            decisions[i] = BattleActionType.None;

            if (unit == null || unit.IsCombatDisabled)
                continue;

            BattleActionScoreSet scores = EvaluateScores(unit, aiTuning);
            unit.State.SetCurrentScores(scores);

            if (unit.IsExternallyControlled)
            {
                decisions[i] = unit.CurrentActionType;
                continue;
            }

            if (
                (channelSystem != null && channelSystem.IsDecisionChangeBlocked(unit))
                || (rosterMutationSystem != null && rosterMutationSystem.IsCommandDisabled(unit))
            )
            {
                unit.State.SetDecisionState(unit.KeepBehaving, unit.ActionTimer + tickDeltaTime);
                decisions[i] = unit.CurrentActionType;
                continue;
            }

            BattleActionType currentAction = unit.CurrentActionType;

            GetBestActionRespectingEscapeLimit(
                units,
                unit,
                scores,
                BattleActionType.None,
                out BattleActionType bestAction,
                out float bestScore
            );

            if (currentAction == BattleActionType.None)
            {
                EnterAction(unit, bestAction, bestScore, aiTuning);
                decisions[i] = unit.CurrentActionType;
                continue;
            }

            float decayedKeepBehaving = unit.KeepBehaving - (decay * tickDeltaTime);
            float nextActionTimer = unit.ActionTimer + tickDeltaTime;

            GetBestActionRespectingEscapeLimit(
                units,
                unit,
                scores,
                currentAction,
                out BattleActionType bestOtherAction,
                out float bestOtherScore
            );

            if (bestOtherScore > decayedKeepBehaving)
            {
                EnterAction(unit, bestOtherAction, bestOtherScore, aiTuning);
            }
            else
            {
                unit.State.SetCurrentActionType(currentAction, GetActionDisplayName(currentAction, aiTuning));
                unit.State.SetDecisionState(decayedKeepBehaving, nextActionTimer);
            }

            decisions[i] = unit.CurrentActionType;
        }
    }

    private static BattleActionScoreSet EvaluateScores(BattleRuntimeUnit unit, BattleAITuningSO aiTuning)
    {
        WeaponType weaponType = unit.Snapshot != null ? unit.Snapshot.WeaponType : WeaponType.None;
        BattleActionScoreSet scores = default;
        if (aiTuning != null && aiTuning.actionTunings != null && aiTuning.actionTunings.Count > 0)
        {
            scores = BattleActionScorer.Evaluate(unit.CurrentModifiedParameters, weaponType, aiTuning.actionTunings);
        }

        return BattleActionScorer.ApplyEscapeReengageBias(unit.CurrentActionType, unit.CurrentRawParameters, scores);
    }

    private static void EnterAction(
        BattleRuntimeUnit unit,
        BattleActionType actionType,
        float chosenFinalScore,
        BattleAITuningSO aiTuning
    )
    {
        if (unit == null)
            return;

        float keepBehaving = chosenFinalScore * CommitmentEnterMultiplier;
        unit.State.SetCurrentActionType(actionType, GetActionDisplayName(actionType, aiTuning));
        unit.State.SetDecisionState(keepBehaving, 0f);
    }

    private static string GetActionDisplayName(BattleActionType actionType, BattleAITuningSO aiTuning)
    {
        if (aiTuning == null)
            return actionType.ToString();

        BattleActionTuning tuning = aiTuning.GetActionTuning(actionType);
        if (tuning != null && !string.IsNullOrWhiteSpace(tuning.displayName))
            return tuning.displayName;

        return actionType.ToString();
    }

    private static int GetLivingUnitCountForDecision(IReadOnlyList<BattleRuntimeUnit> units)
    {
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit != null && !unit.IsCombatDisabled)
                count++;
        }

        return count;
    }

    private static int GetCurrentEscapeUnitCount(IReadOnlyList<BattleRuntimeUnit> units)
    {
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit != null && !unit.IsCombatDisabled && unit.CurrentActionType == BattleActionType.EscapeFromPressure)
            {
                count++;
            }
        }

        return count;
    }

    private static int GetMaxEscapeUnitCount(IReadOnlyList<BattleRuntimeUnit> units)
    {
        int livingUnitCount = GetLivingUnitCountForDecision(units);
        int maxEscapeCount = Mathf.FloorToInt(livingUnitCount * 0.3f);
        return Mathf.Max(1, maxEscapeCount);
    }

    private static bool CanEnterEscapeAction(IReadOnlyList<BattleRuntimeUnit> units, BattleRuntimeUnit unit)
    {
        if (unit == null || unit.IsCombatDisabled)
            return false;

        if (unit.CurrentActionType == BattleActionType.EscapeFromPressure)
            return true;

        return GetCurrentEscapeUnitCount(units) < GetMaxEscapeUnitCount(units);
    }

    private static void GetBestActionRespectingEscapeLimit(
        IReadOnlyList<BattleRuntimeUnit> units,
        BattleRuntimeUnit unit,
        BattleActionScoreSet scores,
        BattleActionType excludedAction,
        out BattleActionType bestAction,
        out float bestScore
    )
    {
        bool canEnterEscape = CanEnterEscapeAction(units, unit);
        bestAction = BattleActionType.None;
        bestScore = float.MinValue;

        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.AssassinateIsolatedEnemy,
            scores.AssassinateIsolatedEnemy,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.DiveEnemyBackline,
            scores.DiveEnemyBackline,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.PeelForWeakAlly,
            scores.PeelForWeakAlly,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.EscapeFromPressure,
            scores.EscapeFromPressure,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.RegroupToAllies,
            scores.RegroupToAllies,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.CollapseOnCluster,
            scores.CollapseOnCluster,
            excludedAction,
            canEnterEscape
        );
        TryConsiderActionRespectingEscapeLimit(
            ref bestAction,
            ref bestScore,
            BattleActionType.EngageNearest,
            scores.EngageNearest,
            excludedAction,
            canEnterEscape
        );

        if (bestAction == BattleActionType.None)
        {
            bestAction = BattleActionType.EngageNearest;
            bestScore = scores.EngageNearest;
        }
    }

    private static void TryConsiderActionRespectingEscapeLimit(
        ref BattleActionType bestAction,
        ref float bestScore,
        BattleActionType candidateAction,
        float candidateScore,
        BattleActionType excludedAction,
        bool canEnterEscape
    )
    {
        if (candidateAction == excludedAction)
            return;

        if (candidateAction == BattleActionType.EscapeFromPressure && !canEnterEscape)
            return;

        if (candidateScore > bestScore)
        {
            bestAction = candidateAction;
            bestScore = candidateScore;
        }
    }
}
