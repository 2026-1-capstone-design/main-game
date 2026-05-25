// Battle order의 parser/dialog 레이어 입력을 순서대로 조립한다.
// parser client, postprocessor, dialog client가 이 파이프라인에 연결됨.
// 실행 진입점 호출은 별도 dispatcher가 담당한다.

using Newtonsoft.Json;

public sealed class BattleOrderLayerPipeline
{
    private readonly BattleParserInputBuilder _parserInputBuilder = new BattleParserInputBuilder();
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

        string command = rawCommand ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            error = "Command is empty.";
            return false;
        }

        SotParserRequestDto parserRequest = _parserInputBuilder.Build(command, context);
        string parserRequestJson = JsonConvert.SerializeObject(parserRequest, JsonSettings);

        SotDialogLayerRequestDto dialogRequest = _dialogInputBuilder.BuildDummy(command, context);
        string dialogRequestJson = JsonConvert.SerializeObject(dialogRequest, JsonSettings);

        result = new BattleOrderLayerPreviewResult(
            parserRequest,
            parserRequestJson,
            dialogRequest,
            dialogRequestJson
        );

        return true;
    }
}
