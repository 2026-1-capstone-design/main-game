// 최후방 폴백 파서 결과를 실행 가능한 후처리 결과로 변환한다.
// 일반 postprocessor의 순응/거부 확률은 사용하지 않는다.
// 성공 결과는 forced obey와 hardcoded dialog를 함께 가진다.
// 실패 결과는 참모 대사 하나만 가진다.
// 이 파일은 실행 발행을 하지 않고 DTO만 만든다.

using System;

public static class BattleCommandEmergencyFallbackBuilder
{
    public static bool TryBuild(
        string rawCommand,
        BattleOrderRuntimeContext context,
        out BattleCommandEmergencyFallbackBuildResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (
            !BattleCommandEmergencyFallbackParser.TryParse(
                rawCommand,
                context,
                out BattleCommandEmergencyFallbackParseResult parseResult,
                out string parseLog
            )
        )
        {
            debugLog = parseLog;
            result = BattleCommandEmergencyFallbackBuildResult.Failed(parseLog);
            return false;
        }

        BattleCommandPostprocessResult postprocessResult = new BattleCommandPostprocessResult
        {
            originalCommand = rawCommand ?? string.Empty,
            fallbackToDefaultMlAi = false,
            advisorLine = string.Empty,
            actors = new[]
            {
                new BattleCommandFinalActorDto
                {
                    unitId = parseResult.actorUnitId,
                    obedienceState = "obey",
                    mainActionCategory = parseResult.mainActionCategory,
                    sourceDialog = string.IsNullOrWhiteSpace(parseResult.sourceDialog)
                        ? "명령을 확인했다."
                        : parseResult.sourceDialog,
                    obeyedActionAdjustment = string.Empty,
                    refusalSummary = string.Empty,
                    finalActionSequence = CloneSequence(parseResult.finalActionSequence),
                },
            },
            debug = new BattleCommandPostprocessDebugDto
            {
                parserThinking = "emergency fallback result",
                droppedActorSummaries = Array.Empty<string>(),
                adjustmentSummaries = Array.Empty<string>(),
                refusalSummaries = Array.Empty<string>(),
            },
        };

        SotDialogLayerResponseDto dialogResponse = BattleCommandFallbackDialogBuilder.BuildFromPostprocessResult(
            postprocessResult,
            context
        );

        result = BattleCommandEmergencyFallbackBuildResult.Succeeded(postprocessResult, dialogResponse, parseLog);
        debugLog = parseLog;
        return true;
    }

    private static SotFinalActionDto[] CloneSequence(SotFinalActionDto[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<SotFinalActionDto>();

        SotFinalActionDto[] result = new SotFinalActionDto[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = CloneAction(source[i]);

        return result;
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
}

public sealed class BattleCommandEmergencyFallbackBuildResult
{
    public bool succeeded;
    public bool bypassPostprocessor;
    public BattleCommandPostprocessResult postprocessResult;
    public SotDialogLayerResponseDto dialogResponse;
    public string advisorLine;
    public string debugLog;

    public static BattleCommandEmergencyFallbackBuildResult Succeeded(
        BattleCommandPostprocessResult postprocessResult,
        SotDialogLayerResponseDto dialogResponse,
        string debugLog
    )
    {
        return new BattleCommandEmergencyFallbackBuildResult
        {
            succeeded = true,
            bypassPostprocessor = true,
            postprocessResult = postprocessResult,
            dialogResponse = dialogResponse,
            advisorLine = string.Empty,
            debugLog = debugLog ?? string.Empty,
        };
    }

    public static BattleCommandEmergencyFallbackBuildResult Failed(string debugLog)
    {
        return new BattleCommandEmergencyFallbackBuildResult
        {
            succeeded = false,
            bypassPostprocessor = false,
            postprocessResult = new BattleCommandPostprocessResult
            {
                originalCommand = string.Empty,
                fallbackToDefaultMlAi = true,
                advisorLine = BattleCommandEmergencyFallbackParser.AdvisorLine,
                actors = Array.Empty<BattleCommandFinalActorDto>(),
                debug = new BattleCommandPostprocessDebugDto
                {
                    parserThinking = "emergency fallback failed",
                    droppedActorSummaries = string.IsNullOrWhiteSpace(debugLog)
                        ? Array.Empty<string>()
                        : new[] { debugLog },
                    adjustmentSummaries = Array.Empty<string>(),
                    refusalSummaries = Array.Empty<string>(),
                },
            },
            dialogResponse = new SotDialogLayerResponseDto { dialog = Array.Empty<SotDialogLineDto>() },
            advisorLine = BattleCommandEmergencyFallbackParser.AdvisorLine,
            debugLog = debugLog ?? string.Empty,
        };
    }
}
