using System.Collections.Generic;
using UnityEngine;

public sealed class BattleDecisionSystem
{
    private const float CommitmentEnterMultiplier = 1.2f;

    public void Decide(
        IReadOnlyList<BattleUnitCombatState> states,
        BattleAITuningSO aiTuning,
        float tickDeltaTime,
        BattleActionType[] decisions,
        BattleSkillChannelSystem channelSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (states == null || decisions == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            BattleUnitCombatState state = states[i];
            decisions[i] = DecideBuiltInUnit(
                states,
                state,
                aiTuning,
                tickDeltaTime,
                channelSystem,
                rosterMutationSystem
            );
        }
    }

    public BattleActionType DecideBuiltInUnit(
        IReadOnlyList<BattleUnitCombatState> states,
        BattleUnitCombatState state,
        BattleAITuningSO aiTuning,
        float tickDeltaTime,
        BattleSkillChannelSystem channelSystem = null,
        BattleRosterMutationSystem rosterMutationSystem = null
    )
    {
        if (state == null || state.IsCombatDisabled)
            return BattleActionType.None;

        float decay = aiTuning != null ? aiTuning.commitmentDecayPerSecond : 0.5f;
        BattleActionScoreSet scores = EvaluateScores(state, aiTuning);
        state.SetCurrentScores(scores);

        if (
            (channelSystem != null && channelSystem.IsDecisionChangeBlocked(state))
            || (rosterMutationSystem != null && rosterMutationSystem.IsCommandDisabled(state))
        )
        {
            state.SetDecisionState(state.KeepBehaving, state.ActionTimer + tickDeltaTime);
            return state.CurrentActionType;
        }

        BattleActionType currentAction = state.CurrentActionType;
        IReadOnlyList<BattleUnitCombatState> decisionStates = states ?? new[] { state };

        GetBestActionRespectingEscapeLimit(
            decisionStates,
            state,
            scores,
            BattleActionType.None,
            out BattleActionType bestAction,
            out float bestScore
        );

        if (currentAction == BattleActionType.None)
        {
            EnterAction(state, bestAction, bestScore, aiTuning);
            return state.CurrentActionType;
        }

        float decayedKeepBehaving = state.KeepBehaving - (decay * tickDeltaTime);
        float nextActionTimer = state.ActionTimer + tickDeltaTime;

        GetBestActionRespectingEscapeLimit(
            decisionStates,
            state,
            scores,
            currentAction,
            out BattleActionType bestOtherAction,
            out float bestOtherScore
        );

        if (bestOtherScore > decayedKeepBehaving)
        {
            EnterAction(state, bestOtherAction, bestOtherScore, aiTuning);
        }
        else
        {
            state.SetCurrentActionType(currentAction, GetActionDisplayName(currentAction, aiTuning));
            state.SetDecisionState(decayedKeepBehaving, nextActionTimer);
        }

        return state.CurrentActionType;
    }

    private static BattleActionScoreSet EvaluateScores(BattleUnitCombatState state, BattleAITuningSO aiTuning)
    {
        BattleActionScoreSet scores = default;
        if (aiTuning != null && aiTuning.actionTunings != null && aiTuning.actionTunings.Count > 0)
        {
            scores = BattleActionScorer.Evaluate(
                state.CurrentModifiedParameters,
                state.WeaponType,
                aiTuning.actionTunings
            );
        }

        return BattleActionScorer.ApplyEscapeReengageBias(state.CurrentActionType, state.CurrentRawParameters, scores);
    }

    private static void EnterAction(
        BattleUnitCombatState state,
        BattleActionType actionType,
        float chosenFinalScore,
        BattleAITuningSO aiTuning
    )
    {
        if (state == null)
            return;

        float keepBehaving = chosenFinalScore * CommitmentEnterMultiplier;
        state.SetCurrentActionType(actionType, GetActionDisplayName(actionType, aiTuning));
        state.SetDecisionState(keepBehaving, 0f);
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

    private static int GetLivingUnitCountForDecision(IReadOnlyList<BattleUnitCombatState> states)
    {
        int count = 0;
        for (int i = 0; i < states.Count; i++)
        {
            BattleUnitCombatState state = states[i];
            if (state != null && !state.IsCombatDisabled)
                count++;
        }

        return count;
    }

    private static int GetCurrentEscapeUnitCount(IReadOnlyList<BattleUnitCombatState> states)
    {
        int count = 0;
        for (int i = 0; i < states.Count; i++)
        {
            BattleUnitCombatState state = states[i];
            if (
                state != null
                && !state.IsCombatDisabled
                && state.CurrentActionType == BattleActionType.EscapeFromPressure
            )
            {
                count++;
            }
        }

        return count;
    }

    private static int GetMaxEscapeUnitCount(IReadOnlyList<BattleUnitCombatState> states)
    {
        int livingUnitCount = GetLivingUnitCountForDecision(states);
        int maxEscapeCount = Mathf.FloorToInt(livingUnitCount * 0.3f);
        return Mathf.Max(1, maxEscapeCount);
    }

    private static bool CanEnterEscapeAction(IReadOnlyList<BattleUnitCombatState> states, BattleUnitCombatState state)
    {
        if (state == null || state.IsCombatDisabled)
            return false;

        if (state.CurrentActionType == BattleActionType.EscapeFromPressure)
            return true;

        return GetCurrentEscapeUnitCount(states) < GetMaxEscapeUnitCount(states);
    }

    private static void GetBestActionRespectingEscapeLimit(
        IReadOnlyList<BattleUnitCombatState> states,
        BattleUnitCombatState state,
        BattleActionScoreSet scores,
        BattleActionType excludedAction,
        out BattleActionType bestAction,
        out float bestScore
    )
    {
        bool canEnterEscape = CanEnterEscapeAction(states, state);
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
