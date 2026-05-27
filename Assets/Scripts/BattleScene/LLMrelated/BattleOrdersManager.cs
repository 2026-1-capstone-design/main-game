// SOT mock/server 결과를 로그로 확인하고 실행 진입점에 연결한다.
// 서버 경로는 Unity가 보낸 모델명/생성 설정/진단 설정으로 Gemini proxy를 호출한다.
// 후처리 완료 action만 SlmUnitCommand로 변환해 BattleSimulationManager에 넘긴다.
// 실제 행동 생성은 SlmCommandUnitPlanner와 실행계층이 처리한다.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class BattleOrdersManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;


    [Header("SOT Layer Preview")]
    [SerializeField]
    private bool logSotLayerInputPreview = true;

    [Header("SOT Input Source")]
    [SerializeField]
    // 최상위 필터. 이게 켜지면 무조건 "공격 브랜드 가렌" 같은 내부 파이프라인으로.
    private bool useMockInput = true;

    [SerializeField]
    // 두번째 필터. 서버 경로에서는 현재 Gemini proxy를 호출한다. 이 값은 backendId 로그 구분용으로만 남긴다.
    private bool useSLM = true;

    [Header("SOT Server")]
    [SerializeField]
    private string slmProxyUrl = "";

    [SerializeField]
    private string slmAppSharedToken = "";

    [SerializeField]
    private BattleLlmBackend selectedSlmBackend = BattleLlmBackend.TogetherGemma3nE4B;

    [SerializeField]
    private int slmRequestTimeoutSeconds = 60;

    [Header("SOT Gemini Request")]
    [SerializeField]
    private string geminiModel = "gemini-2.5-flash-lite";

    [SerializeField]
    private int geminiParserMaxOutputTokens = 450;

    [SerializeField]
    private int geminiDialogMaxOutputTokens = 220;

    [SerializeField]
    private bool geminiUseLowestThinking = true;

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
    private bool _serverSotPipelineRunning;

    public event Action<BattleRuntimeUnit, string> OnAllyOrderResponseReceived;

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

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Initialized. AllyCount={CountUnits(_allyUnits)}, EnemyCount={CountUnits(_enemyUnits)}, HasBattlefieldCollider={_battlefieldCollider != null}",
                this
            );
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
        }
        else
        {
            if (_serverSotPipelineRunning)
            {
                Debug.LogWarning("[BattleOrdersManager] Server SOT pipeline ignored. Previous request is still running.", this);
                return;
            }

            StartCoroutine(RunServerSotLayerPipeline(sotOrderText));
        }
    }

    public void SubmitSingleOrder(BattleRuntimeUnit targetAlly, string rawOrderText)
    {
        //deprecated
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

    private IEnumerator RunServerSotLayerPipeline(string sanitizedRawText)
    {
        _serverSotPipelineRunning = true;

        bool shouldLogPreview = logSotLayerInputPreview;
        BattleSimulationManager simulationManager = BattleSimulationManager.Instance;

        if (simulationManager == null)
        {
            Debug.LogWarning("[BattleOrdersManager] Server SOT pipeline skipped. BattleSimulationManager.Instance is null.", this);
            _serverSotPipelineRunning = false;
            yield break;
        }

        BattleOrderRuntimeContext context = new BattleOrderRuntimeContext(
            _allyUnits,
            _enemyUnits,
            _rosterProjection,
            simulationManager
        );

        string selectedBackendId = GetSelectedSotBackendId();

        SotParserRequestDto parserRequest = _serverParserInputBuilder.Build(sanitizedRawText, context);
        SotLayerPromptBundle parserPrompt = FullPromptBuilderForSlmLayers.BuildParserPrompt(parserRequest);

        if (shouldLogPreview)
        {
            Debug.Log("<color=#4FC3F7><b>[SOT SERVER PARSER PROMPT]</b></color>\n" + parserPrompt.ToDebugText(), this);
        }

        string parserRawResponse = null;
        string parserRequestError = null;

        yield return PostSotLayerRequest(
            "parser",
            selectedBackendId,
            parserPrompt,
            BuildParserLayerGenerationSettings(),
            (responseBackendId, responseProvider, responseModel, responseText) =>
            {
                parserRawResponse = responseText;
                if (shouldLogPreview)
                {
                    Debug.Log(
                        "<color=#81C784><b>[SOT SERVER PARSER RAW RESPONSE]</b></color>\n"
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
            _serverSotPipelineRunning = false;
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
            _serverSotPipelineRunning = false;
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
            _serverSotPipelineRunning = false;
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

            _serverSotPipelineRunning = false;
            yield break;
        }

        dialogRequest = _serverDialogInputBuilder.BuildFromPostprocessResult(postprocessResult, context);

        if (dialogRequest == null || dialogRequest.actors == null || dialogRequest.actors.Length == 0)
        {
            if (shouldLogPreview)
            {
                Debug.Log("[BattleOrdersManager] Server SOT pipeline ended. Dialog request has no actors.", this);
            }

            _serverSotPipelineRunning = false;
            yield break;
        }

        SotLayerPromptBundle dialogPrompt = FullPromptBuilderForSlmLayers.BuildDialogPrompt(dialogRequest);

        if (shouldLogPreview)
        {
            Debug.Log("<color=#BA68C8><b>[SOT SERVER DIALOG PROMPT]</b></color>\n" + dialogPrompt.ToDebugText(), this);
        }

        string dialogRawResponse = null;
        string dialogRequestError = null;

        yield return PostSotLayerRequest(
            "dialog",
            selectedBackendId,
            dialogPrompt,
            BuildDialogLayerGenerationSettings(),
            (responseBackendId, responseProvider, responseModel, responseText) =>
            {
                dialogRawResponse = responseText;
                if (shouldLogPreview)
                {
                    Debug.Log(
                        "<color=#CE93D8><b>[SOT SERVER DIALOG RAW RESPONSE]</b></color>\n"
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
            _serverSotPipelineRunning = false;
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
            _serverSotPipelineRunning = false;
            yield break;
        }

        if (shouldLogPreview)
        {
            Debug.Log(
                "<color=#FFB74D><b>[SOT SERVER POSTPROCESS RESULT]</b></color>\n"
                    + FullPromptBuilderForSlmLayers.ToCompactJson(postprocessResult),
                this
            );
            Debug.Log(
                "<color=#CE93D8><b>[SOT SERVER DIALOG RESPONSE]</b></color>\n"
                    + FullPromptBuilderForSlmLayers.ToCompactJson(dialogResponse),
                this
            );
        }

        if (issuePostprocessedSotCommands)
        {
            EmitDialogLayerResponses(dialogResponse);
        }

        TryIssuePostprocessedSlmCommands(postprocessResult);

        _serverSotPipelineRunning = false;
    }

    private IEnumerator PostSotLayerRequest(
        string layerName,
        string backendId,
        SotLayerPromptBundle promptBundle,
        GeminiGenerationSettingsDto generationSettings,
        Action<string, string, string, string> onSuccess,
        Action<string> onError
    )
    {
        if (string.IsNullOrWhiteSpace(slmProxyUrl))
        {
            onError?.Invoke("SOT proxy URL is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(geminiModel))
        {
            onError?.Invoke("Gemini model is empty.");
            yield break;
        }

        UnityConfiguredGeminiRequestDto requestDto = new UnityConfiguredGeminiRequestDto
        {
            backendId = backendId,
            provider = "gemini",
            layerName = layerName,
            model = geminiModel.Trim(),
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

        using UnityWebRequest request = new UnityWebRequest(slmProxyUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.Max(1, slmRequestTimeoutSeconds);
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");

        if (!string.IsNullOrWhiteSpace(slmAppSharedToken))
        {
            request.SetRequestHeader("X-App-Token", slmAppSharedToken);
        }

        System.Diagnostics.Stopwatch clientStopwatch = System.Diagnostics.Stopwatch.StartNew();
        yield return request.SendWebRequest();
        clientStopwatch.Stop();
        float clientTotalMs = (float)clientStopwatch.Elapsed.TotalMilliseconds;

        string responseBody = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

        if (request.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(
                $"Status={request.responseCode}, Error={request.error}, Body={responseBody}"
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
        sb.AppendLine("GeminiUpstreamTotalMs=" + FormatMs(upstreamTotalMs));
        sb.AppendLine("GeminiTTFTMs=" + FormatMs(upstreamTtftMs));
        sb.AppendLine("GeminiAfterTTFTMs=" + FormatMs(upstreamAfterTtftMs));
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

    private GeminiGenerationSettingsDto BuildParserLayerGenerationSettings()
    {
        return new GeminiGenerationSettingsDto
        {
            temperature = 0f,
            topP = 1f,
            topK = 1,
            maxOutputTokens = Mathf.Max(1, geminiParserMaxOutputTokens),
            stream = false,
            candidateCount = 1,
            responseMimeType = "application/json",
            thinkingConfig = BuildGeminiThinkingConfig(),
        };
    }

    private GeminiGenerationSettingsDto BuildDialogLayerGenerationSettings()
    {
        return new GeminiGenerationSettingsDto
        {
            temperature = 0.2f,
            topP = 0.9f,
            topK = 20,
            maxOutputTokens = Mathf.Max(1, geminiDialogMaxOutputTokens),
            stream = false,
            candidateCount = 1,
            responseMimeType = "application/json",
            thinkingConfig = BuildGeminiThinkingConfig(),
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

    private void LogSotServerFailure(
        string title,
        SotLayerPromptBundle promptBundle,
        string error,
        string rawResponse
    )
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
                + "\n\n<color=#BDBDBD>FullPrompt:</color>\n"
                + promptBundle.ToDebugText()
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

    private void TryIssuePostprocessedSlmCommands(BattleCommandPostprocessResult postprocessResult)
    {
        if (!issuePostprocessedSotCommands)
            return;

        if (postprocessResult == null)
        {
            Debug.LogWarning("[BattleOrdersManager] SOT command execution skipped. PostprocessResult is null.", this);
            return;
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

            return;
        }

        BattleCommandFinalActorDto[] finalActors =
            postprocessResult.actors ?? System.Array.Empty<BattleCommandFinalActorDto>();

        if (finalActors.Length == 0)
        {
            if (verboseLog)
            {
                Debug.Log("[BattleOrdersManager] SOT command execution skipped. No final actors.", this);
            }

            return;
        }

        BattleSimulationManager simulationManager = BattleSimulationManager.Instance;
        if (simulationManager == null)
        {
            Debug.LogError(
                "[BattleOrdersManager] SOT command execution skipped. BattleSimulationManager.Instance is null.",
                this
            );
            return;
        }

        int issuedCount = 0;
        int failedCount = 0;

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
            issuedCount++;
        }

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] SOT command execution result. IssuedActors={issuedCount}, FailedActors={failedCount}",
                this
            );
        }
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

    private string GetSelectedSotBackendId()
    {
        return "unity_configured_gemini";
    }

    private static string GetSelectedBackendId(BattleLlmBackend backend)
    {
        switch (backend)
        {
            case BattleLlmBackend.TogetherGemma3nE4B:
                return "together_gemma_3n";

            case BattleLlmBackend.Gemini25FlashLite:
                return "gemini_25_flash_lite";

            default:
                return "together_gemma_3n";
        }
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
    [Serializable]
    private sealed class UnityConfiguredGeminiRequestDto
    {
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
        public int topK;
        public int maxOutputTokens;
        public string responseMimeType;
        public GeminiThinkingConfigDto thinkingConfig;
        public bool stream;
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
