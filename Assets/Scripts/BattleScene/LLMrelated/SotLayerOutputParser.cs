// 서버 SLM raw text에서 parser/dialog JSON을 추출하고 DTO로 변환한다.
// Parser output은 thinking/dialog/action 구조와 runtime contract를 검증한다.
// Dialog server output은 lines[] 스키마를 검증한 뒤 Unity 내부 dialog[] DTO로 변환한다.
// 실패 시 caller는 아무 행동도 실행하지 않고 full prompt를 로그로 출력한다.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SotLayerOutputParser
{
    public static bool TryParseParserOutput(
        string rawResponse,
        SotParserRequestDto parserRequest,
        out SotParserOutputDto parserOutput,
        out string error
    )
    {
        parserOutput = null;
        error = string.Empty;

        if (!TryParseJsonObject(rawResponse, out JObject root, out error))
            return false;

        if (!TryValidateParserJsonShape(root, out error))
            return false;

        try
        {
            parserOutput = root.ToObject<SotParserOutputDto>();
        }
        catch (Exception exception)
        {
            error = "Failed to deserialize parser output: " + exception.Message;
            return false;
        }

        if (!SotParserOutputValidator.TryValidate(parserOutput, parserRequest, out error))
            return false;

        return true;
    }

    public static bool TryParseDialogOutput(
        string rawResponse,
        SotDialogLayerRequestDto dialogRequest,
        out SotDialogLayerResponseDto dialogResponse,
        out string error
    )
    {
        dialogResponse = null;
        error = string.Empty;

        if (!TryParseJsonObject(rawResponse, out JObject root, out error))
            return false;

        HashSet<string> expectedActorIds = BuildExpectedActorIds(dialogRequest);
        if (expectedActorIds.Count == 0)
        {
            error = "Dialog request has no actors.";
            return false;
        }

        if (!TryValidateDialogJsonShape(root, expectedActorIds, out error))
            return false;

        JArray linesArray = (JArray)root["lines"];
        List<SotDialogLineDto> lines = new List<SotDialogLineDto>(linesArray.Count);

        for (int i = 0; i < linesArray.Count; i++)
        {
            JObject lineObject = (JObject)linesArray[i];
            lines.Add(
                new SotDialogLineDto
                {
                    unitId = lineObject.Value<string>("unitId"),
                    text = lineObject.Value<string>("text"),
                }
            );
        }

        dialogResponse = new SotDialogLayerResponseDto { dialog = lines.ToArray() };
        return true;
    }

    private static bool TryParseJsonObject(string rawResponse, out JObject root, out string error)
    {
        root = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            error = "Response text is empty.";
            return false;
        }

        string jsonText = ExtractLikelyJsonObjectText(StripCodeFence(rawResponse));
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            error = "Could not extract JSON object from response.";
            return false;
        }

        try
        {
            JToken token = JToken.Parse(jsonText);
            root = token as JObject;
            if (root == null)
            {
                error = "Output root must be a JSON object.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = "JSON parse failed: " + exception.Message;
            return false;
        }
    }

    private static bool TryValidateParserJsonShape(JObject root, out string error)
    {
        error = string.Empty;

        if (!HasExactKeys(root, "thinking", "dialog", "action"))
        {
            error = "Parser output root must contain exactly thinking, dialog, action.";
            return false;
        }

        if (root["thinking"].Type != JTokenType.String)
        {
            error = "Parser output thinking must be string.";
            return false;
        }

        if (root["dialog"].Type != JTokenType.Array)
        {
            error = "Parser output dialog must be array.";
            return false;
        }

        if (root["action"].Type != JTokenType.Array)
        {
            error = "Parser output action must be array.";
            return false;
        }

        JArray dialog = (JArray)root["dialog"];
        for (int i = 0; i < dialog.Count; i++)
        {
            JObject item = dialog[i] as JObject;
            if (item == null)
            {
                error = $"Parser output dialog[{i}] must be object.";
                return false;
            }

            if (!HasExactKeys(item, "unitId", "text"))
            {
                error = $"Parser output dialog[{i}] must contain exactly unitId, text.";
                return false;
            }

            if (item["unitId"].Type != JTokenType.String || item["text"].Type != JTokenType.String)
            {
                error = $"Parser output dialog[{i}] unitId/text must be string.";
                return false;
            }
        }

        JArray actions = (JArray)root["action"];
        for (int i = 0; i < actions.Count; i++)
        {
            JObject item = actions[i] as JObject;
            if (item == null)
            {
                error = $"Parser output action[{i}] must be object.";
                return false;
            }

            if (!HasExactKeys(item, "unitId", "sequence"))
            {
                error = $"Parser output action[{i}] must contain exactly unitId, sequence.";
                return false;
            }

            if (item["unitId"].Type != JTokenType.String || item["sequence"].Type != JTokenType.Array)
            {
                error = $"Parser output action[{i}] unitId/sequence type is invalid.";
                return false;
            }

            JArray sequence = (JArray)item["sequence"];
            for (int j = 0; j < sequence.Count; j++)
            {
                JObject sequenceItem = sequence[j] as JObject;
                if (sequenceItem == null)
                {
                    error = $"Parser output action[{i}].sequence[{j}] must be object.";
                    return false;
                }

                if (!TryValidateParserActionShape(sequenceItem, i, j, out error))
                    return false;
            }
        }

        return true;
    }

    private static bool TryValidateParserActionShape(JObject action, int actionIndex, int sequenceIndex, out string error)
    {
        error = string.Empty;
        JToken typeToken = action["type"];

        if (typeToken == null || typeToken.Type != JTokenType.String)
        {
            error = $"Parser output action[{actionIndex}].sequence[{sequenceIndex}] type must be string.";
            return false;
        }

        string type = typeToken.Value<string>();

        switch (type)
        {
            case "move":
                if (!HasExactKeys(action, "type", "subtype", "movementType", "to"))
                {
                    error = $"move action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                    return false;
                }

                return AreStringFields(action, "subtype", "movementType", "to", out error);

            case "attack":
                if (!HasExactKeys(action, "type", "target"))
                {
                    error = $"attack action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                    return false;
                }

                return AreStringFields(action, "target", out error);

            case "skill":
                if (!HasExactKeys(action, "type", "description", "target"))
                {
                    error = $"skill action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                    return false;
                }

                return AreStringFields(action, "description", "target", out error);

            case "wait":
                if (!HasExactKeys(action, "type", "durationSec"))
                {
                    error = $"wait action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                    return false;
                }

                if (!IsJsonNumber(action["durationSec"]))
                {
                    error = $"wait action[{actionIndex}].sequence[{sequenceIndex}] durationSec must be number.";
                    return false;
                }

                return true;

            case "skillControl":
                string mode = action.Value<string>("mode");
                if (mode == "defer")
                {
                    if (!HasExactKeys(action, "type", "mode", "durationSec"))
                    {
                        error = $"skillControl defer action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                        return false;
                    }

                    if (!IsJsonNumber(action["durationSec"]))
                    {
                        error = $"skillControl defer action[{actionIndex}].sequence[{sequenceIndex}] durationSec must be number.";
                        return false;
                    }

                    return AreStringFields(action, "mode", out error);
                }

                if (mode == "forbid")
                {
                    if (!HasExactKeys(action, "type", "mode"))
                    {
                        error = $"skillControl forbid action[{actionIndex}].sequence[{sequenceIndex}] keys are invalid.";
                        return false;
                    }

                    return AreStringFields(action, "mode", out error);
                }

                error = $"skillControl action[{actionIndex}].sequence[{sequenceIndex}] mode is invalid.";
                return false;

            default:
                error = $"Unsupported parser action type '{type}'.";
                return false;
        }
    }

    private static bool TryValidateDialogJsonShape(
        JObject root,
        HashSet<string> expectedActorIds,
        out string error
    )
    {
        error = string.Empty;

        if (!HasExactKeys(root, "lines"))
        {
            error = "Dialog output root must contain only lines.";
            return false;
        }

        if (root["lines"].Type != JTokenType.Array)
        {
            error = "Dialog output lines must be array.";
            return false;
        }

        JArray lines = (JArray)root["lines"];
        if (lines.Count != expectedActorIds.Count)
        {
            error = "Dialog output lines length must match input actors length.";
            return false;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < lines.Count; i++)
        {
            JObject line = lines[i] as JObject;
            if (line == null)
            {
                error = $"Dialog output lines[{i}] must be object.";
                return false;
            }

            if (!HasExactKeys(line, "unitId", "text"))
            {
                error = $"Dialog output lines[{i}] must contain exactly unitId, text.";
                return false;
            }

            string unitId = line.Value<string>("unitId");
            string text = line.Value<string>("text");

            if (string.IsNullOrWhiteSpace(unitId) || !expectedActorIds.Contains(unitId))
            {
                error = $"Dialog output lines[{i}] has unknown unitId '{unitId}'.";
                return false;
            }

            if (!seen.Add(unitId))
            {
                error = $"Dialog output contains duplicate unitId '{unitId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                error = $"Dialog output lines[{i}] text is empty.";
                return false;
            }
        }

        return true;
    }

    private static HashSet<string> BuildExpectedActorIds(SotDialogLayerRequestDto dialogRequest)
    {
        HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);

        SotDialogActorInputDto[] actors =
            dialogRequest != null && dialogRequest.actors != null
                ? dialogRequest.actors
                : Array.Empty<SotDialogActorInputDto>();

        for (int i = 0; i < actors.Length; i++)
        {
            SotDialogActorInputDto actor = actors[i];
            if (actor == null || string.IsNullOrWhiteSpace(actor.unitId))
                continue;

            result.Add(actor.unitId);
        }

        return result;
    }

    private static bool HasExactKeys(JObject obj, params string[] expectedKeys)
    {
        if (obj == null)
            return false;

        if (obj.Count != expectedKeys.Length)
            return false;

        for (int i = 0; i < expectedKeys.Length; i++)
        {
            if (obj.Property(expectedKeys[i]) == null)
                return false;
        }

        return true;
    }

    private static bool AreStringFields(JObject obj, out string error)
    {
        error = string.Empty;
        return true;
    }

    private static bool AreStringFields(JObject obj, string fieldName, out string error)
    {
        return AreStringFields(obj, new[] { fieldName }, out error);
    }

    private static bool AreStringFields(JObject obj, string fieldName1, string fieldName2, out string error)
    {
        return AreStringFields(obj, new[] { fieldName1, fieldName2 }, out error);
    }

    private static bool AreStringFields(
        JObject obj,
        string fieldName1,
        string fieldName2,
        string fieldName3,
        out string error
    )
    {
        return AreStringFields(obj, new[] { fieldName1, fieldName2, fieldName3 }, out error);
    }

    private static bool AreStringFields(JObject obj, string[] fieldNames, out string error)
    {
        error = string.Empty;

        for (int i = 0; i < fieldNames.Length; i++)
        {
            string fieldName = fieldNames[i];
            if (obj[fieldName] == null || obj[fieldName].Type != JTokenType.String)
            {
                error = $"Field '{fieldName}' must be string.";
                return false;
            }
        }

        return true;
    }

    private static bool IsJsonNumber(JToken token)
    {
        return token != null && (token.Type == JTokenType.Float || token.Type == JTokenType.Integer);
    }

    private static string StripCodeFence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string cleaned = text.Trim();

        if (!cleaned.StartsWith("```", StringComparison.Ordinal))
            return cleaned;

        int firstNewline = cleaned.IndexOf('\n');
        if (firstNewline >= 0)
            cleaned = cleaned.Substring(firstNewline + 1);

        if (cleaned.EndsWith("```", StringComparison.Ordinal))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);

        return cleaned.Trim();
    }

    private static string ExtractLikelyJsonObjectText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        int start = text.IndexOf('{');
        if (start < 0)
            return string.Empty;

        bool inString = false;
        bool escaped = false;
        int depth = 0;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                if (inString)
                    escaped = true;

                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
            {
                depth++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return text.Substring(start, i - start + 1);
            }
        }

        return string.Empty;
    }
}
