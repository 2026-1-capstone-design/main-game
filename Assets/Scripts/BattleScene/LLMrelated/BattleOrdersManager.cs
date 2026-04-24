using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleOrdersManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField]
    private bool verboseLog = true;

    [Header("LLM Proxy")]
    [SerializeField]
    private bool sendOrdersToLlm = true;

    [SerializeField]
    private string llmProxyUrl = "";

    [SerializeField]
    private string appSharedToken = "";

    [SerializeField]
    private BattleLlmBackend selectedLlmBackend = BattleLlmBackend.TogetherGemma3nE4B;

    [SerializeField]
    private int requestTimeoutSeconds = 30;

    private readonly BattleRuntimeUnit[] _allyUnits = new BattleRuntimeUnit[6];
    private readonly BattleRuntimeUnit[] _enemyUnits = new BattleRuntimeUnit[6];
    private IBattleRosterProjection _rosterProjection;

    private SphereCollider _battlefieldCollider;
    private bool _initialized;
    private int _requestSequence;

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

        StringBuilder sb = new StringBuilder(768);

        sb.AppendLine("<color=#4FC3F7><b>[GLOBAL]</b></color>");
        sb.AppendLine("<color=#B3E5FC>Global order received.</color>");

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            sb.AppendLine(BuildGlobalAllyLine(i + 1, _allyUnits[i]));
        }

        sb.Append("<color=#FFCC80>Raw order:</color> \"");
        sb.Append(SanitizeRawText(rawOrderText));
        sb.AppendLine("\"");

        if (
            !TryResolveActorFromGlobalOrder(
                rawOrderText,
                out BattleRuntimeUnit resolvedActor,
                out int matchedCount,
                out string matchedSummary
            )
        )
        {
            sb.Append("<color=#EF9A9A>LLM skipped.</color> Matched ally count = ");
            sb.Append(matchedCount);
            sb.Append(". ");
            sb.Append(matchedSummary);

            Debug.Log(sb.ToString(), this);
            return;
        }

        sb.Append("<color=#81C784>Resolved actor:</color> ");
        sb.Append(BuildUnitIdentityText(resolvedActor));
        sb.Append(" / UnitId=");
        sb.Append(BuildUnitId(resolvedActor));

        Debug.Log(sb.ToString(), this);

        TrySendOrderToLlm("GLOBAL", resolvedActor, rawOrderText);
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

        if (_rosterProjection != null && !_rosterProjection.IsPlayerUnit(targetAlly))
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

    private void TrySendOrderToLlm(string orderType, BattleRuntimeUnit actorUnit, string rawOrderText)
    {
        if (!sendOrdersToLlm)
        {
            return;
        }

        if (!_initialized)
        {
            Debug.LogError("[BattleOrdersManager] LLM send blocked. Manager is not initialized.", this);
            return;
        }

        if (actorUnit == null)
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. Actor unit is null.", this);
            return;
        }

        if (_rosterProjection != null && !_rosterProjection.IsPlayerUnit(actorUnit))
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

        string systemInstruction = BuildSystemInstruction(actorUnit);
        string userPayloadJson = BuildUserPayloadJson(actorUnit, sanitizedRawText);

        if (string.IsNullOrWhiteSpace(systemInstruction))
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. System instruction is empty.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(userPayloadJson))
        {
            Debug.LogWarning("[BattleOrdersManager] LLM send skipped. User payload JSON is empty.", this);
            return;
        }

        StartCoroutine(
            SendOrderToLlmCoroutine(orderType, actorUnit, sanitizedRawText, systemInstruction, userPayloadJson)
        );
    }

    private IEnumerator SendOrderToLlmCoroutine(
        string orderType,
        BattleRuntimeUnit actorUnit,
        string sanitizedRawText,
        string systemInstruction,
        string userPayloadJson
    )
    {
        int requestId = ++_requestSequence;

        if (verboseLog)
        {
            Debug.Log(
                $"[BattleOrdersManager] Sending structured order to LLM.\n"
                    + $"RequestId={requestId}\n"
                    + $"Type={orderType}\n"
                    + $"Actor={BuildUnitIdentityText(actorUnit)}\n"
                    + $"ActorUnitId={BuildUnitId(actorUnit)}\n"
                    + $"Raw=\"{sanitizedRawText}\"\n"
                    + $"SystemInstruction=\n{systemInstruction}\n"
                    + $"UserPayloadJson=\n{userPayloadJson}",
                this
            );
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
            onError: error => errorText = error
        );

        if (!string.IsNullOrEmpty(errorText))
        {
            Debug.LogError(
                $"[BattleOrdersManager] LLM request failed. RequestId={requestId}, Type={orderType}, ActorUnitId={BuildUnitId(actorUnit)}, Error={errorText}",
                this
            );
            yield break;
        }

        Debug.Log(
            $"<color=#FFD54F><b>[LLM RESPONSE RAW]</b></color>\n"
                + $"<color=#FFF59D>RequestId:</color> {requestId}\n"
                + $"<color=#FFF59D>Type:</color> {orderType}\n"
                + $"<color=#FFF59D>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n"
                + $"<color=#FFF59D>Backend:</color> {responseBackendId}\n"
                + $"<color=#FFF59D>Provider:</color> {responseProvider}\n"
                + $"<color=#FFF59D>Model:</color> {responseModel}\n"
                + $"<color=#FFF59D>Text:</color> {responseText}",
            this
        );

        if (!TryParseLlmResponse(responseText, out BattleLlmResponseDto parsedResponse, out string parseError))
        {
            Debug.LogError(
                $"<color=#FF8A80><b>[LLM PARSE FAILED]</b></color>\n"
                    + $"<color=#FFCDD2>RequestId:</color> {requestId}\n"
                    + $"<color=#FFCDD2>Type:</color> {orderType}\n"
                    + $"<color=#FFCDD2>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n"
                    + $"<color=#FFCDD2>Reason:</color> {parseError}\n"
                    + $"<color=#FFCDD2>RawText:</color> {responseText}",
                this
            );
            yield break;
        }

        List<string> validationErrors = ValidateLlmResponse(actorUnit, parsedResponse);

        if (validationErrors.Count > 0)
        {
            Debug.LogWarning(
                $"<color=#FFB74D><b>[LLM VALIDATION FAILED]</b></color>\n"
                    + $"<color=#FFE0B2>RequestId:</color> {requestId}\n"
                    + $"<color=#FFE0B2>Type:</color> {orderType}\n"
                    + $"<color=#FFE0B2>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n"
                    + $"<color=#FFE0B2>Errors:</color>\n{BuildValidationErrorSummary(validationErrors)}\n"
                    + $"<color=#FFE0B2>ParsedResponse:</color>\n{BuildParsedResponseSummary(parsedResponse)}\n"
                    + $"<color=#FFE0B2>Fallback:</color> Ignored. No gameplay action is applied.",
                this
            );
            yield break;
        }

        Debug.Log(
            $"<color=#81C784><b>[LLM VALIDATED SUCCESS]</b></color>\n"
                + $"<color=#C8E6C9>RequestId:</color> {requestId}\n"
                + $"<color=#C8E6C9>Type:</color> {orderType}\n"
                + $"<color=#C8E6C9>ActorUnitId:</color> {BuildUnitId(actorUnit)}\n"
                + $"<color=#C8E6C9>ParsedResponse:</color>\n{BuildParsedResponseSummary(parsedResponse)}\n"
                + $"<color=#C8E6C9>Execution:</color> Not applied in this step. Log only.",
            this
        );
    }

    private bool TryResolveActorFromGlobalOrder(
        string rawOrderText,
        out BattleRuntimeUnit resolvedActor,
        out int matchedCount,
        out string matchedSummary
    )
    {
        resolvedActor = null;
        matchedCount = 0;

        List<BattleRuntimeUnit> matchedUnits = new List<BattleRuntimeUnit>(2);
        string searchText = rawOrderText ?? string.Empty;

        for (int i = 0; i < _allyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _allyUnits[i];
            if (unit == null)
            {
                continue;
            }

            string displayName = unit.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            if (searchText.IndexOf(displayName, StringComparison.Ordinal) >= 0)
            {
                matchedUnits.Add(unit);
            }
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
                {
                    sb.Append(", ");
                }

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
        out string parseError
    )
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
        {
            outputJson = jsonText;
        }

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

        if (!TryExtractArrayProperty(outputJson, "action", out string actionArrayJson))
        {
            parseError = "Missing or invalid output.action array.";
            return false;
        }

        if (!TrySplitTopLevelObjectArray(actionArrayJson, out List<string> actionObjectJsons, out string splitError))
        {
            parseError = $"Failed to parse output.action array. {splitError}";
            return false;
        }

        BattleLlmResponseActionDto[] actions = new BattleLlmResponseActionDto[actionObjectJsons.Count];

        for (int i = 0; i < actionObjectJsons.Count; i++)
        {
            if (
                !TryParseSingleAction(
                    actionObjectJsons[i],
                    out BattleLlmResponseActionDto parsedAction,
                    out string actionError
                )
            )
            {
                parseError = $"Action[{i}] parse failed. {actionError}";
                return false;
            }

            actions[i] = parsedAction;
        }

        outputDto.action = actions;

        parsedResponse = new BattleLlmResponseDto { output = outputDto };

        return true;
    }

    private bool TryParseSingleAction(
        string actionJson,
        out BattleLlmResponseActionDto parsedAction,
        out string actionError
    )
    {
        parsedAction = new BattleLlmResponseActionDto();
        actionError = null;

        if (!TryExtractJsonStringProperty(actionJson, "type", out parsedAction.type))
        {
            actionError = "Missing action.type string.";
            return false;
        }

        TryExtractJsonStringProperty(actionJson, "subtype", out parsedAction.subtype);
        TryExtractJsonStringProperty(actionJson, "target", out parsedAction.targetUnitId);
        TryExtractJsonStringProperty(actionJson, "from", out parsedAction.relativeFromUnitId);

        if (TryExtractFloatPairProperty(actionJson, "target", out float targetX, out float targetY))
        {
            parsedAction.absoluteTarget = new BattleLlmVector2Dto { x = targetX, y = targetY };
        }

        if (TryExtractFloatPairProperty(actionJson, "offset", out float offsetX, out float offsetY))
        {
            parsedAction.relativeOffset = new BattleLlmVector2Dto { x = offsetX, y = offsetY };
        }

        return true;
    }

    private List<string> ValidateLlmResponse(BattleRuntimeUnit actorUnit, BattleLlmResponseDto parsedResponse)
    {
        List<string> errors = new List<string>();

        if (actorUnit == null)
        {
            errors.Add("Actor unit is null.");
            return errors;
        }

        if (_rosterProjection != null && !_rosterProjection.IsPlayerUnit(actorUnit))
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
            {
                continue;
            }

            string unitId = BuildUnitId(unit);
            allyIds.Add(unitId);
            allIds.Add(unitId);
        }

        for (int i = 0; i < _enemyUnits.Length; i++)
        {
            BattleRuntimeUnit unit = _enemyUnits[i];
            if (unit == null)
            {
                continue;
            }

            string unitId = BuildUnitId(unit);
            enemyIds.Add(unitId);
            allIds.Add(unitId);
        }

        if (!allyIds.Contains(actorUnitId))
        {
            errors.Add($"Actor unit id '{actorUnitId}' is not present in ally roster.");
        }

        if (parsedResponse == null)
        {
            errors.Add("Parsed response is null.");
            return errors;
        }

        if (parsedResponse.output == null)
        {
            errors.Add("Parsed response.output is null.");
            return errors;
        }

        BattleLlmResponseActionDto[] actions = parsedResponse.output.action ?? new BattleLlmResponseActionDto[0];

        if (actions.Length > 4)
        {
            errors.Add($"Action count exceeds limit. Count={actions.Length}, Limit=4.");
        }

        for (int i = 0; i < actions.Length; i++)
        {
            BattleLlmResponseActionDto action = actions[i];
            if (action == null)
            {
                errors.Add($"Action[{i}] is null.");
                continue;
            }

            string type = NormalizeToken(action.type);
            string subtype = NormalizeToken(action.subtype);

            if (type != "move" && type != "attack")
            {
                errors.Add($"Action[{i}] has unsupported type '{action.type}'. Allowed: move, attack.");
                continue;
            }

            if (type == "attack")
            {
                if (string.IsNullOrWhiteSpace(action.targetUnitId))
                {
                    errors.Add($"Action[{i}] attack is missing target unit id.");
                    continue;
                }

                if (allyIds.Contains(action.targetUnitId))
                {
                    errors.Add(
                        $"Action[{i}] attack target '{action.targetUnitId}' is an ally. Attack against ally is forbidden."
                    );
                    continue;
                }

                if (!enemyIds.Contains(action.targetUnitId))
                {
                    errors.Add($"Action[{i}] attack target '{action.targetUnitId}' does not exist in enemy roster.");
                    continue;
                }

                continue;
            }

            if (type == "move")
            {
                if (subtype != "absolute" && subtype != "relative")
                {
                    errors.Add($"Action[{i}] move subtype '{action.subtype}' is invalid. Allowed: absolute, relative.");
                    continue;
                }

                if (subtype == "absolute")
                {
                    if (action.absoluteTarget == null)
                    {
                        errors.Add($"Action[{i}] move.absolute is missing target coordinate pair.");
                    }

                    continue;
                }

                if (subtype == "relative")
                {
                    if (string.IsNullOrWhiteSpace(action.relativeFromUnitId))
                    {
                        errors.Add($"Action[{i}] move.relative is missing from unit id.");
                    }
                    else if (!allIds.Contains(action.relativeFromUnitId))
                    {
                        errors.Add(
                            $"Action[{i}] move.relative from unit id '{action.relativeFromUnitId}' does not exist."
                        );
                    }

                    if (action.relativeOffset == null)
                    {
                        errors.Add($"Action[{i}] move.relative is missing offset coordinate pair.");
                    }
                }
            }
        }

        return errors;
    }

    private static string BuildParsedResponseSummary(BattleLlmResponseDto parsedResponse)
    {
        if (parsedResponse == null || parsedResponse.output == null)
        {
            return "Parsed response is null.";
        }

        StringBuilder sb = new StringBuilder(512);

        sb.Append("<b>Thinking:</b> ");
        sb.AppendLine(
            string.IsNullOrWhiteSpace(parsedResponse.output.thinking) ? "(empty)" : parsedResponse.output.thinking
        );

        sb.Append("<b>Dialog:</b> ");
        sb.AppendLine(
            string.IsNullOrWhiteSpace(parsedResponse.output.dialog) ? "(empty)" : parsedResponse.output.dialog
        );

        BattleLlmResponseActionDto[] actions = parsedResponse.output.action ?? new BattleLlmResponseActionDto[0];
        sb.Append("<b>ActionCount:</b> ");
        sb.AppendLine(actions.Length.ToString());

        for (int i = 0; i < actions.Length; i++)
        {
            sb.Append("- ");
            sb.AppendLine(BuildActionSummary(actions[i], i));
        }

        return sb.ToString();
    }

    private static string BuildActionSummary(BattleLlmResponseActionDto action, int index)
    {
        if (action == null)
        {
            return $"Action[{index}] = null";
        }

        string type = NormalizeToken(action.type);
        string subtype = NormalizeToken(action.subtype);

        if (type == "attack")
        {
            return $"Action[{index}] type=attack, target={action.targetUnitId}";
        }

        if (type == "move" && subtype == "absolute")
        {
            if (action.absoluteTarget == null)
            {
                return $"Action[{index}] type=move, subtype=absolute, target=(missing)";
            }

            return $"Action[{index}] type=move, subtype=absolute, target=[{action.absoluteTarget.x}, {action.absoluteTarget.y}]";
        }

        if (type == "move" && subtype == "relative")
        {
            if (action.relativeOffset == null)
            {
                return $"Action[{index}] type=move, subtype=relative, from={action.relativeFromUnitId}, offset=(missing)";
            }

            return $"Action[{index}] type=move, subtype=relative, from={action.relativeFromUnitId}, offset=[{action.relativeOffset.x}, {action.relativeOffset.y}]";
        }

        return $"Action[{index}] type={action.type}, subtype={action.subtype}";
    }

    private static string BuildValidationErrorSummary(List<string> errors)
    {
        if (errors == null || errors.Count == 0)
        {
            return "(none)";
        }

        StringBuilder sb = new StringBuilder(256);

        for (int i = 0; i < errors.Count; i++)
        {
            sb.Append("- ");
            sb.AppendLine(errors[i]);
        }

        return sb.ToString();
    }

    private static string NormalizeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static string ExtractLikelyJsonObjectText(string rawResponseText)
    {
        if (string.IsNullOrWhiteSpace(rawResponseText))
        {
            return null;
        }

        string trimmed = rawResponseText.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = trimmed.IndexOf('\n');
            if (firstLineBreak >= 0)
            {
                trimmed = trimmed.Substring(firstLineBreak + 1).Trim();
            }

            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed.Substring(0, lastFence).Trim();
            }
        }

        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return null;
        }

        return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    private static bool TryExtractObjectProperty(string json, string propertyName, out string objectJson)
    {
        objectJson = null;

        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
        {
            return false;
        }

        if (startIndex < 0 || endIndex < startIndex || json[startIndex] != '{')
        {
            return false;
        }

        objectJson = json.Substring(startIndex, endIndex - startIndex + 1);
        return true;
    }

    private static bool TryExtractArrayProperty(string json, string propertyName, out string arrayJson)
    {
        arrayJson = null;

        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
        {
            return false;
        }

        if (startIndex < 0 || endIndex < startIndex || json[startIndex] != '[')
        {
            return false;
        }

        arrayJson = json.Substring(startIndex, endIndex - startIndex + 1);
        return true;
    }

    private static bool TryExtractJsonStringProperty(string json, string propertyName, out string value)
    {
        value = null;

        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
        {
            return false;
        }

        if (startIndex < 0 || endIndex <= startIndex || json[startIndex] != '"' || json[endIndex] != '"')
        {
            return false;
        }

        string encoded = json.Substring(startIndex + 1, endIndex - startIndex - 1);
        value = UnescapeJsonString(encoded);
        return true;
    }

    private static bool TryExtractFloatPairProperty(string json, string propertyName, out float x, out float y)
    {
        x = 0f;
        y = 0f;

        if (!TryGetPropertyValueSpan(json, propertyName, out int startIndex, out int endIndex))
        {
            return false;
        }

        if (startIndex < 0 || endIndex <= startIndex || json[startIndex] != '[' || json[endIndex] != ']')
        {
            return false;
        }

        string arrayJson = json.Substring(startIndex, endIndex - startIndex + 1);
        Match match = Regex.Match(
            arrayJson,
            @"^\[\s*(-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)\s*,\s*(-?\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?)\s*\]$"
        );

        if (!match.Success)
        {
            return false;
        }

        if (!float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }

        if (!float.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }

        return true;
    }

    private static bool TrySplitTopLevelObjectArray(string arrayJson, out List<string> objects, out string error)
    {
        objects = new List<string>();
        error = null;

        if (string.IsNullOrWhiteSpace(arrayJson))
        {
            error = "Array JSON is empty.";
            return false;
        }

        if (arrayJson[0] != '[' || arrayJson[arrayJson.Length - 1] != ']')
        {
            error = "Array JSON does not start/end with '[' and ']'.";
            return false;
        }

        int index = 1;

        while (index < arrayJson.Length - 1)
        {
            index = SkipWhitespace(arrayJson, index);

            if (index >= arrayJson.Length - 1)
            {
                break;
            }

            if (arrayJson[index] == ',')
            {
                index++;
                continue;
            }

            if (arrayJson[index] != '{')
            {
                error = $"Unexpected token '{arrayJson[index]}' inside action array.";
                return false;
            }

            int endIndex = FindMatchingBracket(arrayJson, index, '{', '}');
            if (endIndex < 0)
            {
                error = "Failed to find matching '}' in action array.";
                return false;
            }

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
        {
            return false;
        }

        Match match = Regex.Match(json, "\"" + Regex.Escape(propertyName) + "\"\\s*:");
        if (!match.Success)
        {
            return false;
        }

        int valueStart = SkipWhitespace(json, match.Index + match.Length);
        if (valueStart < 0 || valueStart >= json.Length)
        {
            return false;
        }

        char firstChar = json[valueStart];

        if (firstChar == '{')
        {
            int objectEnd = FindMatchingBracket(json, valueStart, '{', '}');
            if (objectEnd < 0)
            {
                return false;
            }

            startIndex = valueStart;
            endIndex = objectEnd;
            return true;
        }

        if (firstChar == '[')
        {
            int arrayEnd = FindMatchingBracket(json, valueStart, '[', ']');
            if (arrayEnd < 0)
            {
                return false;
            }

            startIndex = valueStart;
            endIndex = arrayEnd;
            return true;
        }

        if (firstChar == '"')
        {
            int stringEnd = FindStringEnd(json, valueStart);
            if (stringEnd < 0)
            {
                return false;
            }

            startIndex = valueStart;
            endIndex = stringEnd;
            return true;
        }

        int primitiveEnd = valueStart;
        while (
            primitiveEnd < json.Length
            && json[primitiveEnd] != ','
            && json[primitiveEnd] != '}'
            && json[primitiveEnd] != ']'
        )
        {
            primitiveEnd++;
        }

        primitiveEnd--;

        if (primitiveEnd < valueStart)
        {
            return false;
        }

        startIndex = valueStart;
        endIndex = primitiveEnd;
        return true;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

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
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == openChar)
            {
                depth++;
                continue;
            }

            if (c == closeChar)
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
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
            {
                i++;
                continue;
            }

            if (c == '"')
            {
                return i;
            }
        }

        return -1;
    }

    private static string UnescapeJsonString(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return string.Empty;
        }

        string normalized = encoded.Replace("\\/", "/");
        return Regex.Unescape(normalized);
    }

    private string BuildSystemInstruction(BattleRuntimeUnit actorUnit)
    {
        string actorUnitId = BuildUnitId(actorUnit);

        StringBuilder sb = new StringBuilder(1024);
        sb.Append(
            "You are generating one battle-order response for a single allied gladiator in a Unity battle prototype. "
        );
        sb.Append("The acting ally unit id is ");
        sb.Append(actorUnitId);
        sb.Append(". ");
        sb.Append("The payload JSON already contains system_prompt, user_input, and an empty output template. ");
        sb.Append("Use the actor personality from payload.system_prompt.personality. ");
        sb.Append("Return only one valid JSON object with exactly this top-level shape: ");
        sb.Append("{\"output\":{\"thinking\":\"string\",\"dialog\":\"string\",\"action\":[]}}. ");
        sb.Append("Do not add markdown, code fences, or extra commentary. ");
        sb.Append("The arena is a rectangular box arena. ");
        sb.Append("Coordinates use x and y fields mapped from Unity world x and z. ");
        sb.Append("The action array may contain 0 to 4 items. ");
        sb.Append("Allowed action types are move and attack only. ");
        sb.Append("Allowed move subtypes are absolute and relative only. ");
        sb.Append("For attack.target, use an existing enemy unitId from the payload only. ");
        sb.Append("Never target an ally with attack. ");
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
                tools = BuildToolDtos(),
            },
            user_input = new BattleLlmUserInputDto
            {
                area_situation = new BattleLlmAreaSituationDto
                {
                    arena = BuildArenaDto(),
                    allies = BuildTeamDtos(_allyUnits),
                    enemies = BuildTeamDtos(_enemyUnits),
                },
                command = sanitizedRawText,
            },
            output = new BattleLlmOutputTemplateDto
            {
                thinking = string.Empty,
                dialog = string.Empty,
                action = new BattleLlmOutputActionPlaceholderDto[0],
            },
        };

        return JsonUtility.ToJson(promptDto, true);
    }

    private BattleLlmToolDto[] BuildToolDtos()
    {
        return new[]
        {
            new BattleLlmToolDto
            {
                type = "move",
                subtype = "absolute",
                description = "절대 좌표로 이동",
                parameters = new BattleLlmToolParametersDto
                {
                    target = "[num, num] — 이동할 절대 좌표",
                    from = null,
                    offset = null,
                },
            },
            new BattleLlmToolDto
            {
                type = "move",
                subtype = "relative",
                description = "특정 유닛을 기준으로 상대 좌표로 이동",
                parameters = new BattleLlmToolParametersDto
                {
                    target = null,
                    from = "unit — 기준 유닛 ID",
                    offset = "[num, num] — 기준 유닛으로부터의 상대 좌표",
                },
            },
            new BattleLlmToolDto
            {
                type = "attack",
                subtype = null,
                description = "대상 유닛을 공격",
                parameters = new BattleLlmToolParametersDto
                {
                    target = "unit — 공격 대상 유닛 ID",
                    from = null,
                    offset = null,
                },
            },
        };
    }

    private BattleLlmArenaDto BuildArenaDto()
    {
        if (_battlefieldCollider == null)
        {
            return new BattleLlmArenaDto
            {
                shape = "box",
                center = new BattleLlmVector2Dto { x = 0f, y = 0f },
                size = new BattleLlmArenaSizeDto { width = 0f, height = 0f },
            };
        }

        Bounds bounds = _battlefieldCollider.bounds;

        return new BattleLlmArenaDto
        {
            shape = "box",
            center = new BattleLlmVector2Dto { x = bounds.center.x, y = bounds.center.z },
            size = new BattleLlmArenaSizeDto { width = bounds.size.x, height = bounds.size.z },
        };
    }

    private BattleLlmUnitStateDto[] BuildTeamDtos(BattleRuntimeUnit[] sourceUnits)
    {
        List<BattleLlmUnitStateDto> result = new List<BattleLlmUnitStateDto>(sourceUnits.Length);

        for (int i = 0; i < sourceUnits.Length; i++)
        {
            BattleRuntimeUnit unit = sourceUnits[i];
            if (unit == null)
            {
                continue;
            }

            result.Add(
                new BattleLlmUnitStateDto
                {
                    unitId = BuildUnitId(unit),
                    position = new BattleLlmVector2Dto { x = unit.Position.x, y = unit.Position.z },
                    stats = new BattleLlmStatsDto
                    {
                        hp = unit.CurrentHealth,
                        atk = unit.Attack,
                        range = unit.AttackRange,
                    },
                }
            );
        }

        return result.ToArray();
    }

    private static string BuildPersonalityDescription(BattleRuntimeUnit actorUnit)
    {
        if (actorUnit == null || actorUnit.Snapshot == null || actorUnit.Snapshot.Personality == null)
        {
            return string.Empty;
        }

        string description = actorUnit.Snapshot.Personality.description;
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
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
}
