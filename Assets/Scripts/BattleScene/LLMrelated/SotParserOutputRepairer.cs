// 서버 parser raw response를 SOT parser output DTO로 관용 보정한다.
// 마지막 brace 누락, OpenAI-style envelope, enum alias, unitId 표기 흔들림을 정리한다.
// request의 commandAnalysis와 area_situation을 기준으로 actor/target 후보를 보정한다.
// 보정 후에는 기존 SotParserOutputValidator를 반드시 다시 통과시킨다.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SotParserOutputRepairer
{
    private static readonly Regex UnitIdLooseRegex = new Regex(
        @"(?<![A-Za-z0-9])([AaEe])[\s_\-]*([0-9]{1,2})(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public static bool TryRepairParserOutput(
        string rawResponse,
        SotParserRequestDto parserRequest,
        out SotParserOutputDto parserOutput,
        out string repairedJson,
        out string repairLog,
        out string error
    )
    {
        parserOutput = null;
        repairedJson = string.Empty;
        repairLog = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            error = "raw parser response is empty.";
            return false;
        }

        ParserRepairContext context = new ParserRepairContext(parserRequest);
        List<string> repairSteps = new List<string>();
        List<string> candidates = BuildCandidateTexts(rawResponse, repairSteps);

        for (int i = 0; i < candidates.Count; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            if (!TryBuildLooseJsonObject(candidate, out JObject rawRoot, out string extractionError, repairSteps))
            {
                error = extractionError;
                continue;
            }

            JObject repairedRoot = BuildParserRoot(rawRoot, context, repairSteps);
            repairedJson = repairedRoot.ToString(Formatting.None);

            try
            {
                parserOutput = repairedRoot.ToObject<SotParserOutputDto>();
            }
            catch (Exception exception)
            {
                error = "repaired parser output deserialize failed: " + exception.Message;
                parserOutput = null;
                continue;
            }

            if (!SotParserOutputValidator.TryValidate(parserOutput, parserRequest, out string validationError))
            {
                error = "repaired parser output still invalid: " + validationError;
                parserOutput = null;
                continue;
            }

            repairLog = string.Join("\n", repairSteps.ToArray());
            return true;
        }

        if (string.IsNullOrWhiteSpace(error))
            error = "parser output repair failed.";

        repairLog = string.Join("\n", repairSteps.ToArray());
        parserOutput = null;
        repairedJson = string.Empty;
        return false;
    }

    private static List<string> BuildCandidateTexts(string rawResponse, List<string> repairSteps)
    {
        List<string> candidates = new List<string>();
        string cleaned = StripCodeFence(rawResponse);
        AddCandidate(candidates, cleaned);

        if (TryParseStrictObject(cleaned, out JObject envelope))
        {
            AddEnvelopeTextCandidates(envelope, candidates, repairSteps);
        }

        return candidates;
    }

    private static void AddEnvelopeTextCandidates(JObject root, List<string> candidates, List<string> repairSteps)
    {
        if (root == null)
            return;

        TryAddStringToken(root["text"], candidates, "envelope.text", repairSteps);
        TryAddStringToken(root["response"], candidates, "envelope.response", repairSteps);
        TryAddStringToken(root["content"], candidates, "envelope.content", repairSteps);

        JObject message = root["message"] as JObject;
        if (message != null)
            TryAddStringToken(message["content"], candidates, "envelope.message.content", repairSteps);

        JArray choices = root["choices"] as JArray;
        if (choices == null || choices.Count == 0)
            return;

        for (int i = 0; i < choices.Count; i++)
        {
            JObject choice = choices[i] as JObject;
            if (choice == null)
                continue;

            TryAddStringToken(choice["text"], candidates, "envelope.choices.text", repairSteps);

            JObject choiceMessage = choice["message"] as JObject;
            if (choiceMessage != null)
                TryAddStringToken(choiceMessage["content"], candidates, "envelope.choices.message.content", repairSteps);

            JObject delta = choice["delta"] as JObject;
            if (delta != null)
                TryAddStringToken(delta["content"], candidates, "envelope.choices.delta.content", repairSteps);
        }
    }

    private static void TryAddStringToken(
        JToken token,
        List<string> candidates,
        string sourceName,
        List<string> repairSteps
    )
    {
        if (token == null || token.Type != JTokenType.String)
            return;

        string value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value))
            return;

        AddCandidate(candidates, value);
        repairSteps.Add("extracted candidate from " + sourceName + ".");
    }

    private static void AddCandidate(List<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], candidate, StringComparison.Ordinal))
                return;
        }

        candidates.Add(candidate);
    }

    private static bool TryBuildLooseJsonObject(
        string rawText,
        out JObject root,
        out string error,
        List<string> repairSteps
    )
    {
        root = null;
        error = string.Empty;

        string jsonText;
        if (!TryExtractOrCompleteJsonObject(rawText, out jsonText, repairSteps))
        {
            error = "could not extract or complete JSON object.";
            return false;
        }

        if (TryParseStrictObject(jsonText, out root))
            return true;

        string withoutTrailingCommas = RemoveTrailingCommasOutsideStrings(jsonText);
        if (!string.Equals(withoutTrailingCommas, jsonText, StringComparison.Ordinal))
        {
            repairSteps.Add("removed trailing commas outside strings.");
            if (TryParseStrictObject(withoutTrailingCommas, out root))
                return true;
        }

        string quotedKeys = QuoteKnownUnquotedKeys(withoutTrailingCommas);
        if (!string.Equals(quotedKeys, withoutTrailingCommas, StringComparison.Ordinal))
        {
            repairSteps.Add("quoted known unquoted keys.");
            if (TryParseStrictObject(quotedKeys, out root))
                return true;
        }

        try
        {
            JToken.Parse(quotedKeys);
        }
        catch (Exception exception)
        {
            error = "JSON parse failed after repair: " + exception.Message;
            return false;
        }

        error = "repaired JSON root is not an object.";
        return false;
    }

    private static bool TryParseStrictObject(string jsonText, out JObject root)
    {
        root = null;

        if (string.IsNullOrWhiteSpace(jsonText))
            return false;

        try
        {
            JToken token = JToken.Parse(jsonText);
            root = token as JObject;
            return root != null;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryExtractOrCompleteJsonObject(
        string text,
        out string jsonText,
        List<string> repairSteps
    )
    {
        jsonText = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        string cleaned = StripCodeFence(text);
        int start = cleaned.IndexOf('{');
        if (start < 0)
            return false;

        StringBuilder builder = new StringBuilder(cleaned.Length + 16);
        List<char> expectedClosers = new List<char>();

        bool inString = false;
        bool escaped = false;
        bool sawAnyBrace = false;

        for (int i = start; i < cleaned.Length; i++)
        {
            char c = cleaned[i];
            builder.Append(c);

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (inString && c == '\\')
            {
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
                sawAnyBrace = true;
                expectedClosers.Add('}');
                continue;
            }

            if (c == '[')
            {
                expectedClosers.Add(']');
                continue;
            }

            if (c == '}' || c == ']')
            {
                if (expectedClosers.Count == 0)
                    break;

                char expected = expectedClosers[expectedClosers.Count - 1];
                if (c == expected)
                {
                    expectedClosers.RemoveAt(expectedClosers.Count - 1);
                    if (expectedClosers.Count == 0)
                    {
                        jsonText = builder.ToString();
                        return sawAnyBrace;
                    }

                    continue;
                }

                if (IsCompatibleCloser(c, expected))
                {
                    expectedClosers.RemoveAt(expectedClosers.Count - 1);
                    repairSteps.Add("accepted compatible mismatched closer.");
                    continue;
                }

                break;
            }
        }

        if (!sawAnyBrace)
            return false;

        if (inString)
        {
            builder.Append('"');
            repairSteps.Add("closed unterminated JSON string.");
        }

        if (expectedClosers.Count > 0)
        {
            for (int i = expectedClosers.Count - 1; i >= 0; i--)
            {
                builder.Append(expectedClosers[i]);
            }

            repairSteps.Add("appended missing JSON closing bracket(s): " + expectedClosers.Count + ".");
        }

        jsonText = builder.ToString();
        return !string.IsNullOrWhiteSpace(jsonText);
    }

    private static bool IsCompatibleCloser(char actual, char expected)
    {
        return (actual == '}' || actual == ']') && (expected == '}' || expected == ']');
    }

    private static JObject BuildParserRoot(
        JObject source,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        JObject root = UnwrapParserRoot(source, repairSteps);

        JObject result = new JObject
        {
            ["thinking"] = ReadString(root, "thinking", "reasoning", "thought") ?? string.Empty,
            ["action"] = RepairActionArray(
                ReadFirstToken(root, "action", "actions", "actorActions", "commands"),
                context,
                repairSteps
            ),
        };

        JArray actionActors = result["action"] as JArray;
        result["dialog"] = RepairDialogArray(
            ReadFirstToken(root, "dialog", "dialogs", "lines"),
            actionActors,
            repairSteps
        );

        return result;
    }

    private static JObject UnwrapParserRoot(JObject source, List<string> repairSteps)
    {
        if (source == null)
            return new JObject();

        if (HasAnyParserKey(source))
            return source;

        JToken outputToken = ReadFirstToken(source, "output", "result", "parserOutput", "data");
        JObject outputObject = outputToken as JObject;
        if (outputObject != null && HasAnyParserKey(outputObject))
        {
            repairSteps.Add("unwrapped parser output object.");
            return outputObject;
        }

        return source;
    }

    private static bool HasAnyParserKey(JObject source)
    {
        if (source == null)
            return false;

        return source["thinking"] != null
            || source["dialog"] != null
            || source["action"] != null
            || source["actions"] != null
            || source["actorActions"] != null
            || source["commands"] != null;
    }

    private static JArray RepairActionArray(
        JToken token,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        JArray sourceArray = ToArray(token);
        Dictionary<string, JArray> sequenceByActor = new Dictionary<string, JArray>(StringComparer.Ordinal);
        List<string> actorOrder = new List<string>();

        for (int i = 0; i < sourceArray.Count; i++)
        {
            JObject actorObject = sourceArray[i] as JObject;
            if (actorObject == null)
                continue;

            string actorId = context.NormalizeUnitId(
                ReadString(actorObject, "unitId", "actorUnitId", "actor", "unit")
            );

            if (!context.IsAllowedActor(actorId))
            {
                string fallbackActor = context.FindFallbackActor();
                if (string.IsNullOrWhiteSpace(fallbackActor))
                {
                    repairSteps.Add("dropped action entry without allowed actor.");
                    continue;
                }

                repairSteps.Add("replaced invalid actor '" + (actorId ?? string.Empty) + "' with '" + fallbackActor + "'.");
                actorId = fallbackActor;
            }

            JArray sourceSequence = ToArray(ReadFirstToken(actorObject, "sequence", "actions", "commands"));
            if (sourceSequence.Count == 0 && LooksLikeSingleAction(actorObject))
                sourceSequence.Add(actorObject);

            JArray targetSequence;
            if (!sequenceByActor.TryGetValue(actorId, out targetSequence))
            {
                targetSequence = new JArray();
                sequenceByActor.Add(actorId, targetSequence);
                actorOrder.Add(actorId);
            }

            for (int j = 0; j < sourceSequence.Count; j++)
            {
                if (targetSequence.Count >= context.MaxActionsPerActor)
                    break;

                JObject repairedAction = RepairActionItem(actorId, sourceSequence[j], context, repairSteps);
                if (repairedAction != null)
                    targetSequence.Add(repairedAction);
            }
        }

        JArray result = new JArray();
        for (int i = 0; i < actorOrder.Count; i++)
        {
            string actorId = actorOrder[i];
            JArray sequence = sequenceByActor[actorId];
            if (sequence.Count == 0)
                continue;

            result.Add(
                new JObject
                {
                    ["unitId"] = actorId,
                    ["sequence"] = sequence,
                }
            );
        }

        return result;
    }

    private static JObject RepairActionItem(
        string actorId,
        JToken token,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        JObject source = token as JObject;
        if (source == null)
            return null;

        string rawType = ReadString(source, "type", "actionType", "kind", "action");
        string rawSubtype = ReadString(source, "subtype", "moveSubtype", "move_type", "moveType");

        string type = NormalizeActionType(rawType, rawSubtype);
        if (string.IsNullOrWhiteSpace(type))
            return null;

        switch (type)
        {
            case "attack":
                return RepairAttackAction(actorId, source, context, repairSteps);

            case "move":
                return RepairMoveAction(actorId, source, rawType, rawSubtype, context, repairSteps);

            case "skill":
                return RepairSkillAction(actorId, source, context, repairSteps);

            case "wait":
                return RepairWaitAction(source, context);

            case "skillControl":
                return RepairSkillControlAction(actorId, source, context);

            default:
                repairSteps.Add("dropped unsupported action type '" + type + "'.");
                return null;
        }
    }

    private static JObject RepairAttackAction(
        string actorId,
        JObject source,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        string targetId = context.NormalizeUnitId(
            ReadString(source, "target", "to", "enemy", "targetUnitId")
        );

        if (!context.IsAllowedAttackTarget(targetId))
        {
            string replacement = context.FindAttackTarget(actorId);
            if (string.IsNullOrWhiteSpace(replacement))
            {
                repairSteps.Add("dropped attack without valid target.");
                return null;
            }

            repairSteps.Add("replaced invalid attack target '" + (targetId ?? string.Empty) + "' with '" + replacement + "'.");
            targetId = replacement;
        }

        return new JObject
        {
            ["type"] = "attack",
            ["target"] = targetId,
        };
    }

    private static JObject RepairMoveAction(
        string actorId,
        JObject source,
        string rawType,
        string rawSubtype,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        string subtype = NormalizeMoveSubtype(rawSubtype);
        if (string.IsNullOrWhiteSpace(subtype))
            subtype = NormalizeMoveSubtype(rawType);

        if (string.IsNullOrWhiteSpace(subtype))
            subtype = "approachOpponent";

        string movementType = NormalizeMovementType(
            ReadString(source, "movementType", "movement", "moveStyle", "style")
        );

        string targetId = context.NormalizeUnitId(
            ReadString(source, "to", "target", "targetUnitId", "anchor")
        );

        string repairedTarget = context.FindMoveTarget(actorId, subtype, targetId);
        if (string.IsNullOrWhiteSpace(repairedTarget))
        {
            repairSteps.Add("dropped move without valid to target.");
            return null;
        }

        if (!string.Equals(targetId, repairedTarget, StringComparison.Ordinal))
        {
            repairSteps.Add(
                "replaced move target '"
                    + (targetId ?? string.Empty)
                    + "' with '"
                    + repairedTarget
                    + "' for subtype '"
                    + subtype
                    + "'."
            );
        }

        return new JObject
        {
            ["type"] = "move",
            ["subtype"] = subtype,
            ["movementType"] = movementType,
            ["to"] = repairedTarget,
        };
    }

    private static JObject RepairSkillAction(
        string actorId,
        JObject source,
        ParserRepairContext context,
        List<string> repairSteps
    )
    {
        if (!context.TryGetAlly(actorId, out SotAllyUnitDto actor) || string.IsNullOrWhiteSpace(actor.skillDescription))
        {
            repairSteps.Add("dropped skill because actor has no skill metadata.");
            return null;
        }

        string targetId = context.NormalizeUnitId(
            ReadString(source, "target", "to", "targetUnitId")
        );

        string repairedTarget = context.FindSkillTarget(actorId, actor, targetId);
        if (string.IsNullOrWhiteSpace(repairedTarget))
        {
            repairSteps.Add("dropped skill without valid target.");
            return null;
        }

        return new JObject
        {
            ["type"] = "skill",
            ["description"] = actor.skillDescription,
            ["target"] = repairedTarget,
        };
    }

    private static JObject RepairWaitAction(JObject source, ParserRepairContext context)
    {
        float duration = ReadFloat(source, context.DefaultWaitDuration, "durationSec", "duration", "seconds", "sec");
        duration = Clamp(duration, context.WaitMin, context.WaitMax);

        return new JObject
        {
            ["type"] = "wait",
            ["durationSec"] = duration,
        };
    }

    private static JObject RepairSkillControlAction(string actorId, JObject source, ParserRepairContext context)
    {
        if (!context.ActorHasSkill(actorId))
            return null;

        string mode = NormalizeSkillControlMode(ReadString(source, "mode", "controlMode", "skillMode"));
        if (string.IsNullOrWhiteSpace(mode))
            mode = "defer";

        JObject result = new JObject
        {
            ["type"] = "skillControl",
            ["mode"] = mode,
        };

        if (mode == "defer")
        {
            float duration = ReadFloat(
                source,
                context.DefaultSkillControlDeferDuration,
                "durationSec",
                "duration",
                "seconds",
                "sec"
            );
            result["durationSec"] = Clamp(duration, context.SkillControlDeferMin, context.SkillControlDeferMax);
        }

        return result;
    }

    private static JArray RepairDialogArray(JToken token, JArray actionActors, List<string> repairSteps)
    {
        Dictionary<string, string> textByActor = new Dictionary<string, string>(StringComparer.Ordinal);
        JArray sourceArray = ToArray(token);

        for (int i = 0; i < sourceArray.Count; i++)
        {
            JObject line = sourceArray[i] as JObject;
            if (line == null)
                continue;

            string unitId = NormalizeUnitIdLoose(ReadString(line, "unitId", "actorUnitId", "actor", "unit"));
            if (string.IsNullOrWhiteSpace(unitId))
                continue;

            string text = ReadString(line, "text", "line", "dialog", "message");
            if (string.IsNullOrWhiteSpace(text))
                text = "명령을 확인했다.";

            if (!textByActor.ContainsKey(unitId))
                textByActor.Add(unitId, text);
        }

        JArray result = new JArray();

        if (actionActors == null || actionActors.Count == 0)
            return result;

        for (int i = 0; i < actionActors.Count; i++)
        {
            JObject actorAction = actionActors[i] as JObject;
            if (actorAction == null)
                continue;

            string unitId = actorAction.Value<string>("unitId");
            if (string.IsNullOrWhiteSpace(unitId))
                continue;

            string text;
            if (!textByActor.TryGetValue(unitId, out text) || string.IsNullOrWhiteSpace(text))
            {
                text = "명령을 확인했다.";
                repairSteps.Add("generated missing dialog for actor '" + unitId + "'.");
            }

            result.Add(
                new JObject
                {
                    ["unitId"] = unitId,
                    ["text"] = text,
                }
            );
        }

        return result;
    }

    private static bool LooksLikeSingleAction(JObject obj)
    {
        if (obj == null)
            return false;

        return obj["type"] != null
            || obj["actionType"] != null
            || obj["kind"] != null
            || obj["subtype"] != null
            || obj["target"] != null
            || obj["to"] != null;
    }

    private static JArray ToArray(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return new JArray();

        JArray array = token as JArray;
        if (array != null)
            return array;

        return new JArray { token };
    }

    private static JToken ReadFirstToken(JObject obj, params string[] names)
    {
        if (obj == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            JToken token = obj[names[i]];
            if (token != null && token.Type != JTokenType.Null)
                return token;
        }

        return null;
    }

    private static string ReadString(JObject obj, params string[] names)
    {
        JToken token = ReadFirstToken(obj, names);
        if (token == null || token.Type == JTokenType.Null)
            return null;

        if (token.Type == JTokenType.String)
            return token.Value<string>();

        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.Boolean)
            return token.ToString(Formatting.None);

        return null;
    }

    private static float ReadFloat(JObject obj, float fallback, params string[] names)
    {
        JToken token = ReadFirstToken(obj, names);
        if (token == null || token.Type == JTokenType.Null)
            return fallback;

        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            return token.Value<float>();

        if (token.Type == JTokenType.String && float.TryParse(token.Value<string>(), out float parsed))
            return parsed;

        return fallback;
    }

    private static string NormalizeActionType(string rawType, string rawSubtype)
    {
        string normalized = NormalizeToken(rawType);
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = NormalizeToken(rawSubtype);

        switch (normalized)
        {
            case "attack":
            case "strike":
            case "hit":
            case "타격":
            case "공격":
                return "attack";

            case "move":
            case "movement":
            case "approach":
            case "approachopponent":
            case "charge":
            case "rush":
            case "advance":
            case "retreat":
            case "back":
            case "flee":
            case "escape":
            case "support":
            case "assist":
            case "cover":
            case "help":
            case "guard":
            case "defend":
            case "hold":
            case "holdfront":
            case "돌진":
            case "돌격":
            case "후퇴":
            case "이탈":
            case "지원":
            case "보호":
            case "도움":
            case "방어":
            case "전열유지":
                return "move";

            case "skill":
            case "cast":
            case "ability":
            case "스킬":
                return "skill";

            case "wait":
            case "delay":
            case "standby":
            case "대기":
                return "wait";

            case "skillcontrol":
            case "skill_control":
            case "defer":
            case "forbid":
            case "noskill":
            case "no_skill":
            case "스킬제어":
            case "스킬금지":
            case "스킬지연":
                return "skillControl";

            default:
                return null;
        }
    }

    private static string NormalizeMoveSubtype(string rawSubtype)
    {
        string normalized = NormalizeToken(rawSubtype);

        switch (normalized)
        {
            case "approachopponent":
            case "approach":
            case "charge":
            case "rush":
            case "advance":
            case "engage":
            case "돌진":
            case "돌격":
            case "접근":
            case "압박":
                return "approachOpponent";

            case "escape":
            case "retreat":
            case "back":
            case "flee":
            case "withdraw":
            case "후퇴":
            case "이탈":
            case "도주":
                return "escape";

            case "help":
            case "support":
            case "assist":
            case "cover":
            case "protect":
            case "지원":
            case "보호":
            case "도움":
            case "도와":
                return "help";

            case "holdfront":
            case "hold":
            case "guard":
            case "defend":
            case "front":
            case "방어":
            case "버티기":
            case "전열":
            case "전열유지":
                return "holdFront";

            default:
                return null;
        }
    }

    private static string NormalizeMovementType(string rawMovementType)
    {
        string normalized = NormalizeToken(rawMovementType);
        if (normalized == "flank" || normalized == "side" || normalized == "rear" || normalized == "우회")
            return "flank";

        return "direct";
    }

    private static string NormalizeSkillControlMode(string rawMode)
    {
        string normalized = NormalizeToken(rawMode);
        switch (normalized)
        {
            case "forbid":
            case "ban":
            case "noskill":
            case "no_skill":
            case "금지":
            case "쓰지마":
                return "forbid";

            case "defer":
            case "delay":
            case "later":
            case "hold":
            case "지연":
            case "미뤄":
            case "아껴":
                return "defer";

            default:
                return null;
        }
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                continue;

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private static string NormalizeUnitIdLoose(string rawUnitId)
    {
        if (string.IsNullOrWhiteSpace(rawUnitId))
            return null;

        Match match = UnitIdLooseRegex.Match(rawUnitId.Trim());
        if (!match.Success)
            return null;

        string side = match.Groups[1].Value.ToUpperInvariant();
        if (!int.TryParse(match.Groups[2].Value, out int number))
            return null;

        if (number < 0 || number > 99)
            return null;

        return side + "_" + number.ToString("00");
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

    private static string RemoveTrailingCommasOutsideStrings(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        StringBuilder sb = new StringBuilder(text.Length);
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (escaped)
            {
                sb.Append(c);
                escaped = false;
                continue;
            }

            if (inString && c == '\\')
            {
                sb.Append(c);
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                sb.Append(c);
                continue;
            }

            if (!inString && c == ',')
            {
                int next = i + 1;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                    next++;

                if (next < text.Length && (text[next] == '}' || text[next] == ']'))
                    continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string QuoteKnownUnquotedKeys(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        string[] keys =
        {
            "thinking",
            "dialog",
            "action",
            "unitId",
            "text",
            "sequence",
            "type",
            "subtype",
            "movementType",
            "to",
            "target",
            "description",
            "mode",
            "durationSec",
        };

        string result = text;
        for (int i = 0; i < keys.Length; i++)
        {
            string key = keys[i];
            result = Regex.Replace(
                result,
                @"(?<=[\{\[,]\s*)" + key + @"\s*:",
                "\"" + key + "\":",
                RegexOptions.CultureInvariant
            );
        }

        return result;
    }

    private static float Clamp(float value, float min, float max)
    {
        if (max < min)
            return value;

        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    private sealed class ParserRepairContext
    {
        private readonly Dictionary<string, SotAllyUnitDto> _allies = new Dictionary<string, SotAllyUnitDto>(
            StringComparer.Ordinal
        );

        private readonly Dictionary<string, SotEnemyUnitDto> _enemies = new Dictionary<string, SotEnemyUnitDto>(
            StringComparer.Ordinal
        );

        private readonly HashSet<string> _allowedActors = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _allowedAttackTargets = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _validMoveToUnits = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _deadAllies = new HashSet<string>(StringComparer.Ordinal);

        public int MaxActionsPerActor { get; }
        public float WaitMin { get; }
        public float WaitMax { get; }
        public float SkillControlDeferMin { get; }
        public float SkillControlDeferMax { get; }
        public float DefaultWaitDuration { get; }
        public float DefaultSkillControlDeferDuration { get; }

        public ParserRepairContext(SotParserRequestDto request)
        {
            SotAreaSituationDto area = request != null && request.input != null ? request.input.area_situation : null;
            if (area != null)
            {
                AddAllies(area.allies);
                AddEnemies(area.enemies);
            }

            SotCommandAnalysisDto analysis = request != null ? request.commandAnalysis : null;
            AddAll(_allowedActors, analysis != null ? analysis.allowedActors : null);
            AddAll(_allowedAttackTargets, analysis != null ? analysis.allowedAttackTargets : null);
            AddAll(_validMoveToUnits, analysis != null ? analysis.validMoveToUnits : null);
            AddAll(_deadAllies, analysis != null ? analysis.deadAllies : null);

            SotActionPolicyDto policy = analysis != null ? analysis.actionPolicy : null;
            MaxActionsPerActor = policy != null && policy.maxActionsPerActor > 0 ? policy.maxActionsPerActor : 3;

            WaitMin = policy != null ? policy.waitDurationSecMin : 1f;
            WaitMax = policy != null ? policy.waitDurationSecMax : 10f;
            SkillControlDeferMin = policy != null ? policy.skillControlDeferSecMin : 1f;
            SkillControlDeferMax = policy != null ? policy.skillControlDeferSecMax : 10f;

            DefaultWaitDuration = Clamp(2f, WaitMin, WaitMax);
            DefaultSkillControlDeferDuration = Clamp(5f, SkillControlDeferMin, SkillControlDeferMax);
        }

        public string NormalizeUnitId(string rawUnitId)
        {
            return NormalizeUnitIdLoose(rawUnitId);
        }

        public bool IsAllowedActor(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) && _allowedActors.Contains(unitId);
        }

        public bool IsAllowedAttackTarget(string unitId)
        {
            return !string.IsNullOrWhiteSpace(unitId) && _allowedAttackTargets.Contains(unitId);
        }

        public bool TryGetAlly(string unitId, out SotAllyUnitDto ally)
        {
            ally = null;
            return !string.IsNullOrWhiteSpace(unitId) && _allies.TryGetValue(unitId, out ally);
        }

        public bool ActorHasSkill(string actorId)
        {
            return TryGetAlly(actorId, out SotAllyUnitDto actor) && !string.IsNullOrWhiteSpace(actor.skillDescription);
        }

        public string FindFallbackActor()
        {
            foreach (string actorId in _allowedActors)
                return actorId;

            return null;
        }

        public string FindAttackTarget(string actorId)
        {
            if (TryGetAlly(actorId, out SotAllyUnitDto actor))
            {
                string closest = NormalizeUnitId(actor.closestTargetableOpponent);
                if (IsAllowedAttackTarget(closest))
                    return closest;

                string farthest = NormalizeUnitId(actor.farthestTargetableOpponent);
                if (IsAllowedAttackTarget(farthest))
                    return farthest;
            }

            foreach (string targetId in _allowedAttackTargets)
                return targetId;

            return null;
        }

        public string FindMoveTarget(string actorId, string subtype, string currentTarget)
        {
            string normalizedCurrent = NormalizeUnitId(currentTarget);

            switch (subtype)
            {
                case "approachOpponent":
                    if (IsAllowedAttackTarget(normalizedCurrent))
                        return normalizedCurrent;

                    return FindAttackTarget(actorId);

                case "escape":
                    if (string.Equals(normalizedCurrent, actorId, StringComparison.Ordinal))
                        return actorId;

                    if (IsLivingAlly(normalizedCurrent))
                        return normalizedCurrent;

                    if (TryGetAlly(actorId, out SotAllyUnitDto escapeActor))
                    {
                        string farthestAlly = NormalizeUnitId(escapeActor.farthestAliveAlly);
                        if (IsLivingAlly(farthestAlly))
                            return farthestAlly;

                        string closestAlly = NormalizeUnitId(escapeActor.closestAliveAlly);
                        if (IsLivingAlly(closestAlly))
                            return closestAlly;
                    }

                    return actorId;

                case "help":
                    if (!string.Equals(normalizedCurrent, actorId, StringComparison.Ordinal) && IsLivingAlly(normalizedCurrent))
                        return normalizedCurrent;

                    if (TryGetAlly(actorId, out SotAllyUnitDto helpActor))
                    {
                        string closestAlly = NormalizeUnitId(helpActor.closestAliveAlly);
                        if (!string.Equals(closestAlly, actorId, StringComparison.Ordinal) && IsLivingAlly(closestAlly))
                            return closestAlly;

                        string farthestAlly = NormalizeUnitId(helpActor.farthestAliveAlly);
                        if (!string.Equals(farthestAlly, actorId, StringComparison.Ordinal) && IsLivingAlly(farthestAlly))
                            return farthestAlly;
                    }

                    foreach (KeyValuePair<string, SotAllyUnitDto> pair in _allies)
                    {
                        if (!string.Equals(pair.Key, actorId, StringComparison.Ordinal) && IsLivingAlly(pair.Key))
                            return pair.Key;
                    }

                    return null;

                case "holdFront":
                    if (string.Equals(normalizedCurrent, actorId, StringComparison.Ordinal))
                        return actorId;

                    if (IsLivingAlly(normalizedCurrent) || IsAllowedAttackTarget(normalizedCurrent))
                        return normalizedCurrent;

                    string attackTarget = FindAttackTarget(actorId);
                    if (!string.IsNullOrWhiteSpace(attackTarget))
                        return attackTarget;

                    return actorId;

                default:
                    return null;
            }
        }

        public string FindSkillTarget(string actorId, SotAllyUnitDto actor, string currentTarget)
        {
            if (actor == null)
                return null;

            string normalizedCurrent = NormalizeUnitId(currentTarget);

            if (actor.IsSkillOnSelf)
                return actorId;

            if (actor.IsSkillOnOtherAlly)
            {
                if (IsValidAllySkillTarget(actorId, normalizedCurrent, actor.canSkillTargetDead))
                    return normalizedCurrent;

                if (actor.canSkillTargetDead)
                {
                    foreach (string deadAlly in _deadAllies)
                    {
                        if (!string.Equals(deadAlly, actorId, StringComparison.Ordinal))
                            return deadAlly;
                    }
                }

                string moveTarget = FindMoveTarget(actorId, "help", normalizedCurrent);
                if (!string.IsNullOrWhiteSpace(moveTarget))
                    return moveTarget;

                return null;
            }

            if (IsValidEnemySkillTarget(normalizedCurrent))
                return normalizedCurrent;

            return FindAttackTarget(actorId);
        }

        private bool IsLivingAlly(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            if (!_allies.TryGetValue(unitId, out SotAllyUnitDto ally))
                return false;

            return ally.isAlive && ally.canBeTargeted;
        }

        private bool IsValidAllySkillTarget(string actorId, string unitId, bool canTargetDead)
        {
            if (string.IsNullOrWhiteSpace(unitId) || string.Equals(actorId, unitId, StringComparison.Ordinal))
                return false;

            if (!_allies.TryGetValue(unitId, out SotAllyUnitDto ally))
                return false;

            if (!ally.canBeTargeted)
                return false;

            if (ally.isAlive)
                return true;

            return canTargetDead && _deadAllies.Contains(unitId);
        }

        private bool IsValidEnemySkillTarget(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            if (!_enemies.TryGetValue(unitId, out SotEnemyUnitDto enemy))
                return false;

            return enemy.isAlive && enemy.canBeTargeted;
        }

        private void AddAllies(SotAllyUnitDto[] allies)
        {
            if (allies == null)
                return;

            for (int i = 0; i < allies.Length; i++)
            {
                SotAllyUnitDto ally = allies[i];
                if (ally == null)
                    continue;

                string unitId = NormalizeUnitId(ally.unitId);
                if (string.IsNullOrWhiteSpace(unitId))
                    continue;

                ally.unitId = unitId;
                _allies[unitId] = ally;
            }
        }

        private void AddEnemies(SotEnemyUnitDto[] enemies)
        {
            if (enemies == null)
                return;

            for (int i = 0; i < enemies.Length; i++)
            {
                SotEnemyUnitDto enemy = enemies[i];
                if (enemy == null)
                    continue;

                string unitId = NormalizeUnitId(enemy.unitId);
                if (string.IsNullOrWhiteSpace(unitId))
                    continue;

                enemy.unitId = unitId;
                _enemies[unitId] = enemy;
            }
        }

        private static void AddAll(HashSet<string> target, string[] source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                string unitId = NormalizeUnitIdLoose(source[i]);
                if (!string.IsNullOrWhiteSpace(unitId))
                    target.Add(unitId);
            }
        }
    }
}
