using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;  // ← Gemini 직접 호출에 필요

[DisallowMultipleComponent]
public sealed class BattleOrdersManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    // ── 기존 LLM Proxy 필드는 그대로 유지 (혹시 나중에 쓸 수도 있으므로) ──
    [Header("LLM Proxy (미사용 - Gemini Direct로 대체됨)")]
    [SerializeField] private bool sendOrdersToLlm = true;
    [SerializeField] private string llmProxyUrl = "";
    [SerializeField] private string appSharedToken = "";
    [SerializeField] private BattleLlmBackend selectedLlmBackend = BattleLlmBackend.TogetherGemma3nE4B;
    [SerializeField] private int requestTimeoutSeconds = 30;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // ▼▼▼ 여기가 새로 추가된 Gemini 직접 연결 설정 ▼▼▼
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    [Header("Gemini Direct API")]
    [Tooltip("Google AI Studio에서 발급받은 API 키를 여기에 입력하세요.\naistudio.google.com → Get API Key")]
    [SerializeField] private string geminiApiKey = "";

    [Tooltip("사용할 Gemini 모델명. 기본값: gemini-2.5-flash-lite-preview-06-17")]
    [SerializeField] private string geminiModel = "gemini-2.5-flash-lite-preview-06-17";

    [Tooltip("켜면 기존 Proxy 대신 Gemini API를 직접 호출합니다.")]
    [SerializeField] private bool useGeminiDirect = true;
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private readonly BattleRuntimeUnit[] _allyUnits = new BattleRuntimeUnit[6];
    private readonly BattleRuntimeUnit[] _enemyUnits = new BattleRuntimeUnit[6];

    private BoxCollider _battlefieldCollider;
    private bool _initialized;
    private int _requestSequence;
    private BattleSimulationManager _battleSimulationManager;
    private TacticalIntentExecutor _intentExecutor;

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits)
    {
        Initialize(runtimeUnits, null);
    }

    public void Initialize(IReadOnlyList<BattleRuntimeUnit> runtimeUnits, BoxCollider battlefieldCollider)
    {
        for (int i = 0; i < _allyUnits.Length; i++)
            _allyUnits[i] = null;
        for (int i = 0; i < _enemyUnits.Length; i++)
            _enemyUnits[i] = null;

        _battlefieldCollider = battlefieldCollider;
        _battleSimulationManager = FindAnyObjectByType<BattleSimulationManager>();
        _intentExecutor = FindAnyObjectByType<TacticalIntentExecutor>();

        if (runtimeUnits != null)
        {
            for (int i = 0; i < runtimeUnits.Count; i++)
            {
                BattleRuntimeUnit unit = runtimeUnits[i];
                if (unit == null)
                    continue;

                if (unit.IsEnemy)
                {
                    int enemyIndex = unit.UnitNumber - 7;
                    if (enemyIndex < 0 || enemyIndex >= _enemyUnits.Length)
                        continue;
                    _enemyUnits[enemyIndex] = unit;
                }
                else
                {
                    int allyIndex = unit.UnitNumber - 1;
                    if (allyIndex < 0 || allyIndex >= _allyUnits.Length)
                        continue;
                    _allyUnits[allyIndex] = unit;
                }
            }
        }

        _initialized = true;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Initialized. AllyCount={CountUnits(_allyUnits)}, EnemyCount={CountUnits(_enemyUnits)}, HasBattlefieldCollider={_battlefieldCollider != null}",
                this);
        }
    }

    public void SubmitGlobalOrder(string rawOrderText)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] SubmitGlobalOrder called before Initialize.", this);
            return;
        }

        StringBuilder sb = new StringBuilder(768);
        sb.AppendLine("<color=#4FC3F7><b>[GLOBAL]</b></color>");
        sb.AppendLine("<color=#B3E5FC>Global order received.</color>");

        for (int i = 0; i < _allyUnits.Length; i++)
            sb.AppendLine(BuildGlobalAllyLine(i + 1, _allyUnits[i]));

        sb.Append("<color=#FFCC80>Raw order:</color> \"");
        sb.Append(SanitizeRawText(rawOrderText));
        sb.AppendLine("\"");

        sb.AppendLine("<color=#81C784>LLM으로 직접 전달합니다.</color>");
        Debug.Log(sb.ToString(), this);

        List<BattleRuntimeUnit> resolvedActors = ResolveActorsForGlobalOrder(rawOrderText);

        if (resolvedActors.Count == 0)
        {
            BattleRuntimeUnit resolvedActor = ResolveActorForOrder("GLOBAL", null, rawOrderText);
            TrySendOrderToLlm("GLOBAL", resolvedActor, rawOrderText);
            return;
        }

        if (resolvedActors.Count > 1)
        {
            StringBuilder multiSb = new StringBuilder(256);
            multiSb.AppendLine("<color=#4FC3F7><b>[GLOBAL MULTI]</b></color>");
            multiSb.Append("<color=#B3E5FC>Resolved actors:</color> ");
            for (int i = 0; i < resolvedActors.Count; i++)
            {
                if (i > 0)
                    multiSb.Append(", ");

                BattleRuntimeUnit actor = resolvedActors[i];
                multiSb.Append(BuildUnitId(actor));
                multiSb.Append('(');
                multiSb.Append(actor != null ? actor.DisplayName : "Unknown");
                multiSb.Append(')');
            }
            multiSb.AppendLine();
            multiSb.AppendLine("<color=#81C784>각 아군별로 독립 LLM 요청을 보냅니다.</color>");
            Debug.Log(multiSb.ToString(), this);
        }

        for (int i = 0; i < resolvedActors.Count; i++)
        {
            TrySendOrderToLlm("GLOBAL", resolvedActors[i], rawOrderText);
        }
    }

    public void SubmitSingleOrder(BattleRuntimeUnit targetAlly, string rawOrderText)
    {
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] SubmitSingleOrder called before Initialize.", this);
            return;
        }

        if (targetAlly == null)
        {
            Debug.LogWarning("[BattleOrdersManager] SubmitSingleOrder ignored. Target ally is null.", this);
            return;
        }

        if (targetAlly.IsEnemy)
        {
            Debug.LogWarning("[BattleOrdersManager] SubmitSingleOrder ignored. Target is an enemy unit.", this);
            return;
        }

        StringBuilder sb = new StringBuilder(384);
        sb.AppendLine("<color=#BA68C8><b>[SINGLE]</b></color>");
        sb.AppendLine("<color=#E1BEE7>Single target order received.</color>");
        sb.Append("<color=#81C784>Target ally:</color> ");
        sb.AppendLine(BuildUnitIdentityText(targetAlly));
        sb.Append("<color=#81C784>Target unitId:</color> ");
        sb.AppendLine(BuildUnitId(targetAlly));
        sb.Append("<color=#FFCC80>Raw order:</color> \"");
        sb.Append(SanitizeRawText(rawOrderText));
        sb.Append('"');
        Debug.Log(sb.ToString(), this);

        TrySendOrderToLlm("SINGLE", targetAlly, rawOrderText);
    }

    // ══════════════════════════════════════════════════════════════
    // TrySendOrderToLlm : Gemini Direct / Proxy 분기
    // ══════════════════════════════════════════════════════════════
    private void TrySendOrderToLlm(string orderType, BattleRuntimeUnit actorUnit, string rawOrderText)
    {
        if (!sendOrdersToLlm)
            return;
        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] LLM send blocked. Manager is not initialized.", this);
            return;
        }
        if (actorUnit != null && actorUnit.IsEnemy)
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. Actor unit is an enemy.", this);
            return;
        }

        string sanitizedRawText = SanitizeRawText(rawOrderText);
        if (string.IsNullOrWhiteSpace(sanitizedRawText))
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. Raw order text is empty.", this);
            return;
        }

        BattleRuntimeUnit resolvedActor = ResolveActorForOrder(orderType, actorUnit, sanitizedRawText);

        NotifyForcedActionSuspendOnLlmOrder(orderType, resolvedActor, sanitizedRawText);

        string systemInstruction = BuildSystemInstruction(resolvedActor);
        string userPayloadJson = BuildUserPayloadJson(resolvedActor, sanitizedRawText);

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ▼▼▼ Gemini Direct 분기 ▼▼▼
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        if (useGeminiDirect)
        {
            if (string.IsNullOrWhiteSpace(geminiApiKey))
            {
                Debug.LogWarning("[BattleOrdersManager] Gemini API Key가 비어있습니다. Inspector에서 입력하세요.", this);
                return;
            }

            StartCoroutine(SendToGeminiDirectly(orderType, resolvedActor, sanitizedRawText, systemInstruction, userPayloadJson));
            return;
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 기존 Proxy 경로
        if (string.IsNullOrWhiteSpace(llmProxyUrl))
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. Proxy URL is empty.", this);
            return;
        }
        if (string.IsNullOrWhiteSpace(appSharedToken))
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. App shared token is empty.", this);
            return;
        }

        StartCoroutine(SendOrderToLlmCoroutine(orderType, resolvedActor, sanitizedRawText, systemInstruction, userPayloadJson));
    }

    private BattleRuntimeUnit ResolveActorForOrder(string orderType, BattleRuntimeUnit actorUnit, string sanitizedRawText)
    {
        if (actorUnit != null)
            return actorUnit;

        if (!string.Equals(orderType, "GLOBAL", StringComparison.OrdinalIgnoreCase))
            return null;

        if (TryExtractFirstAllyNumberFromText(sanitizedRawText, out int allyNumber))
        {
            int allyIndex = allyNumber - 1;
            if (allyIndex >= 0 && allyIndex < _allyUnits.Length)
                return _allyUnits[allyIndex];
        }

        return null;
    }

    private List<BattleRuntimeUnit> ResolveActorsForGlobalOrder(string rawOrderText)
    {
        List<BattleRuntimeUnit> result = new List<BattleRuntimeUnit>(2);
        HashSet<int> addedUnitNumbers = new HashSet<int>();

        string text = SanitizeRawText(rawOrderText);
        if (string.IsNullOrWhiteSpace(text))
            return result;

        MatchCollection numberMatches = Regex.Matches(text, @"(?:아군|ally)\s*([1-6])", RegexOptions.IgnoreCase);
        for (int i = 0; i < numberMatches.Count; i++)
        {
            Match match = numberMatches[i];
            if (!match.Success)
                continue;

            if (!int.TryParse(match.Groups[1].Value, out int allyNumber))
                continue;

            int allyIndex = allyNumber - 1;
            if (allyIndex < 0 || allyIndex >= _allyUnits.Length)
                continue;

            BattleRuntimeUnit unit = _allyUnits[allyIndex];
            if (unit == null || unit.IsEnemy)
                continue;

            if (!addedUnitNumbers.Add(unit.UnitNumber))
                continue;

            result.Add(unit);
        }

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _allyUnits[i];
            if (unit == null)
                continue;

            string displayName = unit.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            if (text.IndexOf(displayName, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!addedUnitNumbers.Add(unit.UnitNumber))
                continue;

            result.Add(unit);
        }

        return result;
    }

    private void NotifyForcedActionSuspendOnLlmOrder(string orderType, BattleRuntimeUnit actorUnit, string sanitizedRawText)
    {
        if (_battleSimulationManager == null)
        {
            _battleSimulationManager = FindAnyObjectByType<BattleSimulationManager>();
            if (_battleSimulationManager == null)
                return;
        }

        if (actorUnit != null && !actorUnit.IsEnemy)
        {
            _battleSimulationManager.SuspendForcedActionForAllyTemporarily(actorUnit.UnitNumber);
            return;
        }

        if (!string.Equals(orderType, "GLOBAL", StringComparison.OrdinalIgnoreCase))
            return;

        if (TryExtractFirstAllyNumberFromText(sanitizedRawText, out int allyNumber))
        {
            _battleSimulationManager.SuspendForcedActionForAllyTemporarily(allyNumber);
        }
    }

    private static bool TryExtractFirstAllyNumberFromText(string text, out int allyNumber)
    {
        allyNumber = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        Match match = Regex.Match(text, @"(?:아군|ally)\s*([1-6])", RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        return int.TryParse(match.Groups[1].Value, out allyNumber);
    }

    // ══════════════════════════════════════════════════════════════
    // ▼▼▼ 새로 추가된 Gemini Direct 호출 코루틴 ▼▼▼
    // ══════════════════════════════════════════════════════════════
    private IEnumerator SendToGeminiDirectly(
        string orderType,
        BattleRuntimeUnit actorUnit,
        string sanitizedRawText,
        string systemInstruction,
        string userPayloadJson)
    {
        int requestId = ++_requestSequence;

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{geminiModel}:generateContent?key={geminiApiKey}";

        // Gemini REST API 요청 JSON 구성
        // system_instruction과 contents를 분리해서 보내는 것이 Gemini 권장 방식
        string escapedSystem = EscapeJsonString(systemInstruction);
        string escapedUser = EscapeJsonString(userPayloadJson);

        string requestJson = "{"
            + "\"system_instruction\":{\"parts\":[{\"text\":\"" + escapedSystem + "\"}]},"
            + "\"contents\":[{\"role\":\"user\",\"parts\":[{\"text\":\"" + escapedUser + "\"}]}],"
            + "\"generationConfig\":{\"temperature\":0.2,\"maxOutputTokens\":256,\"responseMimeType\":\"application/json\"}"
            + "}";

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Gemini Direct 요청 전송\n" +
                $"RequestId={requestId}\n" +
                $"Model={geminiModel}\n" +
                $"Actor={BuildUnitIdentityText(actorUnit)}\n" +
                $"Raw=\"{sanitizedRawText}\"",
                this);
        }

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestJson);

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = requestTimeoutSeconds;

        yield return request.SendWebRequest();

        // ── 네트워크 오류 처리 ──
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                $"[BattleOrdersManager] Gemini 요청 실패. RequestId={requestId}\n" +
                $"Error={request.error}\n" +
                $"Response={request.downloadHandler.text}",
                this);
            yield break;
        }

        string rawResponse = request.downloadHandler.text;

        if (verboseLog)
        {
            Debug.Log(
                $"<color=#FFD54F><b>[GEMINI RESPONSE RAW]</b></color>\n" +
                $"<color=#FFF59D>RequestId:</color> {requestId}\n" +
                $"<color=#FFF59D>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n" +
                $"<color=#FFF59D>RawJson:</color> {rawResponse}",
                this);
        }

        // ── Gemini 응답에서 텍스트 추출 ──
        // Gemini 응답 구조: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
        string responseText = ExtractGeminiResponseText(rawResponse);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            Debug.LogError(
                $"[BattleOrdersManager] Gemini 응답에서 텍스트를 추출하지 못했습니다. RequestId={requestId}\nRaw={rawResponse}",
                this);
            yield break;
        }

        Debug.Log(
            $"<color=#FFD54F><b>[GEMINI TEXT]</b></color>\n" +
            $"<color=#FFF59D>RequestId:</color> {requestId}\n" +
            $"<color=#FFF59D>Text:</color> {responseText}",
            this);

        // ── 기존 파싱 로직 그대로 재사용 ──
        if (!TryParseLlmResponse(responseText, out BattleLlmResponseDto parsedResponse, out string parseError))
        {
            Debug.LogError(
                $"<color=#FF8A80><b>[GEMINI PARSE FAILED]</b></color>\n" +
                $"<color=#FFCDD2>RequestId:</color> {requestId}\n" +
                $"<color=#FFCDD2>Reason:</color> {parseError}\n" +
                $"<color=#FFCDD2>RawText:</color> {responseText}",
                this);
            yield break;
        }

        List<string> validationErrors = ValidateLlmResponse(actorUnit, parsedResponse);

        if (validationErrors.Count > 0)
        {
            Debug.LogWarning(
                $"<color=#FFB74D><b>[GEMINI VALIDATION FAILED]</b></color>\n" +
                $"<color=#FFE0B2>RequestId:</color> {requestId}\n" +
                $"<color=#FFE0B2>Errors:</color>\n{BuildValidationErrorSummary(validationErrors)}\n" +
                $"<color=#FFE0B2>ParsedResponse:</color>\n{BuildParsedResponseSummary(parsedResponse)}",
                this);
            yield break;
        }

        Debug.Log(
            $"<color=#81C784><b>[GEMINI SUCCESS]</b></color>\n" +
            $"<color=#C8E6C9>RequestId:</color> {requestId}\n" +
            $"<color=#C8E6C9>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n" +
            $"<color=#C8E6C9>ParsedResponse:</color>\n{BuildParsedResponseSummary(parsedResponse)}",
            this);

        HandleValidatedResponse(requestId, actorUnit, sanitizedRawText, parsedResponse, "GEMINI");
    }

    // ── Gemini 응답 JSON에서 텍스트 부분만 추출 ──────────────────
    private static string ExtractGeminiResponseText(string geminiResponseJson)
    {
        // Gemini 응답: {"candidates":[{"content":{"parts":[{"text":"실제텍스트"}]}}]}
        // "text" 키의 값을 추출
        const string marker = "\"text\":";
        int markerIdx = geminiResponseJson.IndexOf(marker, StringComparison.Ordinal);
        if (markerIdx < 0)
            return null;

        int valueStart = SkipWhitespace(geminiResponseJson, markerIdx + marker.Length);
        if (valueStart >= geminiResponseJson.Length || geminiResponseJson[valueStart] != '"')
            return null;

        int stringEnd = FindStringEnd(geminiResponseJson, valueStart);
        if (stringEnd < 0)
            return null;

        string encoded = geminiResponseJson.Substring(valueStart + 1, stringEnd - valueStart - 1);
        return UnescapeJsonString(encoded);
    }

    // ── JSON 문자열 이스케이프 (요청 JSON 구성용) ─────────────────
    private static string EscapeJsonString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "")
            .Replace("\t", "\\t");
    }
    // ══════════════════════════════════════════════════════════════
    // ▲▲▲ 새로 추가된 Gemini Direct 코드 끝 ▲▲▲
    // ══════════════════════════════════════════════════════════════

    // ── 기존 Proxy 코루틴 (그대로 유지) ──────────────────────────
    private IEnumerator SendOrderToLlmCoroutine(
        string orderType,
        BattleRuntimeUnit actorUnit,
        string sanitizedRawText,
        string systemInstruction,
        string userPayloadJson)
    {
        int requestId = ++_requestSequence;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Sending structured order to LLM.\n" +
                $"RequestId={requestId}\nType={orderType}\n" +
                $"Actor={BuildUnitIdentityText(actorUnit)}\n" +
                $"Raw=\"{sanitizedRawText}\"",
                this);
        }

        string responseBackendId = null;
        string responseProvider = null;
        string responseModel = null;
        string responseText = null;
        string errorText = null;

        string backendId = GetSelectedBackendId(selectedLlmBackend);

        yield return BattleOrdersHttpClient.PostCommand(
            llmProxyUrl,
            appSharedToken,
            backendId,
            systemInstruction,
            userPayloadJson,
            requestTimeoutSeconds,
            onSuccess: (returnedBackendId, provider, model, text) =>
            {
                responseBackendId = returnedBackendId;
                responseProvider = provider;
                responseModel = model;
                responseText = text;
            },
            onError: error => errorText = error);

        if (!string.IsNullOrEmpty(errorText))
        {
            Debug.LogError(
                $"[BattleOrdersManager] LLM request failed. RequestId={requestId}, Error={errorText}",
                this);
            yield break;
        }

        if (!TryParseLlmResponse(responseText, out BattleLlmResponseDto parsedResponse, out string parseError))
        {
            Debug.LogError(
                $"<color=#FF8A80><b>[LLM PARSE FAILED]</b></color>\n" +
                $"Reason: {parseError}\nRawText: {responseText}",
                this);
            yield break;
        }

        List<string> validationErrors = ValidateLlmResponse(actorUnit, parsedResponse);

        if (validationErrors.Count > 0)
        {
            Debug.LogWarning(
                $"<color=#FFB74D><b>[LLM VALIDATION FAILED]</b></color>\n" +
                $"{BuildValidationErrorSummary(validationErrors)}",
                this);
            yield break;
        }

        Debug.Log(
            $"<color=#81C784><b>[LLM VALIDATED SUCCESS]</b></color>\n" +
            $"{BuildParsedResponseSummary(parsedResponse)}",
            this);

        HandleValidatedResponse(requestId, actorUnit, sanitizedRawText, parsedResponse, "PROXY");
    }

    private void HandleValidatedResponse(int requestId, BattleRuntimeUnit actorUnit, string sanitizedRawText, BattleLlmResponseDto parsedResponse, string sourceTag)
    {
        bool applied = TryApplyParsedResponseToIntent(actorUnit, parsedResponse, sanitizedRawText, out string applySummary, out TacticalIntentType appliedIntentType);

        Debug.Log(
            $"<color=#64B5F6><b>[LLM APPLY]</b></color>\n" +
            $"<color=#BBDEFB>Source:</color> {sourceTag}\n" +
            $"<color=#BBDEFB>RequestId:</color> {requestId}\n" +
            $"<color=#BBDEFB>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n" +
            $"<color=#BBDEFB>RawOrder:</color> \"{sanitizedRawText}\"\n" +
            $"<color=#BBDEFB>Applied:</color> {applied}\n" +
            $"<color=#BBDEFB>Detail:</color> {applySummary}",
            this);

        if (actorUnit != null)
        {
            StartCoroutine(VerifyExecutionAfterLlmResponse(requestId, actorUnit, parsedResponse, applySummary, appliedIntentType));
        }
    }

    private bool TryApplyParsedResponseToIntent(
        BattleRuntimeUnit actorUnit,
        BattleLlmResponseDto parsedResponse,
        string rawOrderText,
        out string summary,
        out TacticalIntentType appliedIntentType)
    {
        summary = string.Empty;
        appliedIntentType = TacticalIntentType.None;

        if (actorUnit == null)
        {
            summary = "Actor is UNKNOWN. Could not apply intent.";
            return false;
        }

        if (actorUnit.IsCombatDisabled)
        {
            summary = "Actor is disabled.";
            return false;
        }

        if (parsedResponse?.output == null)
        {
            summary = "Response output is null.";
            return false;
        }

        if (_intentExecutor == null)
            _intentExecutor = FindAnyObjectByType<TacticalIntentExecutor>();

        if (_intentExecutor == null)
        {
            summary = "TacticalIntentExecutor not found in scene.";
            return false;
        }

        TacticalIntentType intentType = ParseIntentType(parsedResponse.output.intent);
        if (intentType == TacticalIntentType.None)
        {
            summary = $"Unknown intent '{parsedResponse.output.intent}'.";
            return false;
        }

        bool retreatOrder = IsRetreatOrderText(rawOrderText);
        if (retreatOrder && intentType != TacticalIntentType.Retreat)
        {
            summary = $"Retreat keyword detected in order. Overrode intent '{parsedResponse.output.intent}' -> 'retreat'.";
            intentType = TacticalIntentType.Retreat;
            parsedResponse.output.intent = "retreat";
        }

        SkillUsagePolicy skillPolicy = ParseSkillUsagePolicy(parsedResponse.output.skillUsagePolicy);
        PositioningStyle positioning = ParsePositioningStyle(parsedResponse.output.positioning);

        BattleRuntimeUnit primaryTarget = null;
        BattleRuntimeUnit secondaryTarget = null;

        string targetUnitId = parsedResponse.output.targetUnitId;
        if (!string.IsNullOrWhiteSpace(targetUnitId))
        {
            BattleRuntimeUnit resolvedTarget = FindUnitByUnitId(targetUnitId);
            if (resolvedTarget != null)
            {
                if (resolvedTarget.IsEnemy)
                    primaryTarget = resolvedTarget;
                else
                    secondaryTarget = resolvedTarget;
            }
        }

        if (intentType == TacticalIntentType.Assassinate || intentType == TacticalIntentType.Vanguard)
        {
            if (primaryTarget == null || primaryTarget.IsCombatDisabled)
                primaryTarget = FindNearestLivingEnemy(actorUnit);
        }
        else if (intentType == TacticalIntentType.Support)
        {
            if (secondaryTarget == null || secondaryTarget.IsEnemy || secondaryTarget.IsCombatDisabled)
                secondaryTarget = FindNearestLivingAlly(actorUnit);
            if (primaryTarget == null || primaryTarget.IsCombatDisabled)
                primaryTarget = FindNearestLivingEnemy(actorUnit);
        }

        TacticalIntent intent = new TacticalIntent(intentType, skillPolicy, positioning, primaryTarget, secondaryTarget);
        _intentExecutor.SetIntent(actorUnit, intent);
        appliedIntentType = intentType;

        if (!string.IsNullOrEmpty(summary))
        {
            summary += " ";
        }

        summary += $"Mapped Intent={intentType}, Skill={skillPolicy}, Pos={positioning}, Primary={BuildUnitId(primaryTarget)}, Secondary={BuildUnitId(secondaryTarget)}.";
        return true;
    }

    private IEnumerator VerifyExecutionAfterLlmResponse(
        int requestId,
        BattleRuntimeUnit actorUnit,
        BattleLlmResponseDto parsedResponse,
        string applySummary,
        TacticalIntentType appliedIntentType)
    {
        if (actorUnit == null)
            yield break;

        BattleActionType startAction = actorUnit.CurrentActionType;
        TacticalIntentType intentType = appliedIntentType != TacticalIntentType.None
            ? appliedIntentType
            : ParseIntentType(parsedResponse?.output?.intent);
        BattleActionType expectedAction = MapIntentToExpectedAction(intentType);
        BattleRuntimeUnit expectedTarget = FindUnitByUnitId(parsedResponse?.output?.targetUnitId);

        float observeUntil = Time.unscaledTime + 2.5f;
        bool matched = false;
        string evidence = "No matching evidence yet.";

        while (Time.unscaledTime < observeUntil)
        {
            if (actorUnit == null || actorUnit.IsCombatDisabled)
                break;

            if (expectedTarget != null)
            {
                bool targetingExpected = actorUnit.IntentOverrideTarget == expectedTarget
                    || actorUnit.PlannedTargetEnemy == expectedTarget
                    || actorUnit.CurrentTarget == expectedTarget;

                if (targetingExpected)
                {
                    matched = true;
                    evidence = $"Target link matched. CurrentAction={actorUnit.CurrentActionType}, Target={BuildUnitId(expectedTarget)}";
                    break;
                }
            }

            if (expectedAction != BattleActionType.None && actorUnit.CurrentActionType == expectedAction)
            {
                matched = true;
                evidence = $"Action matched expected intent action. Expected={expectedAction}, Current={actorUnit.CurrentActionType}";
                break;
            }

            yield return null;
        }

        BattleActionType forcedAction = BattleActionType.None;
        bool forcedSuspended = false;
        float suspendRemain = 0f;

        if (_battleSimulationManager == null)
            _battleSimulationManager = FindAnyObjectByType<BattleSimulationManager>();

        if (_battleSimulationManager != null)
        {
            _battleSimulationManager.TryGetForcedActionDebugState(
                actorUnit.UnitNumber,
                out forcedAction,
                out forcedSuspended,
                out suspendRemain);
        }

        Debug.Log(
            $"<color=#4DB6AC><b>[LLM EXEC CHECK]</b></color>\n" +
            $"<color=#B2DFDB>RequestId:</color> {requestId}\n" +
            $"<color=#B2DFDB>Actor:</color> {BuildUnitId(actorUnit)}\n" +
            $"<color=#B2DFDB>ApplySummary:</color> {applySummary}\n" +
            $"<color=#B2DFDB>Matched:</color> {matched}\n" +
            $"<color=#B2DFDB>Evidence:</color> {evidence}\n" +
            $"<color=#B2DFDB>ActionBefore:</color> {startAction}\n" +
            $"<color=#B2DFDB>ActionNow:</color> {actorUnit.CurrentActionType}\n" +
            $"<color=#B2DFDB>HasIntentOverride:</color> {actorUnit.HasIntentOverride}\n" +
            $"<color=#B2DFDB>IntentTarget:</color> {BuildUnitId(actorUnit.IntentOverrideTarget)}\n" +
            $"<color=#B2DFDB>ForcedAction:</color> {forcedAction}\n" +
            $"<color=#B2DFDB>ForcedSuspended:</color> {forcedSuspended} (remain={suspendRemain:0.00}s)",
            this);
    }

    private BattleRuntimeUnit FindUnitByUnitId(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
            return null;

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _allyUnits[i];
            if (unit != null && string.Equals(BuildUnitId(unit), unitId, StringComparison.OrdinalIgnoreCase))
                return unit;
        }

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _enemyUnits[i];
            if (unit != null && string.Equals(BuildUnitId(unit), unitId, StringComparison.OrdinalIgnoreCase))
                return unit;
        }

        return null;
    }

    private BattleRuntimeUnit FindNearestLivingEnemy(BattleRuntimeUnit actorUnit)
    {
        if (actorUnit == null)
            return null;

        BattleRuntimeUnit nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            BattleRuntimeUnit enemy = _enemyUnits[i];
            if (enemy == null || enemy.IsCombatDisabled)
                continue;

            Vector3 delta = enemy.Position - actorUnit.Position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private BattleRuntimeUnit FindNearestLivingAlly(BattleRuntimeUnit actorUnit)
    {
        if (actorUnit == null)
            return null;

        BattleRuntimeUnit nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit ally = _allyUnits[i];
            if (ally == null || ally.IsCombatDisabled || ally == actorUnit)
                continue;

            Vector3 delta = ally.Position - actorUnit.Position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = ally;
            }
        }

        return nearest;
    }

    private static TacticalIntentType ParseIntentType(string token)
    {
        switch (NormalizeToken(token))
        {
            case "assassinate":
            case "focus":
            case "engage":
                return TacticalIntentType.Assassinate;
            case "support":
            case "peel":
                return TacticalIntentType.Support;
            case "vanguard":
            case "push":
            case "collapse":
                return TacticalIntentType.Vanguard;
            case "holdline":
            case "hold_line":
            case "hold":
                return TacticalIntentType.HoldLine;
            case "retreat":
            case "withdraw":
            case "fallback":
            case "runaway":
            case "run_away":
            case "disengage":
                return TacticalIntentType.Retreat;
            // KITE는 사거리 유지형 교전을 의미하며 실행 단계에서 keep-max-range 이동으로 해석한다.
            case "kite":
            case "kiting":
            case "skirmish":
                return TacticalIntentType.Kite;
            // REGROUP은 아군 대열로 복귀/집결하는 의도다.
            case "regroup":
            case "rally":
            case "groupup":
            case "group_up":
            case "assemble":
                return TacticalIntentType.Regroup;
            default:
                return TacticalIntentType.None;
        }
    }

    private static SkillUsagePolicy ParseSkillUsagePolicy(string token)
    {
        switch (NormalizeToken(token))
        {
            case "saveforcritical":
            case "save_for_critical":
                return SkillUsagePolicy.SaveForCritical;
            case "initiative":
                return SkillUsagePolicy.Initiative;
            case "reactive":
                return SkillUsagePolicy.Reactive;
            default:
                return SkillUsagePolicy.OnCooldown;
        }
    }

    private static PositioningStyle ParsePositioningStyle(string token)
    {
        switch (NormalizeToken(token))
        {
            case "keepmaxrange":
            case "keep_max_range":
                return PositioningStyle.KeepMaxRange;
            case "flanking":
                return PositioningStyle.Flanking;
            default:
                return PositioningStyle.CloseQuarter;
        }
    }

    private static BattleActionType MapIntentToExpectedAction(TacticalIntentType intentType)
    {
        switch (intentType)
        {
            case TacticalIntentType.Assassinate:
                return BattleActionType.AssassinateIsolatedEnemy;
            case TacticalIntentType.Support:
                return BattleActionType.PeelForWeakAlly;
            case TacticalIntentType.Vanguard:
                return BattleActionType.CollapseOnCluster;
            case TacticalIntentType.HoldLine:
                return BattleActionType.EngageNearest;
            case TacticalIntentType.Retreat:
                return BattleActionType.EscapeFromPressure;
            // KITE intent는 교전 액션을 유지하되 이동 목표만 사거리 중심으로 보정한다.
            case TacticalIntentType.Kite:
                return BattleActionType.EngageNearest;
            // REGROUP은 전열 합류 목적이므로 전용 집결 액션으로 매핑한다.
            case TacticalIntentType.Regroup:
                return BattleActionType.RegroupToAllies;
            default:
                return BattleActionType.None;
        }
    }

    private static bool IsRetreatOrderText(string rawOrderText)
    {
        string normalized = NormalizeToken(rawOrderText);
        if (string.IsNullOrEmpty(normalized))
            return false;

        return normalized.Contains("도망")
            || normalized.Contains("후퇴")
            || normalized.Contains("퇴각")
            || normalized.Contains("물러")
            || normalized.Contains("빠져")
            || normalized.Contains("retreat")
            || normalized.Contains("withdraw")
            || normalized.Contains("fallback")
            || normalized.Contains("disengage")
            || normalized.Contains("run away")
            || normalized.Contains("runaway");
    }

    private BattleRuntimeUnit FindNearestLivingEnemyToPosition(Vector3 position)
    {
        BattleRuntimeUnit nearest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            BattleRuntimeUnit enemy = _enemyUnits[i];
            if (enemy == null || enemy.IsCombatDisabled)
                continue;

            Vector3 delta = enemy.Position - position;
            delta.y = 0f;
            float distSqr = delta.sqrMagnitude;
            if (distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = enemy;
            }
        }

        return nearest;
    }

    // ── 이하 기존 코드 전부 그대로 ────────────────────────────────

    private bool TryResolveActorFromGlobalOrder(
        string rawOrderText,
        out BattleRuntimeUnit resolvedActor,
        out int matchedCount,
        out string matchedSummary)
    {
        resolvedActor = null;
        matchedCount = 0;

        List<BattleRuntimeUnit> matchedUnits = new List<BattleRuntimeUnit>(2);
        string searchText = rawOrderText ?? string.Empty;

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _allyUnits[i];
            if (unit == null)
                continue;

            string displayName = unit.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            if (searchText.IndexOf(displayName, StringComparison.Ordinal) >= 0)
                matchedUnits.Add(unit);
        }

        matchedCount = matchedUnits.Count;

        if (matchedUnits.Count == 0)
        {
            matchedSummary = "No ally name matched exactly.";
            return false;
        }

        if (matchedUnits.Count > 1)
        {
            StringBuilder sb = new StringBuilder(128);
            sb.Append("Multiple ally names matched: ");
            for (int i = 0; i < matchedUnits.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                BattleRuntimeUnit unit = matchedUnits[i];
                sb.Append(unit != null ? unit.DisplayName : "Unknown");
            }
            matchedSummary = sb.ToString();
            return false;
        }

        resolvedActor = matchedUnits[0];
        matchedSummary = $"Matched ally: {resolvedActor.DisplayName}";
        return true;
    }

    private bool TryParseLlmResponse(
        string rawResponseText,
        out BattleLlmResponseDto parsedResponse,
        out string parseError)
    {
        parsedResponse = null;
        parseError = null;

        string jsonText = ExtractLikelyJsonObjectText(rawResponseText);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            parseError = "Could not extract a valid JSON object from raw response text.";
            return false;
        }

        string outputJson;
        if (!TryExtractObjectProperty(jsonText, "output", out outputJson))
            outputJson = jsonText;

        BattleLlmResponseOutputDto outputDto = new BattleLlmResponseOutputDto();

        if (!TryExtractJsonStringProperty(outputJson, "thinking", out outputDto.thinking))
        {
            parseError = "Missing or invalid output.thinking string.";
            return false;
        }

        if (!TryExtractJsonStringProperty(outputJson, "dialog", out outputDto.dialog))
        {
            parseError = "Missing or invalid output.dialog string.";
            return false;
        }

        if (!TryExtractJsonStringProperty(outputJson, "intent", out outputDto.intent))
        {
            parseError = "Missing or invalid output.intent string.";
            return false;
        }

        TryExtractJsonStringProperty(outputJson, "skill_usage_policy", out outputDto.skillUsagePolicy);
        if (string.IsNullOrWhiteSpace(outputDto.skillUsagePolicy))
            TryExtractJsonStringProperty(outputJson, "skillUsagePolicy", out outputDto.skillUsagePolicy);

        TryExtractJsonStringProperty(outputJson, "positioning", out outputDto.positioning);

        TryExtractJsonStringProperty(outputJson, "target", out outputDto.targetUnitId);
        if (string.IsNullOrWhiteSpace(outputDto.targetUnitId))
            TryExtractJsonStringProperty(outputJson, "targetUnitId", out outputDto.targetUnitId);

        // LLM이 스키마 예시값("optional", "none" 등)을 target에 그대로 넣는 경우를 빈 target으로 정규화한다.
        if (IsPlaceholderTargetToken(outputDto.targetUnitId))
            outputDto.targetUnitId = string.Empty;

        parsedResponse = new BattleLlmResponseDto { output = outputDto };
        return true;
    }

    private List<string> ValidateLlmResponse(BattleRuntimeUnit actorUnit, BattleLlmResponseDto parsedResponse)
    {
        List<string> errors = new List<string>();

        if (actorUnit != null && actorUnit.IsEnemy)
        {
            errors.Add("Actor unit must be an ally, but actor is enemy.");
            return errors;
        }

        string actorUnitId = BuildUnitId(actorUnit);

        HashSet<string> allyIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> enemyIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> allIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _allyUnits[i];
            if (unit == null)
                continue;
            string unitId = BuildUnitId(unit);
            allyIds.Add(unitId);
            allIds.Add(unitId);
        }

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _enemyUnits[i];
            if (unit == null)
                continue;
            string unitId = BuildUnitId(unit);
            enemyIds.Add(unitId);
            allIds.Add(unitId);
        }

        if (actorUnit != null && !allyIds.Contains(actorUnitId))
            errors.Add($"Actor unit id '{actorUnitId}' is not present in ally roster.");

        if (parsedResponse == null)
        { errors.Add("Parsed response is null."); return errors; }
        if (parsedResponse.output == null)
        { errors.Add("Parsed response.output is null."); return errors; }

        TacticalIntentType intentType = ParseIntentType(parsedResponse.output.intent);
        if (intentType == TacticalIntentType.None)
            errors.Add($"Unsupported intent '{parsedResponse.output.intent}'. Allowed: assassinate, support, vanguard, hold_line, retreat, kite, regroup.");

        string targetUnitId = parsedResponse.output.targetUnitId;
        // 파서 정규화 전에 남아있을 수 있는 플레이스홀더 target은 미지정 target으로 간주한다.
        if (IsPlaceholderTargetToken(targetUnitId))
            targetUnitId = string.Empty;

        if (!string.IsNullOrWhiteSpace(targetUnitId) && !allIds.Contains(targetUnitId))
            errors.Add($"output.target '{targetUnitId}' does not exist in current roster.");

        SkillUsagePolicy _ = ParseSkillUsagePolicy(parsedResponse.output.skillUsagePolicy);
        PositioningStyle __ = ParsePositioningStyle(parsedResponse.output.positioning);

        return errors;
    }

    private static string BuildParsedResponseSummary(BattleLlmResponseDto parsedResponse)
    {
        if (parsedResponse == null || parsedResponse.output == null)
            return "Parsed response is null.";

        StringBuilder sb = new StringBuilder(512);
        sb.Append("<b>Thinking:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.thinking) ? "(empty)" : parsedResponse.output.thinking);
        sb.Append("<b>Dialog:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.dialog) ? "(empty)" : parsedResponse.output.dialog);

        sb.Append("<b>Intent:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.intent) ? "(empty)" : parsedResponse.output.intent);
        sb.Append("<b>Target:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.targetUnitId) ? "(none)" : parsedResponse.output.targetUnitId);
        sb.Append("<b>SkillPolicy:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.skillUsagePolicy) ? "(default)" : parsedResponse.output.skillUsagePolicy);
        sb.Append("<b>Positioning:</b> ");
        sb.AppendLine(string.IsNullOrWhiteSpace(parsedResponse.output.positioning) ? "(default)" : parsedResponse.output.positioning);

        return sb.ToString();
    }

    private static string BuildValidationErrorSummary(List<string> errors)
    {
        if (errors == null || errors.Count == 0)
            return "(none)";
        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < errors.Count; i++)
        { sb.Append("- "); sb.AppendLine(errors[i]); }
        return sb.ToString();
    }

    private static string NormalizeToken(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    // target 필드에 흔히 들어오는 플레이스홀더 문자열을 감지한다.
    private static bool IsPlaceholderTargetToken(string value)
    {
        string token = NormalizeToken(value);
        if (string.IsNullOrEmpty(token))
            return false;

        return token == "optional"
            || token == "(optional)"
            || token == "none"
            || token == "null"
            || token == "n/a"
            || token == "na"
            || token == "unknown";
    }

    private static string ExtractLikelyJsonObjectText(string rawResponseText)
    {
        if (string.IsNullOrWhiteSpace(rawResponseText))
            return null;

        string trimmed = rawResponseText.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            if (firstLineBreak >= 0)
                trimmed = trimmed.Substring(firstLineBreak + 1).Trim();
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
                trimmed = trimmed.Substring(0, lastFence).Trim();
        }

        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
            return null;

        return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private static bool TryExtractObjectProperty(string json, string propertyName, out string objectJson)
    {
        objectJson = null;
        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
            return false;
        if (startIndex < 0 || endIndex < startIndex || json[startIndex] != '{')
            return false;
        objectJson = json.Substring(startIndex, endIndex - startIndex + 1);
        return true;
    }

    private static bool TryExtractArrayProperty(string json, string propertyName, out string arrayJson)
    {
        arrayJson = null;
        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
            return false;
        if (startIndex < 0 || endIndex < startIndex || json[startIndex] != '[')
            return false;
        arrayJson = json.Substring(startIndex, endIndex - startIndex + 1);
        return true;
    }

    private static bool TryExtractJsonStringProperty(string json, string propertyName, out string value)
    {
        value = null;
        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
            return false;
        if (startIndex < 0 || endIndex <= startIndex || json[startIndex] != '"' || json[endIndex] != '"')
            return false;
        string encoded = json.Substring(startIndex + 1, endIndex - startIndex - 1);
        value = UnescapeJsonString(encoded);
        return true;
    }

    private static bool TryExtractFloatPairProperty(string json, string propertyName, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
            return false;
        if (startIndex < 0 || endIndex <= startIndex || json[startIndex] != '[' || json[endIndex] != ']')
            return false;

        string arrayJson = json.Substring(startIndex, endIndex - startIndex + 1);
        Match match = Regex.Match(arrayJson, @"^\[\s*(-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)\s*,\s*(-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)\s*\]$");
        if (!match.Success)
            return false;

        if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            return false;
        if (!float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            return false;
        return true;
    }

    private static bool TrySplitTopLevelObjectArray(string arrayJson, out List<string> objects, out string error)
    {
        objects = new List<string>();
        error = null;

        if (string.IsNullOrWhiteSpace(arrayJson))
        { error = "Array JSON is empty."; return false; }
        if (arrayJson[0] != '[' || arrayJson[arrayJson.Length - 1] != ']')
        { error = "Array JSON does not start/end with '[' and ']'."; return false; }

        int index = 1;
        while (index < arrayJson.Length - 1)
        {
            index = SkipWhitespace(arrayJson, index);
            if (index >= arrayJson.Length - 1)
                break;
            if (arrayJson[index] == ',')
            { index++; continue; }
            if (arrayJson[index] != '{')
            { error = $"Unexpected token '{arrayJson[index]}' inside action array."; return false; }

            int endIndex = FindMatchingBracket(arrayJson, index, '{', '}');
            if (endIndex < 0)
            { error = "Failed to find matching '}' in action array."; return false; }

            objects.Add(arrayJson.Substring(index, endIndex - index + 1));
            index = endIndex + 1;
        }

        return true;
    }

    private static bool TryGetPropertyValueSpan(string json, string propertyName, out int startIndex, out int endIndex)
    {
        startIndex = -1;
        endIndex = -1;
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(propertyName))
            return false;

        Match match = Regex.Match(json, "\"" + Regex.Escape(propertyName) + "\"\\s*:");
        if (!match.Success)
            return false;

        int valueStart = SkipWhitespace(json, match.Index + match.Length);
        if (valueStart < 0 || valueStart >= json.Length)
            return false;

        char firstChar = json[valueStart];

        if (firstChar == '{')
        {
            int objectEnd = FindMatchingBracket(json, valueStart, '{', '}');
            if (objectEnd < 0)
                return false;
            startIndex = valueStart;
            endIndex = objectEnd;
            return true;
        }

        if (firstChar == '[')
        {
            int arrayEnd = FindMatchingBracket(json, valueStart, '[', ']');
            if (arrayEnd < 0)
                return false;
            startIndex = valueStart;
            endIndex = arrayEnd;
            return true;
        }

        if (firstChar == '"')
        {
            int stringEnd = FindStringEnd(json, valueStart);
            if (stringEnd < 0)
                return false;
            startIndex = valueStart;
            endIndex = stringEnd;
            return true;
        }

        int primitiveEnd = valueStart;
        while (primitiveEnd < json.Length && json[primitiveEnd] != ',' && json[primitiveEnd] != '}' && json[primitiveEnd] != ']')
            primitiveEnd++;
        primitiveEnd--;

        if (primitiveEnd < valueStart)
            return false;
        startIndex = valueStart;
        endIndex = primitiveEnd;
        return true;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index;
    }

    private static int FindMatchingBracket(string text, int startIndex, char openChar, char closeChar)
    {
        bool inString = false;
        int depth = 0;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (inString)
            {
                if (c == '\\')
                { i++; continue; }
                if (c == '"')
                    inString = false;
                continue;
            }
            if (c == '"')
            { inString = true; continue; }
            if (c == openChar)
            { depth++; continue; }
            if (c == closeChar)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }
        return -1;
    }

    private static int FindStringEnd(string text, int startIndex)
    {
        for (int i = startIndex + 1; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\')
            { i++; continue; }
            if (c == '"')
                return i;
        }
        return -1;
    }

    private static string UnescapeJsonString(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return string.Empty;
        string normalized = encoded.Replace("\\/", "/");
        return Regex.Unescape(normalized);
    }

    private string BuildSystemInstruction(BattleRuntimeUnit actorUnit)
    {
        string actorUnitId = BuildUnitId(actorUnit);
        StringBuilder sb = new StringBuilder(1024);
        sb.Append("You are generating one battle-order response for a single allied gladiator in a Unity battle prototype. ");
        sb.Append("The acting ally unit id is ");
        sb.Append(actorUnitId);
        sb.Append(". ");
        sb.Append("The payload JSON already contains system_prompt, user_input, and an empty output template. ");
        sb.Append("Use the actor personality from payload.system_prompt.personality. ");
        sb.Append("Return only one valid JSON object with exactly this top-level shape: ");
        sb.Append("{\"output\":{\"thinking\":\"string\",\"dialog\":\"string\",\"intent\":\"string\",\"target\":\"A_01|E_01|optional\",\"skill_usage_policy\":\"on_cooldown|save_for_critical|initiative|reactive\",\"positioning\":\"keep_max_range|close_quarter|flanking\"}}. ");
        sb.Append("Do not add markdown, code fences, or extra commentary. ");
        sb.Append("Keep output concise: thinking <= 120 chars, dialog <= 80 chars. ");
        sb.Append("Never explain alternatives or long analysis. ");
        sb.Append("Choose intent from: assassinate, support, vanguard, hold_line, retreat, kite, regroup. ");
        sb.Append("If command asks to run away/retreat (Korean: 도망, 후퇴, 퇴각), intent must be retreat. ");
        sb.Append("If command asks to keep distance while fighting (Korean: 견제, 거리 유지, 치고 빠져), intent should be kite. ");
        sb.Append("If command asks to rejoin allies or move back into formation (Korean: 합류, 대열, 재집결, 모여), intent should be regroup. ");
        sb.Append("Target is optional, but when provided it must be an existing unitId from payload allies/enemies. ");
        sb.Append("Never output action arrays or coordinates. Intent-only output required. ");
        sb.Append("Think and speak as the acting allied gladiator.");
        return sb.ToString();
    }

    private string BuildUserPayloadJson(BattleRuntimeUnit actorUnit, string sanitizedRawText)
    {
        BattleLlmPromptDto promptDto = new BattleLlmPromptDto
        {
            system_prompt = new BattleLlmSystemPromptDto
            {
                personality = BuildPersonalityDescription(actorUnit),
                tools = BuildToolDtos()
            },
            user_input = new BattleLlmUserInputDto
            {
                area_situation = new BattleLlmAreaSituationDto
                {
                    arena = BuildArenaDto(),
                    allies = BuildTeamDtos(_allyUnits),
                    enemies = BuildTeamDtos(_enemyUnits)
                },
                command = sanitizedRawText
            },
            output = new BattleLlmOutputTemplateDto
            {
                thinking = string.Empty,
                dialog = string.Empty,
                intent = string.Empty,
                target = string.Empty,
                skill_usage_policy = string.Empty,
                positioning = string.Empty
            }
        };
        return JsonUtility.ToJson(promptDto, true);
    }

    private BattleLlmToolDto[] BuildToolDtos()
    {
        return new[]
        {
            new BattleLlmToolDto { type="assassinate", description="단일 적 집중 제거", parameters = new BattleLlmToolParametersDto { target="enemy unitId (optional)" } },
            new BattleLlmToolDto { type="support", description="아군 보호/지원", parameters = new BattleLlmToolParametersDto { target="ally unitId (optional)" } },
            new BattleLlmToolDto { type="vanguard", description="전방 돌파", parameters = new BattleLlmToolParametersDto { target="enemy unitId (optional)" } },
            new BattleLlmToolDto { type="hold_line", description="현 위치 사수", parameters = new BattleLlmToolParametersDto { target="(optional)" } },
            new BattleLlmToolDto { type="retreat", description="교전을 이탈하고 후퇴", parameters = new BattleLlmToolParametersDto { target="(optional)" } },
            // KITE는 적과 교전을 유지하되 최대 사거리 근처를 유지하는 의도다.
            new BattleLlmToolDto { type="kite", description="사거리를 유지하며 견제", parameters = new BattleLlmToolParametersDto { target="enemy unitId (optional)" } },
            // REGROUP은 아군과 다시 뭉쳐 전열을 정비하는 의도다.
            new BattleLlmToolDto { type="regroup", description="아군 대열로 합류", parameters = new BattleLlmToolParametersDto { target="(optional)" } }
        };
    }

    private BattleLlmArenaDto BuildArenaDto()
    {
        if (_battlefieldCollider == null)
            return new BattleLlmArenaDto { shape = "box", center = new BattleLlmVector2Dto { x = 0f, y = 0f }, size = new BattleLlmArenaSizeDto { width = 0f, height = 0f } };

        Bounds bounds = _battlefieldCollider.bounds;
        return new BattleLlmArenaDto
        {
            shape = "box",
            center = new BattleLlmVector2Dto { x = bounds.center.x, y = bounds.center.z },
            size = new BattleLlmArenaSizeDto { width = bounds.size.x, height = bounds.size.z }
        };
    }

    private BattleLlmUnitStateDto[] BuildTeamDtos(BattleRuntimeUnit[] sourceUnits)
    {
        List<BattleLlmUnitStateDto> result = new List<BattleLlmUnitStateDto>(sourceUnits.Length);
        for (int i = 0; i < sourceUnits.Length; i++)
        {
            BattleRuntimeUnit unit = sourceUnits[i];
            if (unit == null)
                continue;
            result.Add(new BattleLlmUnitStateDto
            {
                unitId = BuildUnitId(unit),
                position = new BattleLlmVector2Dto { x = unit.Position.x, y = unit.Position.z },
                stats = new BattleLlmStatsDto { hp = unit.CurrentHealth, atk = unit.Attack, range = unit.AttackRange }
            });
        }
        return result.ToArray();
    }

    private static string BuildPersonalityDescription(BattleRuntimeUnit actorUnit)
    {
        if (actorUnit?.Snapshot?.Personality == null)
            return string.Empty;
        string description = actorUnit.Snapshot.Personality.description;
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    }

    private static string BuildUnitId(BattleRuntimeUnit unit)
    {
        if (unit == null)
            return "UNKNOWN";
        if (!unit.IsEnemy)
            return $"A_{Mathf.Clamp(unit.UnitNumber, 1, 99):00}";
        return $"E_{Mathf.Clamp(unit.UnitNumber - 6, 1, 99):00}";
    }

    private static int CountUnits(BattleRuntimeUnit[] units)
    {
        int count = 0;
        for (int i = 0; i < units.Length; i++)
            if (units[i] != null)
                count++;
        return count;
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
            return $"<color=#AED581>Ally {allyNumber}:</color> Empty";
        return $"<color=#AED581>Ally {allyNumber}:</color> {BuildUnitIdentityText(unit)}";
    }

    private static string BuildUnitIdentityText(BattleRuntimeUnit unit)
    {
        if (unit == null)
            return "Unknown, Loyalty=0, Personality=None";
        string displayName = string.IsNullOrWhiteSpace(unit.DisplayName) ? "Unknown" : unit.DisplayName;
        int loyalty = unit.Snapshot != null ? unit.Snapshot.Loyalty : 0;
        string personalityName = unit.Snapshot?.Personality != null ? unit.Snapshot.Personality.name : "None";
        return $"{displayName}, Loyalty={loyalty}, Personality={personalityName}";
    }

    private static string SanitizeRawText(string rawOrderText)
    {
        if (string.IsNullOrEmpty(rawOrderText))
            return string.Empty;
        return rawOrderText.Replace("\r", " ").Replace("\n", " ");
    }
}
