// SOT parser output을 후처리하여 actor별 최종 action sequence를 확정한다.
// 순응/거부, fallback, target/action 보정을 수행한다.
// 대사 레이어와 실행 진입점이 읽을 중간 DTO만 만들고 실행 plan은 만들지 않는다.
// advisor/adjustment/refusal 문구는 현재 고정 문자열을 사용한다.

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleCommandPostprocessor
{
    private const string AdvisorLineFallback = "참모 대사를 여기에";
    private const string ObeyedAdjustmentFallback = "순응후 보정 결과 여기에";
    private const string RefusalSummaryFallback = "거부 요약 여기에";

    private const int MaxActionsPerActorFallback = 3;

    public bool TryProcess(
        string originalCommand,
        SotParserOutputDto parserOutput,
        BattleOrderRuntimeContext context,
        out BattleCommandPostprocessResult result,
        out string error
    )
    {
        result = null;
        error = null;

        if (context == null)
        {
            error = "BattleOrderRuntimeContext is null.";
            return false;
        }

        if (parserOutput == null)
        {
            result = CreateFallbackResult(
                originalCommand,
                "parserOutput is null.",
                parserThinking: string.Empty
            );
            return true;
        }

        if (parserOutput.action == null || parserOutput.action.Length == 0)
        {
            result = CreateFallbackResult(
                originalCommand,
                "parserOutput.action is empty.",
                parserOutput.thinking
            );
            return true;
        }

        PostprocessRuntime runtime = new PostprocessRuntime(context, parserOutput);

        List<BattleCommandFinalActorDto> finalActors = new List<BattleCommandFinalActorDto>();
        List<string> droppedActorSummaries = new List<string>();
        List<string> adjustmentSummaries = new List<string>();
        List<string> refusalSummaries = new List<string>();

        for (int i = 0; i < parserOutput.action.Length; i++)
        {
            SotActorActionDto actorAction = parserOutput.action[i];
            if (actorAction == null || string.IsNullOrWhiteSpace(actorAction.unitId))
            {
                droppedActorSummaries.Add("actor action missing unitId.");
                continue;
            }

            string actorId = actorAction.unitId.ToUpperInvariant();
            BattleRuntimeUnit actor = BattleOrderRuntimeQueries.FindUnitById(
                context.Allies,
                context.RosterProjection,
                actorId
            );

            if (!IsValidActor(actor))
            {
                droppedActorSummaries.Add(actorId + " actor invalid.");
                continue;
            }

            if (actorAction.sequence == null || actorAction.sequence.Length == 0)
            {
                droppedActorSummaries.Add(actorId + " sequence empty.");
                continue;
            }

            BattleCommandActionCategory mainCategory = DetermineMainActionCategory(actorAction.sequence);
            bool obey = RollObedience(actor, mainCategory);

            SotFinalActionDto[] candidateSequence;
            if (obey)
            {
                candidateSequence = CloneSequence(actorAction.sequence);
            }
            else
            {
                candidateSequence = BuildFallbackSequence(actor, mainCategory, runtime);
                refusalSummaries.Add(actorId + " refused.");
            }

            bool adjusted;
            SotFinalActionDto[] finalSequence = ValidateAndCorrectSequence(
                actor,
                candidateSequence,
                runtime,
                out adjusted
            );

            if (finalSequence == null || finalSequence.Length == 0)
            {
                SotFinalActionDto[] waitFallback = BuildWaitSequence();
                finalSequence = ValidateAndCorrectSequence(actor, waitFallback, runtime, out bool waitAdjusted);
                adjusted = adjusted || waitAdjusted;
            }

            if (finalSequence == null || finalSequence.Length == 0)
            {
                droppedActorSummaries.Add(actorId + " final sequence empty.");
                continue;
            }

            if (adjusted)
                adjustmentSummaries.Add(actorId + " adjusted.");

            string sourceDialog = ResolveSourceDialog(parserOutput, actorId);

            finalActors.Add(
                new BattleCommandFinalActorDto
                {
                    unitId = actorId,
                    obedienceState = obey ? "obey" : "refuse",
                    mainActionCategory = CategoryToDtoString(mainCategory),
                    sourceDialog = sourceDialog,
                    obeyedActionAdjustment = obey && adjusted ? ObeyedAdjustmentFallback : string.Empty,
                    refusalSummary = obey ? string.Empty : RefusalSummaryFallback,
                    finalActionSequence = finalSequence,
                }
            );
        }

        if (finalActors.Count == 0)
        {
            result = CreateFallbackResult(
                originalCommand,
                "all actor sequences dropped.",
                parserOutput.thinking
            );
            result.debug.droppedActorSummaries = droppedActorSummaries.ToArray();
            result.debug.adjustmentSummaries = adjustmentSummaries.ToArray();
            result.debug.refusalSummaries = refusalSummaries.ToArray();
            return true;
        }

        result = new BattleCommandPostprocessResult
        {
            originalCommand = originalCommand ?? string.Empty,
            fallbackToDefaultMlAi = false,
            advisorLine = string.Empty,
            actors = finalActors.ToArray(),
            debug = new BattleCommandPostprocessDebugDto
            {
                parserThinking = parserOutput.thinking ?? string.Empty,
                droppedActorSummaries = droppedActorSummaries.ToArray(),
                adjustmentSummaries = adjustmentSummaries.ToArray(),
                refusalSummaries = refusalSummaries.ToArray(),
            },
        };

        return true;
    }

    private static BattleCommandPostprocessResult CreateFallbackResult(
        string originalCommand,
        string dropReason,
        string parserThinking
    )
    {
        return new BattleCommandPostprocessResult
        {
            originalCommand = originalCommand ?? string.Empty,
            fallbackToDefaultMlAi = true,
            advisorLine = AdvisorLineFallback,
            actors = Array.Empty<BattleCommandFinalActorDto>(),
            debug = new BattleCommandPostprocessDebugDto
            {
                parserThinking = parserThinking ?? string.Empty,
                droppedActorSummaries = string.IsNullOrWhiteSpace(dropReason)
                    ? Array.Empty<string>()
                    : new[] { dropReason },
                adjustmentSummaries = Array.Empty<string>(),
                refusalSummaries = Array.Empty<string>(),
            },
        };
    }

    private static bool IsValidActor(BattleRuntimeUnit actor)
    {
        return actor != null
            && actor.State != null
            && !actor.IsCombatDisabled
            && !actor.State.IsStunned;
    }

    private static BattleCommandActionCategory DetermineMainActionCategory(SotFinalActionDto[] sequence)
    {
        if (ContainsActionType(sequence, "skill"))
            return BattleCommandActionCategory.Skill;

        if (ContainsActionType(sequence, "skillControl"))
            return BattleCommandActionCategory.SkillControl;

        if (ContainsActionType(sequence, "attack"))
            return BattleCommandActionCategory.Attack;

        BattleCommandActionCategory moveCategory = FindFirstMoveCategory(sequence);
        if (moveCategory != BattleCommandActionCategory.Unknown)
            return moveCategory;

        if (ContainsActionType(sequence, "wait"))
            return BattleCommandActionCategory.Wait;

        return BattleCommandActionCategory.Unknown;
    }

    private static bool ContainsActionType(SotFinalActionDto[] sequence, string type)
    {
        if (sequence == null)
            return false;

        for (int i = 0; i < sequence.Length; i++)
        {
            SotFinalActionDto action = sequence[i];
            if (action == null)
                continue;

            if (string.Equals(action.type, type, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static BattleCommandActionCategory FindFirstMoveCategory(SotFinalActionDto[] sequence)
    {
        if (sequence == null)
            return BattleCommandActionCategory.Unknown;

        for (int i = 0; i < sequence.Length; i++)
        {
            SotFinalActionDto action = sequence[i];
            if (action == null || !string.Equals(action.type, "move", StringComparison.Ordinal))
                continue;

            return MoveSubtypeToCategory(action.subtype);
        }

        return BattleCommandActionCategory.Unknown;
    }

    private static BattleCommandActionCategory MoveSubtypeToCategory(string subtype)
    {
        switch (subtype)
        {
            case "approachOpponent":
                return BattleCommandActionCategory.ApproachOpponent;
            case "escape":
                return BattleCommandActionCategory.Escape;
            case "help":
                return BattleCommandActionCategory.Help;
            case "holdFront":
                return BattleCommandActionCategory.HoldFront;
            default:
                return BattleCommandActionCategory.Unknown;
        }
    }

    private static bool RollObedience(BattleRuntimeUnit actor, BattleCommandActionCategory category)
    {
        int rate = ResolveObedienceRate(actor, category);
        int roll = UnityEngine.Random.Range(0, 100);
        return roll < rate;
    }

    private static int ResolveObedienceRate(BattleRuntimeUnit actor, BattleCommandActionCategory category)
    {
        PersonalitySO personality = ResolvePersonality(actor);
        int index = CategoryToIndex(category);

        if (
            personality != null
            && personality.obedienceRates != null
            && index >= 0
            && index < personality.obedienceRates.Length
        )
        {
            return Mathf.Clamp(personality.obedienceRates[index], 0, 100);
        }

        return personality != null ? Mathf.Clamp(personality.baseLoyalty, 0, 100) : 100;
    }

    private static PersonalitySO ResolvePersonality(BattleRuntimeUnit actor)
    {
        if (actor == null || actor.Snapshot == null)
            return null;

        return actor.Snapshot.Personality;
    }

    private static SotFinalActionDto[] BuildFallbackSequence(
        BattleRuntimeUnit actor,
        BattleCommandActionCategory originalCategory,
        PostprocessRuntime runtime
    )
    {
        PersonalitySO personality = ResolvePersonality(actor);
        int[] weights = personality != null ? personality.fallbackWeights : null;

        bool[] tried = new bool[8];

        for (int attempts = 0; attempts < 8; attempts++)
        {
            BattleCommandActionCategory category = PickFallbackCategory(weights, tried);
            int index = CategoryToIndex(category);
            if (index >= 0)
                tried[index] = true;

            SotFinalActionDto[] sequence = BuildSingleFallbackAction(actor, category, runtime);
            if (sequence != null && sequence.Length > 0)
                return sequence;
        }

        return BuildWaitSequence();
    }

    private static BattleCommandActionCategory PickFallbackCategory(int[] weights, bool[] tried)
    {
        int totalWeight = 0;

        for (int i = 0; i < 8; i++)
        {
            if (tried != null && tried[i])
                continue;

            int weight = weights != null && i < weights.Length ? Mathf.Max(0, weights[i]) : 1;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
            return BattleCommandActionCategory.Wait;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cursor = 0;

        for (int i = 0; i < 8; i++)
        {
            if (tried != null && tried[i])
                continue;

            int weight = weights != null && i < weights.Length ? Mathf.Max(0, weights[i]) : 1;
            if (weight <= 0)
                continue;

            cursor += weight;
            if (roll < cursor)
                return IndexToCategory(i);
        }

        return BattleCommandActionCategory.Wait;
    }

    private static SotFinalActionDto[] BuildSingleFallbackAction(
        BattleRuntimeUnit actor,
        BattleCommandActionCategory category,
        PostprocessRuntime runtime
    )
    {
        switch (category)
        {
            case BattleCommandActionCategory.ApproachOpponent:
                return BuildFallbackApproach(actor, runtime);

            case BattleCommandActionCategory.Escape:
                return BuildFallbackEscape(actor, runtime);

            case BattleCommandActionCategory.Help:
                return BuildFallbackHelp(actor, runtime);

            case BattleCommandActionCategory.HoldFront:
                return BuildFallbackHoldFront(actor, runtime);

            case BattleCommandActionCategory.Attack:
                return BuildFallbackAttack(actor, runtime);

            case BattleCommandActionCategory.Wait:
                return BuildWaitSequence();

            case BattleCommandActionCategory.Skill:
                return BuildFallbackSkill(actor, runtime);

            case BattleCommandActionCategory.SkillControl:
                return BuildFallbackSkillControl(actor);

            default:
                return null;
        }
    }

    private static SotFinalActionDto[] BuildFallbackApproach(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            runtime.Enemies,
            runtime.SimulationManager
        );

        if (target == null)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "move",
                subtype = "approachOpponent",
                movementType = "direct",
                to = runtime.GetUnitId(target),
            }
        );
    }

    private static SotFinalActionDto[] BuildFallbackEscape(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindEligibleBacklineAlly(
            actor,
            runtime.Allies,
            runtime.FormationMap
        );

        if (target == null)
        {
            target = BattleCommandPostprocessRuntimeQueries.FindFarthestLivingAlly(
                actor,
                runtime.Allies
            );
        }

        if (target == null)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "move",
                subtype = "escape",
                movementType = "direct",
                to = runtime.GetUnitId(target),
            }
        );
    }

    private static SotFinalActionDto[] BuildFallbackHelp(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindMostPressuredAlly(
            actor,
            runtime.Allies,
            runtime.Enemies
        );

        if (target == null)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "move",
                subtype = "help",
                movementType = "direct",
                to = runtime.GetUnitId(target),
            }
        );
    }

    private static SotFinalActionDto[] BuildFallbackHoldFront(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindBestHoldFrontAnchor(
            actor,
            runtime.Allies,
            runtime.Enemies,
            runtime.FormationMap,
            runtime.SimulationManager
        );

        if (target == null || target == actor)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "move",
                subtype = "holdFront",
                movementType = "direct",
                to = runtime.GetUnitId(target),
            }
        );
    }

    private static SotFinalActionDto[] BuildFallbackAttack(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            runtime.Enemies,
            runtime.SimulationManager
        );

        if (target == null)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "attack",
                target = runtime.GetUnitId(target),
            }
        );
    }

    private static SotFinalActionDto[] BuildFallbackSkill(
        BattleRuntimeUnit actor,
        PostprocessRuntime runtime
    )
    {
        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (!CanActorUseSkill(actor, metadata))
            return null;

        SotFinalActionDto action = new SotFinalActionDto
        {
            type = "skill",
            description = metadata.skillDescription ?? string.Empty,
        };

        if (metadata.isSkillOnSelf)
        {
            action.target = runtime.GetUnitId(actor);
            return One(action);
        }

        if (metadata.isSkillOnOtherAlly)
        {
            BattleRuntimeUnit target = SelectReplacementOtherAllySkillTarget(
                actor,
                metadata,
                runtime
            );

            if (target == null)
                return null;

            action.target = runtime.GetUnitId(target);
            return One(action);
        }

        BattleRuntimeUnit enemyTarget = SelectReplacementEnemySkillTarget(actor, metadata, runtime);
        if (enemyTarget == null)
            return null;

        action.target = runtime.GetUnitId(enemyTarget);
        return One(action);
    }

    private static SotFinalActionDto[] BuildFallbackSkillControl(BattleRuntimeUnit actor)
    {
        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (metadata.skillId == WeaponSkillId.None)
            return null;

        return One(
            new SotFinalActionDto
            {
                type = "skillControl",
                mode = "forbid",
            }
        );
    }

    private static SotFinalActionDto[] BuildWaitSequence()
    {
        return One(
            new SotFinalActionDto
            {
                type = "wait",
                durationSec = 1f,
            }
        );
    }

    private static SotFinalActionDto[] ValidateAndCorrectSequence(
        BattleRuntimeUnit actor,
        SotFinalActionDto[] candidateSequence,
        PostprocessRuntime runtime,
        out bool adjusted
    )
    {
        adjusted = false;

        if (actor == null || candidateSequence == null || candidateSequence.Length == 0)
            return Array.Empty<SotFinalActionDto>();

        int maxActions = ResolveMaxActionsPerActor(runtime.Context);
        List<SotFinalActionDto> finalActions = new List<SotFinalActionDto>(maxActions);

        for (int i = 0; i < candidateSequence.Length && finalActions.Count < maxActions; i++)
        {
            SotFinalActionDto candidate = candidateSequence[i];
            if (candidate == null)
                continue;

            if (TryCorrectAction(actor, candidate, runtime, out SotFinalActionDto corrected, out bool actionAdjusted))
            {
                if (corrected != null)
                {
                    finalActions.Add(corrected);
                    adjusted = adjusted || actionAdjusted;
                }
            }
        }

        return finalActions.ToArray();
    }

    private static int ResolveMaxActionsPerActor(BattleOrderRuntimeContext context)
    {
        return MaxActionsPerActorFallback;
    }

    private static bool TryCorrectAction(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        corrected = null;
        adjusted = false;

        switch (candidate.type)
        {
            case "attack":
                return TryCorrectAttack(actor, candidate, runtime, out corrected, out adjusted);

            case "move":
                return TryCorrectMove(actor, candidate, runtime, out corrected, out adjusted);

            case "skill":
                return TryCorrectSkill(actor, candidate, runtime, out corrected, out adjusted);

            case "wait":
                return TryCorrectWait(candidate, out corrected, out adjusted);

            case "skillControl":
                return TryCorrectSkillControl(actor, candidate, out corrected, out adjusted);

            default:
                return false;
        }
    }

    private static bool TryCorrectAttack(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        adjusted = false;
        corrected = CloneAction(candidate);

        BattleRuntimeUnit target = runtime.FindEnemy(candidate.target);
        if (BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, runtime.SimulationManager))
            return true;

        BattleRuntimeUnit replacement = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            runtime.Enemies,
            runtime.SimulationManager
        );

        if (replacement == null && target != null)
        {
            replacement = BattleCommandPostprocessRuntimeQueries.FindTargetableEnemyClosestToPosition(
                target.Position,
                runtime.Enemies,
                runtime.SimulationManager
            );
        }

        if (replacement == null)
        {
            replacement = BattleCommandPostprocessRuntimeQueries.FindLowestHpTargetableEnemy(
                runtime.Enemies,
                runtime.SimulationManager
            );
        }

        if (replacement == null)
        {
            replacement = BattleCommandPostprocessRuntimeQueries.FindEnemyAlreadyEngagedWithActor(
                actor,
                runtime.Enemies,
                runtime.SimulationManager
            );
        }

        if (replacement == null)
            return false;

        corrected.target = runtime.GetUnitId(replacement);
        adjusted = true;
        return true;
    }

    private static bool TryCorrectMove(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        corrected = CloneAction(candidate);

        bool movementTypeAdjusted = false;
        if (!IsAllowedMovementType(corrected.movementType))
        {
            corrected.movementType = "direct";
            movementTypeAdjusted = true;
        }

        bool valid;
        bool subtypeAdjusted;

        switch (corrected.subtype)
        {
            case "approachOpponent":
                valid = CorrectMoveApproach(actor, corrected, runtime, out subtypeAdjusted);
                break;

            case "escape":
                valid = CorrectMoveEscape(actor, corrected, runtime, out subtypeAdjusted);
                break;

            case "help":
                valid = CorrectMoveHelp(actor, corrected, runtime, out subtypeAdjusted);
                break;

            case "holdFront":
                valid = CorrectMoveHoldFront(actor, corrected, runtime, out subtypeAdjusted);
                break;

            default:
                adjusted = movementTypeAdjusted;
                return false;
        }

        adjusted = movementTypeAdjusted || subtypeAdjusted;
        return valid;
    }

    private static bool CorrectMoveApproach(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted
    )
    {
        adjusted = false;
        BattleRuntimeUnit target = runtime.FindEnemy(corrected.to);

        if (BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, runtime.SimulationManager))
            return true;

        BattleRuntimeUnit replacement = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            runtime.Enemies,
            runtime.SimulationManager
        );

        if (replacement == null)
            return false;

        corrected.to = runtime.GetUnitId(replacement);
        adjusted = true;
        return true;
    }

    private static bool CorrectMoveEscape(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted
    )
    {
        adjusted = false;
        BattleRuntimeUnit target = runtime.FindAlly(corrected.to);

        if (BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
            return true;

        BattleRuntimeUnit replacement = BattleCommandPostprocessRuntimeQueries.FindEligibleBacklineAlly(
            actor,
            runtime.Allies,
            runtime.FormationMap
        );

        if (replacement == null)
        {
            replacement = BattleCommandPostprocessRuntimeQueries.FindFarthestLivingAlly(
                actor,
                runtime.Allies
            );
        }

        if (replacement == null)
            return false;

        corrected.to = runtime.GetUnitId(replacement);
        adjusted = true;
        return true;
    }

    private static bool CorrectMoveHelp(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted
    )
    {
        adjusted = false;
        BattleRuntimeUnit target = runtime.FindAlly(corrected.to);

        if (BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
            return true;

        BattleRuntimeUnit replacement = BattleCommandPostprocessRuntimeQueries.FindMostPressuredAlly(
            actor,
            runtime.Allies,
            runtime.Enemies
        );

        if (replacement == null)
            return false;

        corrected.to = runtime.GetUnitId(replacement);
        adjusted = true;
        return true;
    }

    private static bool CorrectMoveHoldFront(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted
    )
    {
        adjusted = false;
        BattleRuntimeUnit target = runtime.FindAnyUnit(corrected.to);

        if (
            target != null
            && target != actor
            && BattleCommandPostprocessRuntimeQueries.IsHoldFrontAnchorValid(
                target,
                runtime.FormationMap,
                runtime.SimulationManager
            )
        )
        {
            return true;
        }

        BattleRuntimeUnit replacement = BattleCommandPostprocessRuntimeQueries.FindBestHoldFrontAnchor(
            actor,
            runtime.Allies,
            runtime.Enemies,
            runtime.FormationMap,
            runtime.SimulationManager
        );

        if (replacement == null || replacement == actor)
            return false;

        corrected.to = runtime.GetUnitId(replacement);
        adjusted = true;
        return true;
    }

    private static bool TryCorrectSkill(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        adjusted = false;
        corrected = CloneAction(candidate);

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (!CanActorUseSkill(actor, metadata))
            return false;

        if (!string.Equals(corrected.description, metadata.skillDescription, StringComparison.Ordinal))
        {
            corrected.description = metadata.skillDescription ?? string.Empty;
            adjusted = true;
        }

        if (metadata.isSkillOnSelf)
        {
            string actorId = runtime.GetUnitId(actor);
            if (!string.Equals(corrected.target, actorId, StringComparison.Ordinal))
            {
                corrected.target = actorId;
                adjusted = true;
            }

            return true;
        }

        if (metadata.isSkillOnOtherAlly)
        {
            BattleRuntimeUnit target = runtime.FindAlly(corrected.target);
            bool valid =
                BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target)
                || (
                    metadata.canSkillTargetDead
                    && BattleCommandPostprocessRuntimeQueries.IsValidDeadAllyTarget(actor, target)
                );

            if (valid)
                return true;

            BattleRuntimeUnit replacement = SelectReplacementOtherAllySkillTarget(actor, metadata, runtime);
            if (replacement == null)
                return false;

            corrected.target = runtime.GetUnitId(replacement);
            adjusted = true;
            return true;
        }

        BattleRuntimeUnit enemyTarget = runtime.FindEnemy(corrected.target);
        if (
            BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(
                enemyTarget,
                runtime.SimulationManager
            )
        )
        {
            return true;
        }

        BattleRuntimeUnit enemyReplacement = SelectReplacementEnemySkillTarget(actor, metadata, runtime);
        if (enemyReplacement == null)
            return false;

        corrected.target = runtime.GetUnitId(enemyReplacement);
        adjusted = true;
        return true;
    }

    private static BattleRuntimeUnit SelectReplacementOtherAllySkillTarget(
        BattleRuntimeUnit actor,
        BattleSkillRuntimeMetadata metadata,
        PostprocessRuntime runtime
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindLowestHpLivingAlly(
            actor,
            runtime.Allies
        );

        if (target != null)
            return target;

        target = BattleCommandPostprocessRuntimeQueries.FindMostPressuredAlly(
            actor,
            runtime.Allies,
            runtime.Enemies
        );

        if (target != null)
            return target;

        if (metadata.canSkillTargetDead)
        {
            target = BattleCommandPostprocessRuntimeQueries.FindDeadAllyTarget(
                actor,
                runtime.Allies
            );
        }

        return target;
    }

    private static BattleRuntimeUnit SelectReplacementEnemySkillTarget(
        BattleRuntimeUnit actor,
        BattleSkillRuntimeMetadata metadata,
        PostprocessRuntime runtime
    )
    {
        if (metadata.isSkillAoe)
        {
            BattleRuntimeUnit aoeTarget = BattleCommandPostprocessRuntimeQueries.FindBestAoeCenterEnemy(
                actor,
                runtime.Enemies,
                runtime.SimulationManager
            );

            if (aoeTarget != null)
                return aoeTarget;
        }

        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            runtime.Enemies,
            runtime.SimulationManager
        );

        if (target != null)
            return target;

        return BattleCommandPostprocessRuntimeQueries.FindLowestHpTargetableEnemy(
            runtime.Enemies,
            runtime.SimulationManager
        );
    }

    private static bool TryCorrectWait(
        SotFinalActionDto candidate,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        corrected = CloneAction(candidate);
        float originalDuration = corrected.durationSec.HasValue ? corrected.durationSec.Value : 1f;
        float clampedDuration = Mathf.Clamp(originalDuration, 1f, 10f);
        adjusted = !Mathf.Approximately(originalDuration, clampedDuration);
        corrected.durationSec = clampedDuration;
        return true;
    }

    private static bool TryCorrectSkillControl(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        out SotFinalActionDto corrected,
        out bool adjusted
    )
    {
        adjusted = false;
        corrected = CloneAction(candidate);

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (metadata.skillId == WeaponSkillId.None)
            return false;

        if (corrected.mode == "defer")
        {
            float originalDuration = corrected.durationSec.HasValue ? corrected.durationSec.Value : 1f;
            float clampedDuration = Mathf.Clamp(originalDuration, 1f, 10f);
            adjusted = !Mathf.Approximately(originalDuration, clampedDuration);
            corrected.durationSec = clampedDuration;
            return true;
        }

        if (corrected.mode == "forbid")
        {
            corrected.durationSec = null;
            return true;
        }

        corrected.mode = "forbid";
        corrected.durationSec = null;
        adjusted = true;
        return true;
    }

    private static bool CanActorUseSkill(
        BattleRuntimeUnit actor,
        BattleSkillRuntimeMetadata metadata
    )
    {
        return actor != null
            && actor.State != null
            && metadata.skillId != WeaponSkillId.None
            && !actor.State.IsSkillDisabled
            && actor.State.SkillCooldownRemaining <= 0f;
    }

    private static bool IsAllowedMovementType(string movementType)
    {
        return movementType == "direct" || movementType == "flank";
    }

    private static SotFinalActionDto[] CloneSequence(SotFinalActionDto[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<SotFinalActionDto>();

        SotFinalActionDto[] clone = new SotFinalActionDto[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            clone[i] = CloneAction(source[i]);
        }

        return clone;
    }

    private static SotFinalActionDto CloneAction(SotFinalActionDto source)
    {
        if (source == null)
            return null;

        return new SotFinalActionDto
        {
            type = source.type,
            subtype = source.subtype,
            movementType = source.movementType,
            to = source.to,
            target = source.target,
            description = source.description,
            mode = source.mode,
            durationSec = source.durationSec,
        };
    }

    private static SotFinalActionDto[] One(SotFinalActionDto action)
    {
        if (action == null)
            return Array.Empty<SotFinalActionDto>();

        return new[] { action };
    }

    private static string ResolveSourceDialog(SotParserOutputDto parserOutput, string unitId)
    {
        if (
            parserOutput == null
            || parserOutput.dialog == null
            || string.IsNullOrWhiteSpace(unitId)
        )
        {
            return "명령을 확인했다.";
        }

        for (int i = 0; i < parserOutput.dialog.Length; i++)
        {
            SotDialogLineDto dialog = parserOutput.dialog[i];
            if (dialog == null)
                continue;

            if (string.Equals(dialog.unitId, unitId, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(dialog.text)
                    ? "명령을 확인했다."
                    : dialog.text;
            }
        }

        return "명령을 확인했다.";
    }

    private static int CategoryToIndex(BattleCommandActionCategory category)
    {
        switch (category)
        {
            case BattleCommandActionCategory.ApproachOpponent:
                return 0;
            case BattleCommandActionCategory.Escape:
                return 1;
            case BattleCommandActionCategory.Help:
                return 2;
            case BattleCommandActionCategory.HoldFront:
                return 3;
            case BattleCommandActionCategory.Attack:
                return 4;
            case BattleCommandActionCategory.Wait:
                return 5;
            case BattleCommandActionCategory.Skill:
                return 6;
            case BattleCommandActionCategory.SkillControl:
                return 7;
            default:
                return -1;
        }
    }

    private static BattleCommandActionCategory IndexToCategory(int index)
    {
        switch (index)
        {
            case 0:
                return BattleCommandActionCategory.ApproachOpponent;
            case 1:
                return BattleCommandActionCategory.Escape;
            case 2:
                return BattleCommandActionCategory.Help;
            case 3:
                return BattleCommandActionCategory.HoldFront;
            case 4:
                return BattleCommandActionCategory.Attack;
            case 5:
                return BattleCommandActionCategory.Wait;
            case 6:
                return BattleCommandActionCategory.Skill;
            case 7:
                return BattleCommandActionCategory.SkillControl;
            default:
                return BattleCommandActionCategory.Wait;
        }
    }

    private static string CategoryToDtoString(BattleCommandActionCategory category)
    {
        switch (category)
        {
            case BattleCommandActionCategory.ApproachOpponent:
                return "approachOpponent";
            case BattleCommandActionCategory.Escape:
                return "escape";
            case BattleCommandActionCategory.Help:
                return "help";
            case BattleCommandActionCategory.HoldFront:
                return "holdFront";
            case BattleCommandActionCategory.Attack:
                return "attack";
            case BattleCommandActionCategory.Wait:
                return "wait";
            case BattleCommandActionCategory.Skill:
                return "skill";
            case BattleCommandActionCategory.SkillControl:
                return "skillControl";
            default:
                return "wait";
        }
    }

    private enum BattleCommandActionCategory
    {
        Unknown = -1,
        ApproachOpponent = 0,
        Escape = 1,
        Help = 2,
        HoldFront = 3,
        Attack = 4,
        Wait = 5,
        Skill = 6,
        SkillControl = 7,
    }

    private sealed class PostprocessRuntime
    {
        public readonly BattleOrderRuntimeContext Context;
        public readonly BattleRuntimeUnit[] Allies;
        public readonly BattleRuntimeUnit[] Enemies;
        public readonly IBattleRosterProjection Roster;
        public readonly BattleSimulationManager SimulationManager;
        public readonly Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> FormationMap;

        public PostprocessRuntime(BattleOrderRuntimeContext context, SotParserOutputDto parserOutput)
        {
            Context = context;
            Allies = context.Allies ?? Array.Empty<BattleRuntimeUnit>();
            Enemies = context.Enemies ?? Array.Empty<BattleRuntimeUnit>();
            Roster = context.RosterProjection;
            SimulationManager = context.SimulationManager;
            FormationMap = BattleOrderRuntimeQueries.BuildFormationInfoMap(Allies, Enemies);
        }

        public BattleRuntimeUnit FindAlly(string unitId)
        {
            return BattleOrderRuntimeQueries.FindUnitById(Allies, Roster, unitId);
        }

        public BattleRuntimeUnit FindEnemy(string unitId)
        {
            return BattleOrderRuntimeQueries.FindUnitById(Enemies, Roster, unitId);
        }

        public BattleRuntimeUnit FindAnyUnit(string unitId)
        {
            BattleRuntimeUnit unit = FindAlly(unitId);
            if (unit != null)
                return unit;

            return FindEnemy(unitId);
        }

        public string GetUnitId(BattleRuntimeUnit unit)
        {
            return BattleOrderRuntimeQueries.GetUnitId(unit, Roster);
        }
    }
}
