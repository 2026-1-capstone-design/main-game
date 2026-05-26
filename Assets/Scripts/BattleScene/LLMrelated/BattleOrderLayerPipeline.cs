// Battle order의 parser/postprocess/dialog 레이어 입력을 순서대로 조립한다.
// parser client, postprocessor, dialog client가 이 파이프라인에 연결됨.
// 실행 진입점 호출은 별도 dispatcher가 담당한다.

using System.Collections.Generic;
using Newtonsoft.Json;

public sealed class BattleOrderLayerPipeline
{
    private readonly BattleParserInputBuilder _parserInputBuilder = new BattleParserInputBuilder();
    private readonly BattleCommandPostprocessor _postprocessor = new BattleCommandPostprocessor();
    private readonly BattleDialogLayerInputBuilder _dialogInputBuilder = new BattleDialogLayerInputBuilder();

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Include,
    };

    public bool TryBuildInputPreview(
        string rawCommand,
        BattleOrderRuntimeContext context,
        out BattleOrderLayerPreviewResult result,
        out string mockParserLog,
        out string error
    )
    {
        result = null;
        mockParserLog = null;
        error = null;

        if (context == null)
        {
            error = "BattleOrderRuntimeContext is null.";
            return false;
        }

        string command = rawCommand ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "Command is empty.";
            return false;
        }

        SotParserRequestDto parserRequest = _parserInputBuilder.Build(command, context);
        string parserRequestJson = JsonConvert.SerializeObject(parserRequest, JsonSettings);

        if (
            !BattleMockCommandParser.TryParse(
                parserRequest,
                out BattleMockCommandParseResult mockParseResult,
                out mockParserLog
            )
        )
        {
            error = "invalidinput";
            return false;
        }

        SotParserOutputDto mockParserOutput = ConvertMockParserOutput(
            mockParseResult != null ? mockParseResult.parserOutput : null
        );
        string mockParserOutputJson = JsonConvert.SerializeObject(mockParserOutput, JsonSettings);

        if (
            !_postprocessor.TryProcess(
                command,
                mockParserOutput,
                context,
                out BattleCommandPostprocessResult postprocessResult,
                out string postprocessError
            )
        )
        {
            error = postprocessError;
            return false;
        }

        string postprocessResultJson = JsonConvert.SerializeObject(postprocessResult, JsonSettings);

        SotDialogLayerRequestDto dialogRequest;
        SotDialogLayerResponseDto dialogResponse;

        if (postprocessResult != null && postprocessResult.fallbackToDefaultMlAi)
        {
            dialogRequest = new SotDialogLayerRequestDto
            {
                originalCommand = command,
                actors = System.Array.Empty<SotDialogActorInputDto>(),
            };

            dialogResponse = new SotDialogLayerResponseDto { dialog = System.Array.Empty<SotDialogLineDto>() };
        }
        else
        {
            dialogRequest = _dialogInputBuilder.BuildFromPostprocessResult(postprocessResult, context);
            dialogResponse = BuildMockDialogResponse(dialogRequest);
        }

        string dialogRequestJson = JsonConvert.SerializeObject(dialogRequest, JsonSettings);
        string dialogResponseJson = JsonConvert.SerializeObject(dialogResponse, JsonSettings);

        result = new BattleOrderLayerPreviewResult(
            parserRequest,
            parserRequestJson,
            mockParserOutput,
            mockParserOutputJson,
            postprocessResult,
            postprocessResultJson,
            dialogRequest,
            dialogRequestJson,
            dialogResponse,
            dialogResponseJson,
            mockParserLog
        );

        return true;
    }

    private static SotParserOutputDto ConvertMockParserOutput(BattleMockParserOutputDto source)
    {
        if (source == null)
        {
            return new SotParserOutputDto
            {
                thinking = string.Empty,
                dialog = System.Array.Empty<SotDialogLineDto>(),
                action = System.Array.Empty<SotActorActionDto>(),
            };
        }

        return new SotParserOutputDto
        {
            thinking = source.thinking ?? string.Empty,
            dialog = ConvertMockParserDialog(source.dialog),
            action = ConvertMockActorSequences(source.action),
        };
    }

    private static SotDialogLineDto[] ConvertMockParserDialog(BattleMockCommandDialogDto[] source)
    {
        if (source == null || source.Length == 0)
            return System.Array.Empty<SotDialogLineDto>();

        List<SotDialogLineDto> result = new List<SotDialogLineDto>(source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            BattleMockCommandDialogDto line = source[i];
            if (line == null || string.IsNullOrWhiteSpace(line.unitId))
                continue;

            result.Add(
                new SotDialogLineDto
                {
                    unitId = line.unitId,
                    text = string.IsNullOrWhiteSpace(line.text) ? "알겠습니다" : line.text,
                }
            );
        }

        return result.ToArray();
    }

    private static SotActorActionDto[] ConvertMockActorSequences(BattleMockActorCommandSequenceDto[] source)
    {
        if (source == null || source.Length == 0)
            return System.Array.Empty<SotActorActionDto>();

        List<SotActorActionDto> result = new List<SotActorActionDto>(source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            BattleMockActorCommandSequenceDto actorSequence = source[i];
            if (actorSequence == null || string.IsNullOrWhiteSpace(actorSequence.unitId))
                continue;

            result.Add(
                new SotActorActionDto
                {
                    unitId = actorSequence.unitId,
                    sequence = BattleMockCommandParser.ToFinalActionSequence(actorSequence),
                }
            );
        }

        return result.ToArray();
    }

    private static SotDialogLayerResponseDto BuildMockDialogResponse(SotDialogLayerRequestDto dialogRequest)
    {
        if (dialogRequest == null || dialogRequest.actors == null)
        {
            return new SotDialogLayerResponseDto { dialog = System.Array.Empty<SotDialogLineDto>() };
        }

        List<SotDialogLineDto> lines = new List<SotDialogLineDto>(dialogRequest.actors.Length);

        for (int i = 0; i < dialogRequest.actors.Length; i++)
        {
            SotDialogActorInputDto actor = dialogRequest.actors[i];
            if (actor == null || string.IsNullOrWhiteSpace(actor.unitId))
                continue;

            lines.Add(
                new SotDialogLineDto
                {
                    unitId = actor.unitId,
                    text = actor.obedienceState == "refuse" ? "거절합니다" : "알겠습니다",
                }
            );
        }

        return new SotDialogLayerResponseDto { dialog = lines.ToArray() };
    }
}
