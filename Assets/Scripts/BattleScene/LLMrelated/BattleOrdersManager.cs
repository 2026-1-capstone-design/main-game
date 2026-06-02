// SOT mock/Gemini LLM/remote SLM 결과를 로그로 확인하고 실행 진입점에 연결한다.
// 서버 SOT 처리 중에는 명령 상태를 Processing으로 고정한다.
// 후처리 완료 action만 SlmUnitCommand로 변환해 BattleSimulationManager에 넘긴다.
// 실제 행동 생성은 SlmCommandUnitPlanner와 실행계층이 처리한다.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public enum BattleOrderCommandState
{
    Default,
    UserInput,
    Processing,
}

public sealed class BattleOrderProcessingResult
{
    public bool Succeeded { get; }
    public BattleRuntimeUnit[] IssuedActors { get; }

    public BattleRuntimeUnit FirstIssuedActor
    {
        get { return IssuedActors != null && IssuedActors.Length > 0 ? IssuedActors[0] : null; }
    }

    private BattleOrderProcessingResult(bool succeeded, BattleRuntimeUnit[] issuedActors)
    {
        IssuedActors = issuedActors ?? Array.Empty<BattleRuntimeUnit>();
        Succeeded = succeeded && IssuedActors.Length > 0;
    }

    public static BattleOrderProcessingResult Failed()
    {
        return new BattleOrderProcessingResult(false, Array.Empty<BattleRuntimeUnit>());
    }

    public static BattleOrderProcessingResult FromIssuedActors(List<BattleRuntimeUnit> issuedActors)
    {
        return new BattleOrderProcessingResult(issuedActors != null && issuedActors.Count > 0, issuedActors?.ToArray());
    }

    public static BattleOrderProcessingResult FromIssuedActors(BattleRuntimeUnit[] issuedActors)
    {
        return new BattleOrderProcessingResult(issuedActors != null && issuedActors.Length > 0, issuedActors);
    }
}

[DisallowMultipleComponent]
public sealed class BattleOrdersManager : MonoBehaviour
{
    private enum SotServerRouteKind
    {
        GeminiLlm,
        RemoteSlm,
    }

    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    [Header("SOT Layer Preview")]
    [SerializeField]
    private bool logSotLayerInputPreview = true;

    [Header("SOT Input Source")]
    [SerializeField]
    // 최상위 필터. 켜져 있으면 mock parser 전담 경로로 들어간다.
    private bool useMockInput = true;

    [SerializeField]
    // useMockInput이 꺼져 있을 때의 두 번째 분기다. true면 remote SLM, false면 Gemini LLM 경로로 들어간다.
    private bool useSLM = true;

    [Header("SOT Remote SLM Upstream")]
    [SerializeField]
    private string remoteSlmUpstreamUrl = "";

    [Header("SOT Gemini LLM Proxy")]
    [SerializeField]
    [FormerlySerializedAs("slmProxyUrl")]
    private string geminiProxyUrl = "https://together-proxy-fn-769017230258.asia-northeast3.run.app";

    [SerializeField]
    [FormerlySerializedAs("slmAppSharedToken")]
    private string geminiAppSharedToken = "";

    [SerializeField]
    [FormerlySerializedAs("slmRequestTimeoutSeconds")]
    private int geminiRequestTimeoutSeconds = 60;

    [Header("SOT Gemini LLM Request")]
    [SerializeField]
    private string geminiModel = "gemini-2.5-flash-lite";

    [SerializeField]
    private int geminiParserMaxOutputTokens = 700;

    [SerializeField]
    private int geminiDialogMaxOutputTokens = 400;

    [SerializeField]
    private bool geminiUseLowestThinking = true;

    [Header("SOT Remote SLM Proxy")]
    [SerializeField]
    private string remoteSlmProxyUrl = "";

    [SerializeField]
    private string remoteSlmAppSharedToken = "wlsgur9898eoaks";

    [SerializeField]
    private int remoteSlmRequestTimeoutSeconds = 60;

    [Header("SOT Remote SLM Request")]
    [SerializeField]
    private string remoteSlmModel = "gemma-4-e4b-it-q4-sft";

    [SerializeField]
    private int remoteSlmParserMaxOutputTokens = 700;

    [SerializeField]
    private int remoteSlmDialogMaxOutputTokens = 400;

    [SerializeField]
    private int remoteSlmParserNumCtx = 6000;

    [SerializeField]
    private int remoteSlmDialogNumCtx = 4000;

    [Header("SOT Timing Diagnostics")]
    [SerializeField]
    private bool logSotTimingBreakdown = true;

    [SerializeField]
    private bool measureUpstreamTtftByStreaming = true;

    [Header("SOT Command Execution")]
    [SerializeField]
    private bool issuePostprocessedSotCommands = true;

    private readonly BattleRuntimeUnit[] _allyUnits = new BattleRuntimeUnit[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly BattleRuntimeUnit[] _enemyUnits = new BattleRuntimeUnit[BattleTeamConstants.MaxUnitsPerTeam];
    private readonly BattleUnitNameResolver _unitNameResolver = new BattleUnitNameResolver();
    private readonly BattleParserInputBuilder _serverParserInputBuilder = new BattleParserInputBuilder();
    private readonly BattleCommandPostprocessor _serverPostprocessor = new BattleCommandPostprocessor();
    private readonly BattleDialogLayerInputBuilder _serverDialogInputBuilder = new BattleDialogLayerInputBuilder();
    private IBattleRosterProjection _rosterProjection;

    private SphereCollider _battlefieldCollider;
    private bool _initialized;

    // 구형 BattleLlmResponseDto 기반 요청 sequence라 주석처리함. 나중에 실제 SOT LLM 호출 재연결 시 다시 써야 함.
    // private int _requestSequence;
    private BattleOrderLayerPipeline _layerPipeline;

    // 서버 코루틴 중복 방지용 내부 플래그다. UI/배속/STT 제어는 CurrentCommandState를 기준으로 한다.
    private bool _serverSotPipelineRunning;
    private BattleOrderCommandState _commandState = BattleOrderCommandState.Default;

    public BattleOrderCommandState CurrentCommandState => _commandState;
    public bool IsCommandProcessing => _commandState == BattleOrderCommandState.Processing;

    public event Action<BattleRuntimeUnit, string> OnAllyOrderResponseReceived;
    public event Action<BattleOrderCommandState, BattleOrderCommandState> OnCommandStateChanged;
    public event Action<BattleOrderProcessingResult> OnCommandProcessingFinished;

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        Initialize(runtimeUnits, null, null);
    }

    public void Initialize(
        IReadOnlyList<BattleRuntimeUnit> runtimeUnits,
        IBattleRosterProjection rosterProjection,
        SphereCollider battlefieldCollider
    )
    {
        for (int i = 0; i < _allyUnits.Length; i++)
        {
            _allyUnits[i] = null;
        }

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            _enemyUnits[i] = null;
        }

        _rosterProjection = rosterProjection;
        _battlefieldCollider = battlefieldCollider;

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (_rosterProjection != null && _rosterProjection.TryGetHostileIndex(unit, out int hostileIndex))
                {
                    if (hostileIndex < 0 || hostileIndex >= _enemyUnits.Length)
                    {
                        continue;
                    }

                    _enemyUnits[hostileIndex] = unit;
                }
                else if (_rosterProjection != null && _rosterProjection.TryGetPlayerIndex(unit, out int playerIndex))
                {
                    if (playerIndex < 0 || playerIndex >= _allyUnits.Length)
                    {
                        continue;
                    }

                    _allyUnits[playerIndex] = unit;
                }
            }
        }
        _unitNameResolver.Rebuild(_allyUnits, _enemyUnits, _rosterProjection);

        _serverSotPipelineRunning = false;
        SetCommandState(BattleOrderCommandState.Default);
        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Initialized. AllyCount={CountUnits(_allyUnits)}, EnemyCount={CountUnits(_enemyUnits)}, HasBattlefieldCollider={_battlefieldCollider != null}",
                this
            );
        }
    }

    // 명령 입력 시작 상태를 외부 UI가 요청한다. 서버 처리 중이면 새 입력을 받지 않는다.
    public bool TryBeginUserOrderInput()
    {
        if (_commandState == BattleOrderCommandState.Processing)
        {
            return false;
        }

        SetCommandState(BattleOrderCommandState.UserInput);
        return true;
    }

    // 입력이 취소되거나 실제 처리로 넘어가지 않은 경우에만 기본 상태로 되돌린다.
    public void CancelUserOrderInput()
    {
        if (_commandState == BattleOrderCommandState.UserInput)
        {
            SetCommandState(BattleOrderCommandState.Default);
        }
    }

    public void SubmitGlobalOrder(string rawOrderText)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] SubmitGlobalOrder called before Initialize.", this);
            return;
        }

        string sanitizedRawText = SanitizeRawText(rawOrderText);
        if (string.IsNullOrWhiteSpace(sanitizedRawText))
        {
            Debug.LogWarning("[BattleOrdersManager] Global order ignored. Raw order text is empty.", this);
            CancelUserOrderInput();
            return;
        }

        if (verboseLog)
        {
            StringBuilder sb = new StringBuilder(768);
            sb.AppendLine("<color=#4FC3F7><b>[GLOBAL]</b></color>");
            sb.AppendLine("<color=#B3E5FC>Global order received.</color>");

            for (int i = 0; i < _allyUnits.Length; i++)
            {
                sb.AppendLine(BuildGlobalAllyLine(i + 1, _allyUnits[i]));
            }

            sb.Append("<color=#FFCC80>Raw order:</color> \"");
            sb.Append(sanitizedRawText);
            sb.AppendLine("\"");

            Debug.Log(sb.ToString(), this);
        }

        string sotOrderText = _unitNameResolver.ReplaceDisplayNamesWithSotIds(sanitizedRawText);

        if (verboseLog && !string.Equals(sanitizedRawText, sotOrderText, StringComparison.Ordinal))
        {
            Debug.Log(
                $"[BattleOrdersManager] Unit display names resolved for SOT pipeline. Raw=\"{sanitizedRawText}\", SOT=\"{sotOrderText}\"",
                this
            );
        }

        if (useMockInput)
        {
            RunSotLayerPipeline(sotOrderText);
            CancelUserOrderInput();
            return;
        }

        if (_serverSotPipelineRunning)
        {
            Debug.LogWarning(
                "[BattleOrdersManager] Server SOT pipeline ignored. Previous request is still running.",
                this
            );
            return;
        }

        SotServerRouteKind routeKind = useSLM ? SotServerRouteKind.RemoteSlm : SotServerRouteKind.GeminiLlm;

        if (!TryValidateSotServerRouteSettings(routeKind, out string routeSettingsError))
        {
            Debug.LogError("[BattleOrdersManager] Server SOT pipeline skipped. " + routeSettingsError, this);
            CancelUserOrderInput();
            return;
        }

        SetCommandState(BattleOrderCommandState.Processing);
        StartCoroutine(RunServerSotLayerPipeline(sotOrderText, routeKind));
    }

    public void SubmitSingleOrder(BattleRuntimeUnit targetAlly, string rawOrderText)
    {
        //deprecated
    }

    public string BuildCurrentUnitDisplayNamePromptSegment()
    {
        List<string> displayNames = new List<string>(BattleTeamConstants.MaxUnitsPerTeam * 2);
        _unitNameResolver.AppendDisplayNames(displayNames);

        if (displayNames.Count == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder(128);

        for (int i = 0; i < displayNames.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(displayNames[i]);
        }

        return sb.ToString();
    }

    private void RunSotLayerPipeline(string sanitizedRawText)
    {
        bool shouldLogPreview = logSotLayerInputPreview;

        BattleSimulationManager simulationManager = BattleSimulationManager.Instance;
        if (simulationManager == null)
        {
            if (shouldLogPreview)
            {
                Debug.LogWarning(
                    "[BattleOrdersManager] SOT layer pipeline skipped. BattleSimulationManager.Instance is null.",
                    this
                );
            }

            return;
        }

        _layerPipeline ??= new BattleOrderLayerPipeline();

        BattleOrderRuntimeContext context = new BattleOrderRuntimeContext(
            _allyUnits,
            _enemyUnits,
            _rosterProjection,
            simulationManager
        );

        if (
            !_layerPipeline.TryBuildInputPreview(
                sanitizedRawText,
                context,
                out BattleOrderLayerPreviewResult result,
                out string mockParserLog,
                out string error
            )
        )
        {
            if (shouldLogPreview)
            {
                if (error == "invalidinput")
                {
                    Debug.LogWarning(
                        "<color=#FF8A80><b>[SOT MOCK PARSER]</b></color> invalidinput\n"
                            + (string.IsNullOrWhiteSpace(mockParserLog) ? string.Empty : mockParserLog),
                        this
                    );
                }
                else
                {
                    Debug.LogWarning($"[BattleOrdersManager] SOT layer pipeline failed. Reason={error}", this);
                }
            }

            return;
        }

        if (shouldLogPreview)
        {
            if (!string.IsNullOrWhiteSpace(mockParserLog))
            {
                Debug.Log("<color=#AED581><b>[SOT MOCK PARSER]</b></color>\n" + mockParserLog, this);
            }

            Debug.Log("<color=#4FC3F7><b>[SOT PARSER INPUT PREVIEW]</b></color>\n" + result.ParserRequestJson, this);

            Debug.Log("<color=#81C784><b>[SOT MOCK PARSER OUTPUT]</b></color>\n" + result.MockParserOutputJson, this);

            Debug.Log(
                "<color=#FFB74D><b>[SOT POSTPROCESS RESULT PREVIEW]</b></color>\n" + result.PostprocessResultJson,
                this
            );

            Debug.Log("<color=#BA68C8><b>[SOT DIALOG INPUT PREVIEW]</b></color>\n" + result.DialogRequestJson, this);

            Debug.Log(
                "<color=#CE93D8><b>[SOT DIALOG RESPONSE PREVIEW]</b></color>\n" + result.DialogResponseJson,
                this
            );
        }

        TryIssuePostprocessedSlmCommands(result.PostprocessResult);

        if (issuePostprocessedSotCommands)
        {
            EmitDialogLayerResponses(result.DialogResponse);
        }
    }

    private IEnumerator RunServerSotLayerPipeline(string sanitizedRawText, SotServerRouteKind routeKind)
    {
        _serverSotPipelineRunning = true;

        bool shouldLogPreview = logSotLayerInputPreview;
        BattleSimulationManager simulationManager = BattleSimulationManager.Instance;

        if (simulationManager == null)
        {
            Debug.LogWarning(
                "[BattleOrdersManager] Server SOT pipeline skipped. BattleSimulationManager.Instance is null.",
                this
            );
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        BattleOrderRuntimeContext context = new BattleOrderRuntimeContext(
            _allyUnits,
            _enemyUnits,
            _rosterProjection,
            simulationManager
        );

        string selectedBackendId = GetSelectedSotBackendId(routeKind);
        string routeLabel = GetSotRouteLabel(routeKind);

        SotParserRequestDto parserRequest = _serverParserInputBuilder.Build(sanitizedRawText, context);
        SotLayerPromptBundle parserPrompt = FullPromptBuilderForSlmLayers.BuildParserPrompt(parserRequest);

        if (shouldLogPreview)
        {
            Debug.Log(
                "<color=#4FC3F7><b>[SOT " + routeLabel + " PARSER PROMPT]</b></color>\n" + parserPrompt.ToDebugText(),
                this
            );
        }

        string parserRawResponse = null;
        string parserRequestError = null;

        yield return PostSotLayerRequest(
            routeKind,
            "parser",
            selectedBackendId,
            parserPrompt,
            BuildParserLayerGenerationSettings(routeKind),
            (responseBackendId, responseProvider, responseModel, responseText) =>
            {
                parserRawResponse = responseText;
                if (shouldLogPreview)
                {
                    Debug.Log(
                        "<color=#81C784><b>[SOT "
                            + routeLabel
                            + " PARSER RAW RESPONSE]</b></color>\n"
                            + $"Backend={responseBackendId}, Provider={responseProvider}, Model={responseModel}\n"
                            + responseText,
                        this
                    );
                }
            },
            error => parserRequestError = error
        );

        if (!string.IsNullOrWhiteSpace(parserRequestError))
        {
            LogSotServerFailure("PARSER REQUEST FAILED", parserPrompt, parserRequestError, parserRawResponse);
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        if (
            !SotLayerOutputParser.TryParseParserOutput(
                parserRawResponse,
                parserRequest,
                out SotParserOutputDto parserOutput,
                out string parserParseError
            )
        )
        {
            LogSotServerFailure("PARSER OUTPUT INVALID", parserPrompt, parserParseError, parserRawResponse);
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        if (
            !_serverPostprocessor.TryProcess(
                sanitizedRawText,
                parserOutput,
                context,
                out BattleCommandPostprocessResult postprocessResult,
                out string postprocessError
            )
        )
        {
            LogSotServerFailure("POSTPROCESS FAILED", parserPrompt, postprocessError, parserRawResponse);
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        SotDialogLayerRequestDto dialogRequest;

        if (postprocessResult != null && postprocessResult.fallbackToDefaultMlAi)
        {
            if (shouldLogPreview)
            {
                Debug.Log(
                    "[BattleOrdersManager] Server SOT pipeline ended with fallbackToDefaultMlAi=true. AdvisorLine="
                        + (postprocessResult.advisorLine ?? string.Empty),
                    this
                );
            }

            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        dialogRequest = _serverDialogInputBuilder.BuildFromPostprocessResult(postprocessResult, context);

        if (dialogRequest == null || dialogRequest.actors == null || dialogRequest.actors.Length == 0)
        {
            if (shouldLogPreview)
            {
                Debug.Log("[BattleOrdersManager] Server SOT pipeline ended. Dialog request has no actors.", this);
            }

            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        SotLayerPromptBundle dialogPrompt = FullPromptBuilderForSlmLayers.BuildDialogPrompt(dialogRequest);

        if (shouldLogPreview)
        {
            Debug.Log(
                "<color=#BA68C8><b>[SOT " + routeLabel + " DIALOG PROMPT]</b></color>\n" + dialogPrompt.ToDebugText(),
                this
            );
        }

        string dialogRawResponse = null;
        string dialogRequestError = null;

        yield return PostSotLayerRequest(
            routeKind,
            "dialog",
            selectedBackendId,
            dialogPrompt,
            BuildDialogLayerGenerationSettings(routeKind),
            (responseBackendId, responseProvider, responseModel, responseText) =>
            {
                dialogRawResponse = responseText;
                if (shouldLogPreview)
                {
                    Debug.Log(
                        "<color=#CE93D8><b>[SOT "
                            + routeLabel
                            + " DIALOG RAW RESPONSE]</b></color>\n"
                            + $"Backend={responseBackendId}, Provider={responseProvider}, Model={responseModel}\n"
                            + responseText,
                        this
                    );
                }
            },
            error => dialogRequestError = error
        );

        if (!string.IsNullOrWhiteSpace(dialogRequestError))
        {
            LogSotServerFailure("DIALOG REQUEST FAILED", dialogPrompt, dialogRequestError, dialogRawResponse);
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        if (
            !SotLayerOutputParser.TryParseDialogOutput(
                dialogRawResponse,
                dialogRequest,
                out SotDialogLayerResponseDto dialogResponse,
                out string dialogParseError
            )
        )
        {
            LogSotServerFailure("DIALOG OUTPUT INVALID", dialogPrompt, dialogParseError, dialogRawResponse);
            FinishServerSotPipeline(BattleOrderProcessingResult.Failed());
            yield break;
        }

        if (shouldLogPreview)
        {
            Debug.Log(
                "<color=#FFB74D><b>[SOT "
                    + routeLabel
                    + " POSTPROCESS RESULT]</b></color>\n"
                    + FullPromptBuilderForSlmLayers.ToCompactJson(postprocessResult),
                this
            );
            Debug.Log(
                "<color=#CE93D8><b>[SOT "
                    + routeLabel
                    + " DIALOG RESPONSE]</b></color>\n"
                    + FullPromptBuilderForSlmLayers.ToCompactJson(dialogResponse),
                this
            );
        }

        if (issuePostprocessedSotCommands)
        {
            EmitDialogLayerResponses(dialogResponse);
        }

        BattleRuntimeUnit[] issuedActors = TryIssuePostprocessedSlmCommands(postprocessResult);

        FinishServerSotPipeline(BattleOrderProcessingResult.FromIssuedActors(issuedActors));
    }

    private IEnumerator PostSotLayerRequest(
        SotServerRouteKind routeKind,
        string layerName,
        string backendId,
        SotLayerPromptBundle promptBundle,
        GeminiGenerationSettingsDto generationSettings,
        Action<string, string, string, string> onSuccess,
        Action<string> onError
    )
    {
        string proxyUrl = GetSotProxyUrl(routeKind);
        string appSharedToken = GetSotAppSharedToken(routeKind);
        string model = GetSotModel(routeKind);
        string provider = GetSotProvider(routeKind);
        int requestTimeoutSeconds = GetSotRequestTimeoutSeconds(routeKind);

        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            onError?.Invoke(GetSotRouteLabel(routeKind) + " proxy URL is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            onError?.Invoke(GetSotRouteLabel(routeKind) + " model is empty.");
            yield break;
        }

        if (routeKind == SotServerRouteKind.RemoteSlm && string.IsNullOrWhiteSpace(appSharedToken))
        {
            onError?.Invoke("Remote SLM app shared token is empty.");
            yield break;
        }

        UnityConfiguredGeminiRequestDto requestDto = new UnityConfiguredGeminiRequestDto
        {
            remoteSlmProxyUrl = routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmUpstreamUrl.Trim() : string.Empty,
            backendId = backendId,
            provider = provider,
            layerName = layerName,
            model = model.Trim(),
            systemInstruction = promptBundle.SystemInstruction,
            userPayloadJson = promptBundle.UserPayloadJson,
            generationSettings = generationSettings,
            debugTiming = new SotProxyDebugTimingRequestDto
            {
                enabled = logSotTimingBreakdown,
                streamUpstream = measureUpstreamTtftByStreaming,
            },
        };

        string requestJson = JsonUtility.ToJson(requestDto);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

        using UnityWebRequest request = new UnityWebRequest(proxyUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

        if (!string.IsNullOrWhiteSpace(appSharedToken))
        {
            request.SetRequestHeader("X-App-Token", appSharedToken);
        }

        System.Diagnostics.Stopwatch clientStopwatch = System.Diagnostics.Stopwatch.StartNew();
        yield return request.SendWebRequest();
        clientStopwatch.Stop();
        float clientTotalMs = (float)clientStopwatch.Elapsed.TotalMilliseconds;

        string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(
                $"Route={GetSotRouteLabel(routeKind)}, Status={request.responseCode}, Error={request.error}, Body={responseBody}"
            );
            yield break;
        }

        SotProxyResponseDto responseDto;
        try
        {
            responseDto = JsonUtility.FromJson<SotProxyResponseDto>(responseBody);
        }
        catch (Exception exception)
        {
            onError?.Invoke("Proxy response JSON parse failed. " + exception.Message + ", Body=" + responseBody);
            yield break;
        }

        if (responseDto == null || string.IsNullOrWhiteSpace(responseDto.text))
        {
            onError?.Invoke("Proxy response text is empty. Body=" + responseBody);
            yield break;
        }

        if (logSotTimingBreakdown)
        {
            Debug.Log(BuildSotTimingBreakdownLog(layerName, responseDto, clientTotalMs), this);
        }

        onSuccess?.Invoke(
            responseDto.backendId ?? string.Empty,
            responseDto.provider ?? string.Empty,
            responseDto.model ?? string.Empty,
            responseDto.text
        );
    }

    private static string BuildSotTimingBreakdownLog(
        string layerName,
        SotProxyResponseDto responseDto,
        float clientTotalMs
    )
    {
        SotProxyTimingDto timing = responseDto != null ? responseDto.timing : null;
        float proxyTotalMs = timing != null ? timing.proxyTotalMs : -1f;
        float upstreamTotalMs = timing != null ? timing.upstreamTotalMs : -1f;
        float upstreamTtftMs = timing != null ? timing.upstreamTtftMs : -1f;
        float upstreamAfterTtftMs = timing != null ? timing.upstreamAfterTtftMs : -1f;
        float preUpstreamMs = timing != null ? timing.proxyPreUpstreamMs : -1f;
        float postUpstreamMs = timing != null ? timing.proxyPostUpstreamMs : -1f;
        float responseParseMs = timing != null ? timing.responseParseMs : -1f;
        float approximateUnityProxyNetworkMs = proxyTotalMs >= 0f ? clientTotalMs - proxyTotalMs : -1f;

        StringBuilder sb = new StringBuilder(768);
        sb.AppendLine("<color=#90CAF9><b>[SOT TIMING BREAKDOWN]</b></color>");
        sb.AppendLine("Layer=" + (layerName ?? string.Empty));
        sb.AppendLine("Model=" + (responseDto != null ? responseDto.model ?? string.Empty : string.Empty));
        sb.AppendLine("ClientTotalMs=" + FormatMs(clientTotalMs));
        sb.AppendLine("ProxyTotalMs=" + FormatMs(proxyTotalMs));
        sb.AppendLine("ApproxUnityProxyNetworkMs=" + FormatMs(approximateUnityProxyNetworkMs));
        sb.AppendLine("ProxyPreUpstreamMs=" + FormatMs(preUpstreamMs));
        sb.AppendLine("UpstreamTotalMs=" + FormatMs(upstreamTotalMs));
        sb.AppendLine("UpstreamTTFTMs=" + FormatMs(upstreamTtftMs));
        sb.AppendLine("UpstreamAfterTTFTMs=" + FormatMs(upstreamAfterTtftMs));
        sb.AppendLine("ProxyResponseParseMs=" + FormatMs(responseParseMs));
        sb.AppendLine("ProxyPostUpstreamMs=" + FormatMs(postUpstreamMs));
        sb.AppendLine("UsedUpstreamStreaming=" + (timing != null && timing.usedUpstreamStreaming));
        sb.AppendLine("RetriedWithoutReasoning=" + (timing != null && timing.retriedWithoutReasoning));
        return sb.ToString();
    }

    private static string FormatMs(float value)
    {
        return value < 0f ? "N/A" : value.ToString("0.0") + " ms";
    }

    private GeminiGenerationSettingsDto BuildParserLayerGenerationSettings(SotServerRouteKind routeKind)
    {
        int maxOutputTokens = Mathf.Max(1, GetParserMaxOutputTokens(routeKind));
        int numCtx = Mathf.Max(1, GetParserNumCtx(routeKind));
        float temperature = 0f;
        float topP = 0.8f;

        return new GeminiGenerationSettingsDto
        {
            temperature = temperature,
            topP = topP,
            top_p = topP,
            topK = routeKind == SotServerRouteKind.GeminiLlm ? 1 : 0,
            maxOutputTokens = maxOutputTokens,
            num_predict = maxOutputTokens,
            numCtx = numCtx,
            num_ctx = numCtx,
            stream = routeKind == SotServerRouteKind.RemoteSlm,
            think = false,
            candidateCount = 1,
            responseMimeType = "application/json",
            thinkingConfig = routeKind == SotServerRouteKind.GeminiLlm ? BuildGeminiThinkingConfig() : null,
        };
    }

    private GeminiGenerationSettingsDto BuildDialogLayerGenerationSettings(SotServerRouteKind routeKind)
    {
        int maxOutputTokens = Mathf.Max(1, GetDialogMaxOutputTokens(routeKind));
        int numCtx = Mathf.Max(1, GetDialogNumCtx(routeKind));
        float temperature = 0.2f;
        float topP = 0.8f;

        return new GeminiGenerationSettingsDto
        {
            temperature = temperature,
            topP = topP,
            top_p = topP,
            topK = routeKind == SotServerRouteKind.GeminiLlm ? 20 : 0,
            maxOutputTokens = maxOutputTokens,
            num_predict = maxOutputTokens,
            numCtx = numCtx,
            num_ctx = numCtx,
            stream = routeKind == SotServerRouteKind.RemoteSlm,
            think = false,
            candidateCount = 1,
            responseMimeType = "application/json",
            thinkingConfig = routeKind == SotServerRouteKind.GeminiLlm ? BuildGeminiThinkingConfig() : null,
        };
    }

    private GeminiThinkingConfigDto BuildGeminiThinkingConfig()
    {
        return new GeminiThinkingConfigDto
        {
            thinkingBudget = geminiUseLowestThinking ? 0 : -1,
            thinkingLevel = geminiUseLowestThinking ? "minimal" : "low",
        };
    }

    private bool TryValidateSotServerRouteSettings(SotServerRouteKind routeKind, out string error)
    {
        error = null;

        string routeLabel = GetSotRouteLabel(routeKind);
        string proxyUrl = GetSotProxyUrl(routeKind);
        string model = GetSotModel(routeKind);

        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            error = routeLabel + " proxy URL is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            error = routeLabel + " model is empty.";
            return false;
        }

        if (routeKind == SotServerRouteKind.RemoteSlm)
        {
            if (string.IsNullOrWhiteSpace(remoteSlmUpstreamUrl))
            {
                error = "Remote SLM upstream URL is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(remoteSlmAppSharedToken))
            {
                error = "Remote SLM app shared token is empty.";
                return false;
            }
        }

        return true;
    }

    private string GetSotProxyUrl(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmProxyUrl : geminiProxyUrl;
    }

    private string GetSotAppSharedToken(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmAppSharedToken : geminiAppSharedToken;
    }

    private string GetSotModel(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmModel : geminiModel;
    }

    private int GetSotRequestTimeoutSeconds(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmRequestTimeoutSeconds : geminiRequestTimeoutSeconds;
    }

    private int GetParserMaxOutputTokens(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmParserMaxOutputTokens : geminiParserMaxOutputTokens;
    }

    private int GetDialogMaxOutputTokens(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmDialogMaxOutputTokens : geminiDialogMaxOutputTokens;
    }

    private int GetParserNumCtx(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmParserNumCtx : 6000;
    }

    private int GetDialogNumCtx(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? remoteSlmDialogNumCtx : 4000;
    }

    private static string GetSotProvider(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? "remote_slm" : "gemini";
    }

    private static string GetSelectedSotBackendId(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm
            ? "remote_slm_llama_cpp_gemma4_e4b_q4_sft"
            : "unity_configured_gemini";
    }

    private static string GetSotRouteLabel(SotServerRouteKind routeKind)
    {
        return routeKind == SotServerRouteKind.RemoteSlm ? "REMOTE SLM" : "GEMINI LLM";
    }

    private void LogSotServerFailure(string title, SotLayerPromptBundle promptBundle, string error, string rawResponse)
    {
        if (!verboseLog)
        {
            return;
        }

        Debug.Log(
            "<color=#9E9E9E><b>[SOT SERVER "
                + title
                + " - IGNORED]</b></color>\n"
                + "<color=#BDBDBD>Reason:</color> "
                + (error ?? string.Empty)
                + "\n\n<color=#BDBDBD>PromptWithoutSystem:</color>\n"
                + BuildFailurePromptDebugText(promptBundle)
                + "\n\n<color=#BDBDBD>RawResponse:</color>\n"
                + (rawResponse ?? string.Empty),
            this
        );
    }

    private void EmitDialogLayerResponses(SotDialogLayerResponseDto dialogResponse)
    {
        if (dialogResponse == null || dialogResponse.dialog == null || dialogResponse.dialog.Length == 0)
        {
            return;
        }

        for (int i = 0; i < dialogResponse.dialog.Length; i++)
        {
            SotDialogLineDto line = dialogResponse.dialog[i];
            if (line == null || string.IsNullOrWhiteSpace(line.unitId))
            {
                continue;
            }

            BattleRuntimeUnit actorUnit = _unitNameResolver.FindUnitBySotId(line.unitId);
            if (actorUnit == null)
            {
                if (verboseLog)
                {
                    Debug.LogWarning(
                        "[BattleOrdersManager] Dialog response skipped. Actor unitId was not found. UnitId="
                            + (line.unitId ?? string.Empty),
                        this
                    );
                }

                continue;
            }

            if (_rosterProjection != null && !_rosterProjection.IsPlayerUnit(actorUnit))
            {
                if (verboseLog)
                {
                    Debug.LogWarning(
                        "[BattleOrdersManager] Dialog response skipped. Dialog unitId is not an ally. UnitId="
                            + (line.unitId ?? string.Empty),
                        this
                    );
                }

                continue;
            }

            if (
                !_unitNameResolver.TryResolveDialogText(
                    line.text,
                    out string resolvedDialogText,
                    out string resolveError
                )
            )
            {
                if (verboseLog)
                {
                    Debug.LogWarning(
                        "[BattleOrdersManager] Dialog text fallback applied. UnitId="
                            + (line.unitId ?? string.Empty)
                            + ", Reason="
                            + resolveError,
                        this
                    );
                }
            }

            RaiseAllyOrderResponse(actorUnit, resolvedDialogText);
        }
    }

    private void RaiseAllyOrderResponse(BattleRuntimeUnit actorUnit, string responseText)
    {
        string sanitizedText = SanitizeRawText(responseText);
        if (actorUnit != null && !string.IsNullOrWhiteSpace(sanitizedText))
        {
            OnAllyOrderResponseReceived?.Invoke(actorUnit, sanitizedText);
        }
    }

    private BattleRuntimeUnit[] TryIssuePostprocessedSlmCommands(BattleCommandPostprocessResult postprocessResult)
    {
        if (!issuePostprocessedSotCommands)
            return Array.Empty<BattleRuntimeUnit>();

        if (postprocessResult == null)
        {
            Debug.LogWarning("[BattleOrdersManager] SOT command execution skipped. PostprocessResult is null.", this);
            return Array.Empty<BattleRuntimeUnit>();
        }

        if (postprocessResult.fallbackToDefaultMlAi)
        {
            if (verboseLog)
            {
                Debug.Log(
                    "[BattleOrdersManager] SOT command execution skipped. fallbackToDefaultMlAi=true. AdvisorLine="
                        + (postprocessResult.advisorLine ?? string.Empty),
                    this
                );
            }

            return Array.Empty<BattleRuntimeUnit>();
        }

        BattleCommandFinalActorDto[] finalActors =
            postprocessResult.actors ?? System.Array.Empty<BattleCommandFinalActorDto>();

        if (finalActors.Length == 0)
        {
            if (verboseLog)
            {
                Debug.Log("[BattleOrdersManager] SOT command execution skipped. No final actors.", this);
            }

            return Array.Empty<BattleRuntimeUnit>();
        }

        BattleSimulationManager simulationManager = BattleSimulationManager.Instance;
        if (simulationManager == null)
        {
            Debug.LogError(
                "[BattleOrdersManager] SOT command execution skipped. BattleSimulationManager.Instance is null.",
                this
            );
            return Array.Empty<BattleRuntimeUnit>();
        }

        int issuedCount = 0;
        int failedCount = 0;
        List<BattleRuntimeUnit> issuedActors = new List<BattleRuntimeUnit>(finalActors.Length);

        for (int i = 0; i < finalActors.Length; i++)
        {
            BattleCommandFinalActorDto finalActor = finalActors[i];
            if (finalActor == null)
            {
                failedCount++;
                Debug.LogWarning(
                    "[BattleOrdersManager] SOT command execution skipped for actor entry. Final actor is null.",
                    this
                );
                continue;
            }

            if (
                !SlmDtoConverter.TryConvert(
                    finalActor,
                    _allyUnits,
                    _enemyUnits,
                    _rosterProjection,
                    out BattleRuntimeUnit actorUnit,
                    out List<SlmUnitCommand> slmCommands,
                    out string conversionError
                )
            )
            {
                failedCount++;
                Debug.LogWarning(
                    "[BattleOrdersManager] SOT command conversion failed. ActorUnitId="
                        + (finalActor.unitId ?? string.Empty)
                        + ", Error="
                        + conversionError,
                    this
                );
                continue;
            }

            if (actorUnit == null || actorUnit.State == null)
            {
                failedCount++;
                Debug.LogWarning(
                    "[BattleOrdersManager] SOT command execution skipped. Actor state is null. ActorUnitId="
                        + (finalActor.unitId ?? string.Empty),
                    this
                );
                continue;
            }

            if (slmCommands == null || slmCommands.Count == 0)
            {
                failedCount++;
                Debug.LogWarning(
                    "[BattleOrdersManager] SOT command execution skipped. Converted command list is empty. ActorUnitId="
                        + (finalActor.unitId ?? string.Empty),
                    this
                );
                continue;
            }

            simulationManager.IssueSlmCommands(actorUnit.State, slmCommands);
            issuedActors.Add(actorUnit);
            actorUnit.FlashCommandReceivedName();
            issuedCount++;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] SOT command execution result. IssuedActors={issuedCount}, FailedActors={failedCount}",
                this
            );
        }

        return issuedActors.ToArray();
    }

    private void FinishServerSotPipeline(BattleOrderProcessingResult result)
    {
        _serverSotPipelineRunning = false;
        SetCommandState(BattleOrderCommandState.Default);
        OnCommandProcessingFinished?.Invoke(result ?? BattleOrderProcessingResult.Failed());
    }

    private void SetCommandState(BattleOrderCommandState nextState)
    {
        if (_commandState == nextState)
        {
            return;
        }

        BattleOrderCommandState previousState = _commandState;
        _commandState = nextState;
        OnCommandStateChanged?.Invoke(previousState, nextState);
    }

    private string BuildUnitId(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "UNKNOWN";
        }

        if (_rosterProjection != null)
        {
            return _rosterProjection.GetDisplayUnitId(unit);
        }

        return $"U_{Mathf.Clamp(unit.UnitNumber, 0, 99):00}";
    }

    private static int CountUnits(BattleRuntimeUnit[] units)
    {
        int count = 0;

        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static string BuildGlobalAllyLine(int allyNumber, BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return $"<color=#AED581>Ally {allyNumber}:</color> Empty";
        }

        return $"<color=#AED581>Ally {allyNumber}:</color> {BuildUnitIdentityText(unit)}";
    }

    private static string BuildUnitIdentityText(BattleRuntimeUnit unit)
    {
        if (unit == null)
        {
            return "Unknown, Loyalty=0, Personality=None";
        }

        string displayName = string.IsNullOrWhiteSpace(unit.DisplayName) ? "Unknown" : unit.DisplayName;
        int loyalty = unit.Snapshot != null ? unit.Snapshot.Loyalty : 0;

        string personalityName = "None";
        if (unit.Snapshot != null && unit.Snapshot.Personality != null)
        {
            personalityName = unit.Snapshot.Personality.name;
        }

        return $"{displayName}, Loyalty={loyalty}, Personality={personalityName}";
    }

    private static string SanitizeRawText(string rawOrderText)
    {
        if (string.IsNullOrEmpty(rawOrderText))
        {
            return string.Empty;
        }

        return rawOrderText.Replace("\r", " ").Replace("\n", " ");
    }

    private static string BuildFailurePromptDebugText(SotLayerPromptBundle promptBundle)
    {
        return "[Layer]\n" + promptBundle.LayerName + "\n\n[UserPayloadJson]\n" + promptBundle.UserPayloadJson;
    }

    [Serializable]
    private sealed class UnityConfiguredGeminiRequestDto
    {
        public string remoteSlmProxyUrl;
        public string backendId;
        public string provider;
        public string layerName;
        public string model;
        public string systemInstruction;
        public string userPayloadJson;
        public GeminiGenerationSettingsDto generationSettings;
        public SotProxyDebugTimingRequestDto debugTiming;
    }

    [Serializable]
    private sealed class GeminiGenerationSettingsDto
    {
        public float temperature;
        public float topP;
        public float top_p;
        public int topK;
        public int maxOutputTokens;
        public int num_predict;
        public int numCtx;
        public int num_ctx;
        public string responseMimeType;
        public GeminiThinkingConfigDto thinkingConfig;
        public bool stream;
        public bool think;
        public int candidateCount;
    }

    [Serializable]
    private sealed class GeminiThinkingConfigDto
    {
        public int thinkingBudget;
        public string thinkingLevel;
    }

    [Serializable]
    private sealed class SotProxyDebugTimingRequestDto
    {
        public bool enabled;
        public bool streamUpstream;
    }

    [Serializable]
    private sealed class SotProxyResponseDto
    {
        public string backendId;
        public string provider;
        public string model;
        public string text;
        public SotProxyTimingDto timing;
    }

    [Serializable]
    private sealed class SotProxyTimingDto
    {
        public float proxyTotalMs;
        public float proxyPreUpstreamMs;
        public float upstreamHeadersMs;
        public float upstreamTtftMs;
        public float upstreamTotalMs;
        public float upstreamAfterTtftMs;
        public float responseParseMs;
        public float proxyPostUpstreamMs;
        public bool usedUpstreamStreaming;
        public bool retriedWithoutReasoning;
    }
}
