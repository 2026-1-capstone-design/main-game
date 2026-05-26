// SOT parser output을 후처리하여 actor별 최종 action sequence를 확정한다.
// 순응/거부, fallback, target/action 보정을 수행한다.
// 대사 레이어와 실행 진입점이 읽을 중간 DTO만 만들고 실행 plan은 만들지 않는다.
// 대사 레이어 입력용 설명 문자열은 BattleCommandInputDialogBuilder가 생성한다.

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleCommandPostprocessor
{
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
                BattleInputDialogAdvisorReason.ParserOutputNull,
                "parserOutput is null.",
                parserThinking: string.Empty
            );
            return true;
        }

        if (parserOutput.action == null || parserOutput.action.Length == 0)
        {
            result = CreateFallbackResult(
                originalCommand,
                BattleInputDialogAdvisorReason.ParserActionEmpty,
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
                droppedActorSummaries.Add(
                    BattleCommandInputDialogBuilder.BuildActorDropSummary(
                        string.Empty,
                        BattleInputDialogActorDropReason.MissingUnitId
                    )
                );
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
                droppedActorSummaries.Add(
                    BattleCommandInputDialogBuilder.BuildActorDropSummary(
                        actorId,
                        ResolveInvalidActorDropReason(actor)
                    )
                );
                continue;
            }

            if (actorAction.sequence == null || actorAction.sequence.Length == 0)
            {
                droppedActorSummaries.Add(
                    BattleCommandInputDialogBuilder.BuildActorDropSummary(
                        actorId,
                        BattleInputDialogActorDropReason.SequenceEmpty
                    )
                );
                continue;
            }

            BattleCommandActionCategory mainCategory = DetermineMainActionCategory(actorAction.sequence);
            bool obey = RollObedience(actor, mainCategory);

            SotFinalActionDto[] candidateSequence;
            string refusalSummary = string.Empty;
            bool refused = !obey;
            BattleCommandActionCategory fallbackCategory = BattleCommandActionCategory.Unknown;

            if (obey)
            {
                candidateSequence = CloneSequence(actorAction.sequence);
            }
            else
            {
                candidateSequence = BuildFallbackSequence(actor, mainCategory, runtime);
                fallbackCategory = DetermineMainActionCategory(candidateSequence);
            }

            bool adjusted;
            List<string> actorAdjustmentLines;
            SotFinalActionDto[] finalSequence = ValidateAndCorrectSequence(
                actor,
                candidateSequence,
                runtime,
                out adjusted,
                out actorAdjustmentLines
            );

            if (finalSequence == null || finalSequence.Length == 0)
            {
                SotFinalActionDto[] waitFallback = BuildWaitSequence();
                finalSequence = ValidateAndCorrectSequence(
                    actor,
                    waitFallback,
                    runtime,
                    out bool waitAdjusted,
                    out List<string> waitAdjustmentLines
                );

                adjusted = adjusted || waitAdjusted;

                if (waitAdjustmentLines != null && waitAdjustmentLines.Count > 0)
                    actorAdjustmentLines.AddRange(waitAdjustmentLines);

                if (finalSequence != null && finalSequence.Length > 0)
                {
                    actorAdjustmentLines.Add(
                        BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                            BattleInputDialogAdjustmentReason.FallbackWaitApplied,
                            CategoryToDtoString(mainCategory),
                            string.Empty,
                            string.Empty
                        )
                    );
                    adjusted = true;
                }
            }

            if (finalSequence == null || finalSequence.Length == 0)
            {
                droppedActorSummaries.Add(
                    BattleCommandInputDialogBuilder.BuildActorDropSummary(
                        actorId,
                        BattleInputDialogActorDropReason.FinalSequenceEmpty
                    )
                );
                continue;
            }

            if (refused)
            {
                fallbackCategory = DetermineMainActionCategory(finalSequence);
                BattleInputDialogRefusalReason refusalReason =
                    fallbackCategory == BattleCommandActionCategory.Wait
                        ? BattleInputDialogRefusalReason.FallbackWaitApplied
                        : BattleInputDialogRefusalReason.ObedienceRollFailed;

                refusalSummary = BattleCommandInputDialogBuilder.BuildRefusalSummary(
                    refusalReason,
                    CategoryToDtoString(mainCategory),
                    CategoryToDtoString(fallbackCategory),
                    finalSequence,
                    actorId
                );

                refusalSummaries.Add(actorId + " " + refusalSummary);
            }

            string obeyedActionAdjustment =
                obey && adjusted
                    ? BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(actorAdjustmentLines)
                    : string.Empty;

            if (adjusted && !string.IsNullOrWhiteSpace(obeyedActionAdjustment))
                adjustmentSummaries.Add(actorId + " " + obeyedActionAdjustment);

            string sourceDialog = ResolveSourceDialog(parserOutput, actorId);
            finalActors.Add(
                new BattleCommandFinalActorDto
                {
                    unitId = actorId,
                    obedienceState = obey ? "obey" : "refuse",
                    mainActionCategory = CategoryToDtoString(mainCategory),
                    sourceDialog = sourceDialog,
                    obeyedActionAdjustment = obeyedActionAdjustment,
                    refusalSummary = obey ? string.Empty : refusalSummary,
                    finalActionSequence = finalSequence,
                }
            );
        }

        if (finalActors.Count == 0)
        {
            result = CreateFallbackResult(
                originalCommand,
                BattleInputDialogAdvisorReason.AllActorsDropped,
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
        BattleInputDialogAdvisorReason advisorReason,
        string dropReason,
        string parserThinking
    )
    {
        return new BattleCommandPostprocessResult
        {
            originalCommand = originalCommand ?? string.Empty,
            fallbackToDefaultMlAi = true,
            advisorLine = BattleCommandInputDialogBuilder.BuildAdvisorLine(advisorReason),
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

    private static BattleInputDialogTargetIssue ResolveTargetIssue(
    BattleRuntimeUnit target,
    BattleSimulationManager simulationManager
    )
    {
        if (target == null || target.State == null)
            return BattleInputDialogTargetIssue.TargetNotFoundOrUntargetable;

        if (target.IsCombatDisabled || !BattleOrderRuntimeQueries.IsAlive(target))
            return BattleInputDialogTargetIssue.TargetDead;

        if (BattleOrderRuntimeQueries.HasTargetingBlock(target, simulationManager))
            return BattleInputDialogTargetIssue.TargetNotFoundOrUntargetable;

        return BattleInputDialogTargetIssue.Unknown;
    }

    private static bool IsValidActor(BattleRuntimeUnit actor)
    {
        return actor != null
            && actor.State != null
            && !actor.IsCombatDisabled
            && !actor.State.IsStunned;
    }

    private static BattleInputDialogActorDropReason ResolveInvalidActorDropReason(BattleRuntimeUnit actor)
    {
        if (actor == null || actor.State == null)
            return BattleInputDialogActorDropReason.ActorNotFound;

        if (actor.IsCombatDisabled)
            return BattleInputDialogActorDropReason.ActorDead;

        if (actor.State.IsStunned)
            return BattleInputDialogActorDropReason.ActorStunned;

        return BattleInputDialogActorDropReason.Unknown;
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
        int originalIndex = CategoryToIndex(originalCategory);
        if (originalIndex >= 0 && originalIndex < tried.Length)
            tried[originalIndex] = true;

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

        return One(
            new SotFinalActionDto
            {
                type = "move",
                subtype = "escape",
                movementType = "direct",
                to = target != null ? runtime.GetUnitId(target) : null,
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

        if (target == null)
            target = actor;

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
        out bool adjusted,
        out List<string> adjustmentLines
    )
    {
        adjusted = false;
        adjustmentLines = new List<string>();

        if (actor == null || candidateSequence == null || candidateSequence.Length == 0)
            return Array.Empty<SotFinalActionDto>();

        int maxActions = ResolveMaxActionsPerActor(runtime.Context);
        List<SotFinalActionDto> finalActions = new List<SotFinalActionDto>(maxActions);

        for (int i = 0; i < candidateSequence.Length && finalActions.Count < maxActions; i++)
        {
            SotFinalActionDto candidate = candidateSequence[i];
            if (candidate == null)
                continue;

            if (
                TryCorrectAction(
                    actor,
                    candidate,
                    runtime,
                    out SotFinalActionDto corrected,
                    out bool actionAdjusted,
                    out string actionAdjustmentLine
                )
            )
            {
                if (corrected != null)
                {
                    finalActions.Add(corrected);
                    adjusted = adjusted || actionAdjusted;

                    if (!string.IsNullOrWhiteSpace(actionAdjustmentLine))
                        adjustmentLines.Add(actionAdjustmentLine);
                }
            }
        }

        if (candidateSequence.Length > maxActions)
        {
            adjusted = true;
            adjustmentLines.Add(
                BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.SequenceTrimmed,
                    string.Empty,
                    string.Empty,
                    string.Empty
                )
            );
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
        out bool adjusted,
        out string adjustmentLine
    )
    {
        corrected = null;
        adjusted = false;
        adjustmentLine = string.Empty;

        switch (candidate.type)
        {
            case "attack":
                return TryCorrectAttack(actor, candidate, runtime, out corrected, out adjusted, out adjustmentLine);

            case "move":
                return TryCorrectMove(actor, candidate, runtime, out corrected, out adjusted, out adjustmentLine);

            case "skill":
                return TryCorrectSkill(actor, candidate, runtime, out corrected, out adjusted, out adjustmentLine);

            case "wait":
                return TryCorrectWait(candidate, out corrected, out adjusted, out adjustmentLine);

            case "skillControl":
                return TryCorrectSkillControl(actor, candidate, out corrected, out adjusted, out adjustmentLine);

            default:
                return false;
        }
    }

    private static bool TryCorrectAttack(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
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

        string originalTargetId = candidate.target;
        string replacementTargetId = runtime.GetUnitId(replacement);

        corrected.target = replacementTargetId;
        adjusted = true;
        adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.AttackTargetReplaced,
            "attack",
            originalTargetId,
            replacementTargetId,
            ResolveTargetIssue(target, runtime.SimulationManager),
            runtime.GetUnitId(actor)
        );
        return true;
    }

    private static bool TryCorrectMove(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        corrected = CloneAction(candidate);
        adjustmentLine = string.Empty;
        List<string> lines = new List<string>();

        bool movementTypeAdjusted = false;
        if (!IsAllowedMovementType(corrected.movementType))
        {
            corrected.movementType = "direct";
            movementTypeAdjusted = true;
            lines.Add(
                BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.MovementTypeDefaulted,
                    corrected.subtype,
                    string.Empty,
                    string.Empty
                )
            );
        }

        if (corrected.subtype == "help" && corrected.movementType != "direct")
        {
            corrected.movementType = "direct";
            movementTypeAdjusted = true;
            lines.Add(
                BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.MovementTypeDefaulted,
                    corrected.subtype,
                    string.Empty,
                    string.Empty
                )
            );
        }

        bool valid;
        bool subtypeAdjusted;
        string subtypeAdjustmentLine;

        switch (corrected.subtype)
        {
            case "approachOpponent":
                valid = CorrectMoveApproach(
                    actor,
                    corrected,
                    runtime,
                    out subtypeAdjusted,
                    out subtypeAdjustmentLine
                );
                break;

            case "escape":
                valid = CorrectMoveEscape(
                    actor,
                    corrected,
                    runtime,
                    out subtypeAdjusted,
                    out subtypeAdjustmentLine
                );
                break;

            case "help":
                valid = CorrectMoveHelp(
                    actor,
                    corrected,
                    runtime,
                    out subtypeAdjusted,
                    out subtypeAdjustmentLine
                );
                break;

            case "holdFront":
                valid = CorrectMoveHoldFront(
                    actor,
                    corrected,
                    runtime,
                    out subtypeAdjusted,
                    out subtypeAdjustmentLine
                );
                break;

            default:
                adjusted = movementTypeAdjusted;
                adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(lines);
                return false;
        }

        adjusted = movementTypeAdjusted || subtypeAdjusted;

        if (!string.IsNullOrWhiteSpace(subtypeAdjustmentLine))
            lines.Add(subtypeAdjustmentLine);

        adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(lines);
        return valid;
    }

    private static bool CorrectMoveApproach(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
        string originalTo = corrected.to;

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

        string replacementTo = runtime.GetUnitId(replacement);
        corrected.to = replacementTo;
        adjusted = true;
        adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.MoveTargetReplaced,
            corrected.subtype,
            originalTo,
            replacementTo,
            ResolveTargetIssue(target, runtime.SimulationManager),
            runtime.GetUnitId(actor)
        );
        return true;
    }

    private static bool CorrectMoveEscape(
    BattleRuntimeUnit actor,
    SotFinalActionDto corrected,
    PostprocessRuntime runtime,
    out bool adjusted,
    out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
        string originalTo = corrected.to;

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
        {
            corrected.to = null;
            adjusted = !string.IsNullOrWhiteSpace(originalTo);
            if (adjusted)
            {
                adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.MoveTargetReplaced,
                    corrected.subtype,
                    originalTo,
                    string.Empty,
                    ResolveTargetIssue(target, runtime.SimulationManager),
                    runtime.GetUnitId(actor)
                );
            }

            return true;
        }

        string replacementTo = runtime.GetUnitId(replacement);
        corrected.to = replacementTo;
        adjusted = true;
        adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.MoveTargetReplaced,
            corrected.subtype,
            originalTo,
            replacementTo,
            ResolveTargetIssue(target, runtime.SimulationManager),
            runtime.GetUnitId(actor)
        );
        return true;
    }

    private static bool CorrectMoveHelp(
    BattleRuntimeUnit actor,
    SotFinalActionDto corrected,
    PostprocessRuntime runtime,
    out bool adjusted,
    out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
        string originalTo = corrected.to;

        if (corrected.movementType != "direct")
        {
            corrected.movementType = "direct";
            adjusted = true;
            adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                BattleInputDialogAdjustmentReason.MovementTypeDefaulted,
                corrected.subtype,
                string.Empty,
                string.Empty
            );
        }

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

        string replacementTo = runtime.GetUnitId(replacement);
        corrected.to = replacementTo;
        adjusted = true;

        string targetAdjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.MoveTargetReplaced,
            corrected.subtype,
            originalTo,
            replacementTo,
            ResolveTargetIssue(target, runtime.SimulationManager),
            runtime.GetUnitId(actor)
        );

        if (string.IsNullOrWhiteSpace(adjustmentLine))
            adjustmentLine = targetAdjustmentLine;
        else if (!string.IsNullOrWhiteSpace(targetAdjustmentLine))
            adjustmentLine = adjustmentLine + " " + targetAdjustmentLine;

        return true;
    }

    private static bool CorrectMoveHoldFront(
        BattleRuntimeUnit actor,
        SotFinalActionDto corrected,
        PostprocessRuntime runtime,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
        string originalTo = corrected.to;

        BattleRuntimeUnit target = runtime.FindAnyUnit(corrected.to);

        if (target == actor)
            return true;

        if (
            target != null
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

        if (replacement == null)
            replacement = actor;

        string replacementTo = runtime.GetUnitId(replacement);
        corrected.to = replacementTo;
        adjusted = true;
        adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.MoveTargetReplaced,
            corrected.subtype,
            originalTo,
            replacementTo,
            ResolveTargetIssue(target, runtime.SimulationManager),
            runtime.GetUnitId(actor)
        );
        return true;
    }

    private static bool TryCorrectSkill(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        PostprocessRuntime runtime,
        out SotFinalActionDto corrected,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
        corrected = CloneAction(candidate);
        List<string> adjustmentLines = new List<string>();

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (!CanActorUseSkill(actor, metadata))
            return false;

        if (!string.Equals(corrected.description, metadata.skillDescription, StringComparison.Ordinal))
        {
            corrected.description = metadata.skillDescription ?? string.Empty;
            adjusted = true;
            adjustmentLines.Add(
                BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.SkillDescriptionCorrected,
                    "skill",
                    string.Empty,
                    string.Empty
                )
            );
        }

        if (metadata.isSkillOnSelf)
        {
            string actorId = runtime.GetUnitId(actor);
            if (!string.Equals(corrected.target, actorId, StringComparison.Ordinal))
            {
                corrected.target = actorId;
                adjusted = true;
                adjustmentLines.Add(
                    BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                        BattleInputDialogAdjustmentReason.SkillTargetReplaced,
                        "skill",
                        candidate.target,
                        actorId,
                        BattleInputDialogTargetIssue.SkillTargetMismatch,
                        actorId
                    )
                );
            }

            adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(adjustmentLines);
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
            {
                adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(adjustmentLines);
                return true;
            }

            BattleRuntimeUnit replacement = SelectReplacementOtherAllySkillTarget(actor, metadata, runtime);
            if (replacement == null)
                return false;

            string replacementTargetId = runtime.GetUnitId(replacement);
            corrected.target = replacementTargetId;
            adjusted = true;
            adjustmentLines.Add(
                BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                    BattleInputDialogAdjustmentReason.SkillTargetReplaced,
                    "skill",
                    candidate.target,
                    replacementTargetId,
                    ResolveTargetIssue(target, runtime.SimulationManager),
                    runtime.GetUnitId(actor)
                )
            );
            adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(adjustmentLines);
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
            adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(adjustmentLines);
            return true;
        }

        BattleRuntimeUnit enemyReplacement = SelectReplacementEnemySkillTarget(actor, metadata, runtime);
        if (enemyReplacement == null)
            return false;

        string enemyReplacementTargetId = runtime.GetUnitId(enemyReplacement);
        corrected.target = enemyReplacementTargetId;
        adjusted = true;
        adjustmentLines.Add(
            BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
                BattleInputDialogAdjustmentReason.SkillTargetReplaced,
                "skill",
                candidate.target,
                enemyReplacementTargetId,
                ResolveTargetIssue(enemyTarget, runtime.SimulationManager),
                runtime.GetUnitId(actor)
            )
        );
        adjustmentLine = BattleCommandInputDialogBuilder.BuildCombinedObeyedActionAdjustment(adjustmentLines);
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
        out bool adjusted,
        out string adjustmentLine
    )
    {
        corrected = CloneAction(candidate);
        float originalDuration = corrected.durationSec.HasValue ? corrected.durationSec.Value : 1f;
        float clampedDuration = Mathf.Clamp(originalDuration, 1f, 10f);
        adjusted = !Mathf.Approximately(originalDuration, clampedDuration);
        corrected.durationSec = clampedDuration;

        adjustmentLine = adjusted
            ? BattleCommandInputDialogBuilder.BuildDurationClampSummary(
                "wait",
                originalDuration,
                clampedDuration
            )
            : string.Empty;

        return true;
    }

    private static bool TryCorrectSkillControl(
        BattleRuntimeUnit actor,
        SotFinalActionDto candidate,
        out SotFinalActionDto corrected,
        out bool adjusted,
        out string adjustmentLine
    )
    {
        adjusted = false;
        adjustmentLine = string.Empty;
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

            adjustmentLine = adjusted
                ? BattleCommandInputDialogBuilder.BuildDurationClampSummary(
                    "skillControl",
                    originalDuration,
                    clampedDuration
                )
                : string.Empty;

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
        adjustmentLine = BattleCommandInputDialogBuilder.BuildObeyedActionAdjustment(
            BattleInputDialogAdjustmentReason.SkillControlModeCorrected,
            "skillControl",
            string.Empty,
            string.Empty
        );
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
