// 대사 레이어에 넘길 입력을 만든다.
// 대사 레이어는 finalActionSequence를 수정하지 않는다.
// mock parser 결과의 actor sequence를 그대로 대사 레이어 입력에 반영한다.

using System.Collections.Generic;

public sealed class BattleDialogLayerInputBuilder
{
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
                    personalityDescription = ResolvePersonalityDescription(actor),
                    sourceDialog = ResolveSourceDialog(mockParseResult, actorSequence.unitId),
                    obedienceState = "obey",
                    obeyedActionAdjustment = string.Empty,
                    refusalSummary = string.Empty,
                    finalActionSequence = BattleMockCommandParser.ToFinalActionSequence(actorSequence),
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
        if (allies == null || string.IsNullOrWhiteSpace(unitId))
            return null;

        string normalizedUnitId = unitId.ToUpperInvariant();

        for (int i = 0; i < allies.Length; i++)
        {
            BattleRuntimeUnit unit = allies[i];
            if (unit == null)
                continue;

            string currentUnitId = BattleOrderRuntimeQueries.GetUnitId(unit, roster);
            if (string.Equals(currentUnitId, normalizedUnitId, System.StringComparison.Ordinal))
                return unit;
        }

        return null;
    }

    private static string ResolveSourceDialog(
        BattleMockCommandParseResult mockParseResult,
        string unitId
    )
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

            return string.IsNullOrWhiteSpace(dialog.text)
                ? "명령을 확인했다."
                : dialog.text;
        }

        return "명령을 확인했다.";
    }

    private static string ResolvePersonalityDescription(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return string.Empty;

        return string.IsNullOrWhiteSpace(unit.Snapshot.Personality.description)
            ? string.Empty
            : unit.Snapshot.Personality.description.Trim();
    }

    private static int ResolveSpeechStyle(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.Snapshot == null || unit.Snapshot.Personality == null)
            return 0;

        return unit.Snapshot.Personality.speechStyle;
    }
}
