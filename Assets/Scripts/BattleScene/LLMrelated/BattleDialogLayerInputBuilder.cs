// 대사 레이어에 넘길 입력을 만든다.
// 대사 레이어는 finalActionSequence를 수정하지 않는다.
// 후처리 결과가 있으면 확정된 actor sequence만 대사 레이어 입력에 반영한다.
// mock parser 직접 입력 경로는 preview 호환용으로 유지한다.

using System.Collections.Generic;

public sealed class BattleDialogLayerInputBuilder
{
    public SotDialogLayerRequestDto BuildFromPostprocessResult(
        BattleCommandPostprocessResult postprocessResult,
        BattleOrderRuntimeContext context
    )
    {
        if (postprocessResult == null || postprocessResult.fallbackToDefaultMlAi)
        {
            return new SotDialogLayerRequestDto
            {
                originalCommand = postprocessResult != null ? postprocessResult.originalCommand : string.Empty,
                actors = System.Array.Empty<SotDialogActorInputDto>(),
            };
        }

        List<SotDialogActorInputDto> actors = new List<SotDialogActorInputDto>();

        BattleCommandFinalActorDto[] finalActors =
            postprocessResult.actors ?? System.Array.Empty<BattleCommandFinalActorDto>();

        for (int i = 0; i < finalActors.Length; i++)
        {
            BattleCommandFinalActorDto finalActor = finalActors[i];
            if (
                finalActor == null
                || string.IsNullOrWhiteSpace(finalActor.unitId)
                || finalActor.finalActionSequence == null
                || finalActor.finalActionSequence.Length == 0
            )
            {
                continue;
            }

            BattleRuntimeUnit actor = FindAllyByUnitId(
                context != null ? context.Allies : null,
                context != null ? context.RosterProjection : null,
                finalActor.unitId
            );

            actors.Add(
                new SotDialogActorInputDto
                {
                    unitId = finalActor.unitId,
                    speechStyle = ResolveSpeechStyle(actor),
                    personalityDescription = ResolveDialogPersonalityDescription(actor),
                    sourceDialog = string.IsNullOrWhiteSpace(finalActor.sourceDialog)
                        ? "명령을 확인했다."
                        : finalActor.sourceDialog,
                    obedienceState = string.IsNullOrWhiteSpace(finalActor.obedienceState)
                        ? "obey"
                        : finalActor.obedienceState,
                    obeyedActionAdjustment = finalActor.obeyedActionAdjustment ?? string.Empty,
                    refusalSummary = finalActor.refusalSummary ?? string.Empty,
                    finalActionSequence = BuildDialogFinalActionSequence(finalActor.finalActionSequence),
                }
            );
        }

        return new SotDialogLayerRequestDto
        {
            originalCommand = postprocessResult.originalCommand ?? string.Empty,
            actors = actors.ToArray(),
        };
    }

    public SotDialogLayerRequestDto BuildFromMockParserResult(
        string originalCommand,
        BattleOrderRuntimeContext context,
        BattleMockCommandParseResult mockParseResult
    )
    {
        List<SotDialogActorInputDto> actors = new List<SotDialogActorInputDto>();

        BattleMockActorCommandSequenceDto[] actorSequences =
            mockParseResult != null
            && mockParseResult.parserOutput != null
            && mockParseResult.parserOutput.action != null
                ? mockParseResult.parserOutput.action
                : System.Array.Empty<BattleMockActorCommandSequenceDto>();

        for (int i = 0; i < actorSequences.Length; i++)
        {
            BattleMockActorCommandSequenceDto actorSequence = actorSequences[i];
            if (actorSequence == null || string.IsNullOrWhiteSpace(actorSequence.unitId))
                continue;

            BattleRuntimeUnit actor = FindAllyByUnitId(
                context != null ? context.Allies : null,
                context != null ? context.RosterProjection : null,
                actorSequence.unitId
            );

            actors.Add(
                new SotDialogActorInputDto
                {
                    unitId = actorSequence.unitId,
                    speechStyle = ResolveSpeechStyle(actor),
                    personalityDescription = ResolveDialogPersonalityDescription(actor),
                    sourceDialog = ResolveSourceDialog(mockParseResult, actorSequence.unitId),
                    obedienceState = "obey",
                    obeyedActionAdjustment = string.Empty,
                    refusalSummary = string.Empty,
                    finalActionSequence = BuildDialogFinalActionSequence(
                        BattleMockCommandParser.ToFinalActionSequence(actorSequence)
                    ),
                }
            );
        }

        return new SotDialogLayerRequestDto
        {
            originalCommand = originalCommand ?? string.Empty,
            actors = actors.ToArray(),
        };
    }

    private static BattleRuntimeUnit FindAllyByUnitId(
        BattleRuntimeUnit[] allies,
        IBattleRosterProjection roster,
        string unitId
    )
    {
        return BattleOrderRuntimeQueries.FindUnitById(allies, roster, unitId);
    }

    private static string ResolveSourceDialog(BattleMockCommandParseResult mockParseResult, string unitId)
    {
        if (
            mockParseResult == null
            || mockParseResult.parserOutput == null
            || mockParseResult.parserOutput.dialog == null
            || string.IsNullOrWhiteSpace(unitId)
        )
        {
            return "명령을 확인했다.";
        }

        for (int i = 0; i < mockParseResult.parserOutput.dialog.Length; i++)
        {
            BattleMockCommandDialogDto dialog = mockParseResult.parserOutput.dialog[i];
            if (dialog == null)
                continue;

            if (!string.Equals(dialog.unitId, unitId, System.StringComparison.Ordinal))
                continue;

            return string.IsNullOrWhiteSpace(dialog.text) ? "명령을 확인했다." : dialog.text;
        }

        return "명령을 확인했다.";
    }

    // dialog SLM 입력용 finalActionSequence를 만든다.
    // escape는 실행층에서 to를 쓰지 않고 적 위협/적 군집 반대 방향으로 이동하므로 dialog 입력에서도 to를 제거한다.
    private static SotFinalActionDto[] BuildDialogFinalActionSequence(SotFinalActionDto[] source)
    {
        if (source == null || source.Length == 0)
            return System.Array.Empty<SotFinalActionDto>();

        SotFinalActionDto[] result = new SotFinalActionDto[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            result[i] = CloneForDialog(source[i]);
        }

        return result;
    }

    // 실행 의미와 다른 옛 escape.to가 대사 생성에 섞이지 않도록 제거한다.
    private static SotFinalActionDto CloneForDialog(SotFinalActionDto source)
    {
        if (source == null)
            return null;

        string type = source.type ?? string.Empty;
        string subtype = source.subtype ?? string.Empty;
        bool isEscape =
            string.Equals(type, "move", System.StringComparison.OrdinalIgnoreCase)
            && string.Equals(subtype, "escape", System.StringComparison.OrdinalIgnoreCase);

        return new SotFinalActionDto
        {
            type = source.type,
            subtype = source.subtype,
            movementType = isEscape ? "direct" : source.movementType,
            to = isEscape ? null : source.to,
            target = source.target,
            description = source.description,
            mode = source.mode,
            durationSec = source.durationSec,
        };
    }

    // 대사 레이어에는 긴 description 대신 짧은 dialogPersonalityDescription만 넘김. 성능 낮은 slm을 돕도록, 실제 말투를 반영한 설명문임.
    // 기존 asset에 값이 없으면 description으로 폴백한다.
    private static string ResolveDialogPersonalityDescription(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return string.Empty;

        PersonalitySO personality = unit.Snapshot.Personality;

        if (!string.IsNullOrWhiteSpace(personality.dialogPersonalityDescription))
            return personality.dialogPersonalityDescription.Trim();

        return string.IsNullOrWhiteSpace(personality.description) ? string.Empty : personality.description.Trim();
    }

    private static int ResolveSpeechStyle(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return 0;

        return unit.Snapshot.Personality.speechStyle;
    }
}
