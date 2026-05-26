// 공백 기준 exact token만 받아 SOT 형태의 mock parser output을 만든다.
// command token 바로 뒤에 필요한 unitId/int token이 오지 않으면 invalid 처리한다.
// 이 파일은 전장 상태를 읽기만 하며 시뮬레이션 명령을 발행하지 않는다.
// duration clamp, 순응/거부, runtime 보정은 후처리 단계에서 수행한다.

/*
예시는 다음과 같음
attack A_01 E_02

공격 a_03 e_01

wait A_01 5

대기 a_02 12

dihelp A_01 A_04

우돌격 a_05 e_03

defer A_06 7
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public static class BattleMockCommandParser
{
    private const string DirectMovementType = "direct";
    private const string FlankMovementType = "flank";

    private static readonly Regex UnitIdRegex = new Regex("^[AaEe]_[0-9]{2}$", RegexOptions.Compiled);

    public static bool TryParse(
        SotParserRequestDto parserRequest,
        out BattleMockCommandParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (parserRequest == null || parserRequest.input == null)
        {
            debugLog = "invalidinput: parser request is null.";
            return false;
        }

        BattleMockCommandParserContext context = new BattleMockCommandParserContext(parserRequest);
        string commandText = parserRequest.input.command ?? string.Empty;
        string[] tokens = SplitTokens(commandText);

        if (tokens.Length == 0)
        {
            debugLog = "invalidinput: command is empty.";
            return false;
        }

        List<BattleMockCommandDebugEntry> debugEntries = new List<BattleMockCommandDebugEntry>();
        List<BattleMockActorCommandSequenceDto> actorSequences = new List<BattleMockActorCommandSequenceDto>();
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor = new Dictionary<
            string,
            BattleMockActorCommandSequenceDto
        >(StringComparer.Ordinal);

        int cursor = 0;
        int maxActionsPerActor = context.MaxActionsPerActor;

        while (cursor < tokens.Length)
        {
            if (!TryParseCommandToken(tokens[cursor], out BattleMockCommandSpec commandSpec))
            {
                debugLog = $"invalidinput: expected command token at token[{cursor}]='{tokens[cursor]}'.";
                return false;
            }

            cursor++;

            if (
                !TryParseCommand(
                    commandSpec,
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out string error
                )
            )
            {
                debugLog = "invalidinput: " + error;
                return false;
            }
        }

        result = new BattleMockCommandParseResult
        {
            parserOutput = new BattleMockParserOutputDto
            {
                thinking = "mock parser result",
                dialog = BuildDialogDtos(actorSequences),
                action = actorSequences.ToArray(),
            },
            debugLines = BuildDebugLines(debugEntries),
        };

        debugLog = BuildDebugLog(result);
        return true;
    }

    public static SotFinalActionDto[] ToFinalActionSequence(BattleMockActorCommandSequenceDto actorSequence)
    {
        if (actorSequence == null || actorSequence.sequence == null)
            return System.Array.Empty<SotFinalActionDto>();

        List<SotFinalActionDto> result = new List<SotFinalActionDto>(actorSequence.sequence.Count);

        for (int i = 0; i < actorSequence.sequence.Count; i++)
        {
            BattleMockCommandActionDto source = actorSequence.sequence[i];
            if (source == null || string.IsNullOrWhiteSpace(source.type))
                continue;

            result.Add(ToFinalAction(source));
        }

        return result.ToArray();
    }

    public static BattleMockDialogLayerOutputDto BuildMockDialogOutput(BattleMockCommandParseResult parseResult)
    {
        List<BattleMockDialogLayerLineDto> dialogs = new List<BattleMockDialogLayerLineDto>();

        BattleMockActorCommandSequenceDto[] actorSequences =
            parseResult != null && parseResult.parserOutput != null && parseResult.parserOutput.action != null
                ? parseResult.parserOutput.action
                : System.Array.Empty<BattleMockActorCommandSequenceDto>();

        for (int i = 0; i < actorSequences.Length; i++)
        {
            BattleMockActorCommandSequenceDto sequence = actorSequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.unitId))
                continue;

            dialogs.Add(new BattleMockDialogLayerLineDto { unitId = sequence.unitId, text = "알겠습니다" });
        }

        return new BattleMockDialogLayerOutputDto { dialog = dialogs.ToArray() };
    }

    private static SotFinalActionDto ToFinalAction(BattleMockCommandActionDto source)
    {
        SotFinalActionDto result = new SotFinalActionDto
        {
            type = source.type,
            subtype = source.subtype,
            movementType = source.movementType,
            to = source.to,
            target = source.target,
            description = source.description,
            mode = source.mode,
        };

        if (source.durationSec.HasValue)
            result.durationSec = source.durationSec.Value;

        return result;
    }

    private static bool TryParseCommand(
        BattleMockCommandSpec commandSpec,
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        switch (commandSpec.Kind)
        {
            case BattleMockCommandKind.Attack:
                return TryParseAttack(
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            case BattleMockCommandKind.Skill:
                return TryParseSkill(
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            case BattleMockCommandKind.Wait:
                return TryParseWait(
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            case BattleMockCommandKind.SkillControlDefer:
                return TryParseSkillControlDefer(
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            case BattleMockCommandKind.SkillControlForbid:
                return TryParseSkillControlForbid(
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            case BattleMockCommandKind.Move:
                return TryParseMove(
                    commandSpec.MoveSubtype,
                    commandSpec.MovementType,
                    tokens,
                    ref cursor,
                    context,
                    actorSequences,
                    sequenceByActor,
                    debugEntries,
                    maxActionsPerActor,
                    out error
                );

            default:
                error = "unsupported command.";
                return false;
        }
    }

    private static bool TryParseAttack(
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"attack actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!TryConsumeUnitId(tokens, ref cursor, out string targetId, out error))
            return false;

        if (!context.IsAllowedAttackTarget(targetId))
        {
            error = $"attack target '{targetId}' is not an allowed attack target.";
            return false;
        }

        BattleMockCommandActionDto action = new BattleMockCommandActionDto { type = "attack", target = targetId };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 {targetId}를 공격함"));
        return true;
    }

    private static bool TryParseSkill(
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"skill actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!context.TryGetAlly(actorId, out SotAllyUnitDto actor))
        {
            error = $"skill actor '{actorId}' is not found in allies.";
            return false;
        }

        if (!HasSkill(actor))
        {
            error = $"skill actor '{actorId}' has no skill metadata.";
            return false;
        }

        string targetId;

        if (actor.IsSkillOnSelf)
        {
            targetId = actorId;
        }
        else
        {
            if (!TryConsumeUnitId(tokens, ref cursor, out targetId, out error))
                return false;

            if (actor.IsSkillOnOtherAlly)
            {
                if (!context.IsValidSkillAllyTarget(actorId, targetId, actor.canSkillTargetDead))
                {
                    error = $"skill target '{targetId}' is not a valid ally skill target for '{actorId}'.";
                    return false;
                }
            }
            else
            {
                if (!context.IsValidSkillEnemyTarget(targetId))
                {
                    error = $"skill target '{targetId}' is not a valid enemy skill target.";
                    return false;
                }
            }
        }

        BattleMockCommandActionDto action = new BattleMockCommandActionDto
        {
            type = "skill",
            description = actor.skillDescription ?? string.Empty,
            target = targetId,
        };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        if (targetId == actorId)
            debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 자신에게 스킬 사용"));
        else
            debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 {targetId}에게 스킬 사용"));

        return true;
    }

    private static bool TryParseWait(
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"wait actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!TryConsumeInt(tokens, ref cursor, out int durationSec, out error))
            return false;

        BattleMockCommandActionDto action = new BattleMockCommandActionDto { type = "wait", durationSec = durationSec };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 {durationSec}초 대기함"));
        return true;
    }

    private static bool TryParseSkillControlDefer(
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"defer actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!context.ActorHasSkill(actorId))
        {
            error = $"defer actor '{actorId}' has no skill metadata.";
            return false;
        }

        if (!TryConsumeInt(tokens, ref cursor, out int durationSec, out error))
            return false;

        BattleMockCommandActionDto action = new BattleMockCommandActionDto
        {
            type = "skillControl",
            mode = "defer",
            durationSec = durationSec,
        };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 스킬 사용을 {durationSec}초 뒤로 미룸"));
        return true;
    }

    private static bool TryParseSkillControlForbid(
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"forbid actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!context.ActorHasSkill(actorId))
        {
            error = $"forbid actor '{actorId}' has no skill metadata.";
            return false;
        }

        BattleMockCommandActionDto action = new BattleMockCommandActionDto { type = "skillControl", mode = "forbid" };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 스킬 사용을 금지함"));
        return true;
    }

    private static bool TryParseMove(
        string subtype,
        string movementType,
        string[] tokens,
        ref int cursor,
        BattleMockCommandParserContext context,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        List<BattleMockCommandDebugEntry> debugEntries,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!TryConsumeUnitId(tokens, ref cursor, out string actorId, out error))
            return false;

        if (!context.IsAllowedActor(actorId))
        {
            error = $"move actor '{actorId}' is not an allowed actor.";
            return false;
        }

        if (!TryConsumeUnitId(tokens, ref cursor, out string targetId, out error))
            return false;

        if (!IsValidMoveTarget(actorId, targetId, subtype, context, out error))
            return false;

        BattleMockCommandActionDto action = new BattleMockCommandActionDto
        {
            type = "move",
            subtype = subtype,
            movementType = movementType,
            to = targetId,
        };

        if (!AddAction(actorId, action, actorSequences, sequenceByActor, maxActionsPerActor, out error))
            return false;

        if (subtype == "holdFront" && actorId == targetId)
            debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 자리를 지킴"));
        else
            debugEntries.Add(new BattleMockCommandDebugEntry($"{actorId}가 {targetId}에게 move_{subtype} 이동"));

        return true;
    }

    private static bool IsValidMoveTarget(
        string actorId,
        string targetId,
        string subtype,
        BattleMockCommandParserContext context,
        out string error
    )
    {
        error = string.Empty;

        switch (subtype)
        {
            case "approachOpponent":
                if (!context.IsAllowedAttackTarget(targetId))
                {
                    error = $"approachOpponent target '{targetId}' is not an allowed attack target.";
                    return false;
                }
                return true;

            case "escape":
            case "help":
                if (actorId == targetId)
                {
                    error = $"{subtype} target cannot be self.";
                    return false;
                }

                if (!context.IsLivingAlly(targetId))
                {
                    error = $"{subtype} target '{targetId}' is not a living ally.";
                    return false;
                }

                return true;

            case "holdFront":
                if (actorId == targetId)
                    return true;

                if (!context.IsLivingAlly(targetId) && !context.IsAllowedAttackTarget(targetId))
                {
                    error = $"holdFront target '{targetId}' is not a valid ally or enemy anchor.";
                    return false;
                }

                return true;

            default:
                error = $"unsupported move subtype '{subtype}'.";
                return false;
        }
    }

    private static bool TryParseCommandToken(string rawToken, out BattleMockCommandSpec commandSpec)
    {
        commandSpec = default;

        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        string token = rawToken.Trim();
        string lower = token.ToLowerInvariant();

        if (lower == "attack" || token == "공격")
        {
            commandSpec = BattleMockCommandSpec.Simple(BattleMockCommandKind.Attack);
            return true;
        }

        if (lower == "skill" || token == "스킬")
        {
            commandSpec = BattleMockCommandSpec.Simple(BattleMockCommandKind.Skill);
            return true;
        }

        if (lower == "wait" || token == "대기")
        {
            commandSpec = BattleMockCommandSpec.Simple(BattleMockCommandKind.Wait);
            return true;
        }

        if (lower == "defer" || token == "미루기")
        {
            commandSpec = BattleMockCommandSpec.Simple(BattleMockCommandKind.SkillControlDefer);
            return true;
        }

        if (lower == "forbid" || token == "금지")
        {
            commandSpec = BattleMockCommandSpec.Simple(BattleMockCommandKind.SkillControlForbid);
            return true;
        }

        return TryParseMoveToken(token, out commandSpec);
    }

    private static bool TryParseMoveToken(string rawToken, out BattleMockCommandSpec commandSpec)
    {
        commandSpec = default;

        string movementType = DirectMovementType;
        string baseToken = rawToken;
        string lower = rawToken.ToLowerInvariant();

        if (rawToken.StartsWith("직", StringComparison.Ordinal))
        {
            movementType = DirectMovementType;
            baseToken = rawToken.Substring(1);
            lower = baseToken.ToLowerInvariant();
        }
        else if (rawToken.StartsWith("우", StringComparison.Ordinal))
        {
            movementType = FlankMovementType;
            baseToken = rawToken.Substring(1);
            lower = baseToken.ToLowerInvariant();
        }
        else if (lower.StartsWith("di", StringComparison.Ordinal) && lower.Length > 2)
        {
            movementType = DirectMovementType;
            baseToken = rawToken.Substring(2);
            lower = baseToken.ToLowerInvariant();
        }
        else if (lower.StartsWith("fl", StringComparison.Ordinal) && lower.Length > 2)
        {
            movementType = FlankMovementType;
            baseToken = rawToken.Substring(2);
            lower = baseToken.ToLowerInvariant();
        }

        if (lower == "approachopponent" || baseToken == "돌격")
        {
            commandSpec = BattleMockCommandSpec.Move("approachOpponent", movementType);
            return true;
        }

        if (lower == "escape" || baseToken == "도망")
        {
            commandSpec = BattleMockCommandSpec.Move("escape", movementType);
            return true;
        }

        if (lower == "help" || baseToken == "도움")
        {
            commandSpec = BattleMockCommandSpec.Move("help", movementType);
            return true;
        }

        if (lower == "holdfront" || lower == "holdfrontline" || baseToken == "전열유지")
        {
            commandSpec = BattleMockCommandSpec.Move("holdFront", movementType);
            return true;
        }

        return false;
    }

    private static bool TryConsumeUnitId(string[] tokens, ref int cursor, out string unitId, out string error)
    {
        unitId = null;
        error = string.Empty;

        if (cursor >= tokens.Length)
        {
            error = "expected unit id, but input ended.";
            return false;
        }

        string token = tokens[cursor];

        if (!UnitIdRegex.IsMatch(token))
        {
            error = $"expected unit id at token[{cursor}], but got '{token}'.";
            return false;
        }

        unitId = token.ToUpperInvariant();
        cursor++;
        return true;
    }

    private static bool TryConsumeInt(string[] tokens, ref int cursor, out int value, out string error)
    {
        value = 0;
        error = string.Empty;

        if (cursor >= tokens.Length)
        {
            error = "expected integer, but input ended.";
            return false;
        }

        string token = tokens[cursor];

        if (!int.TryParse(token, out value))
        {
            error = $"expected integer at token[{cursor}], but got '{token}'.";
            return false;
        }

        cursor++;
        return true;
    }

    private static bool AddAction(
        string actorId,
        BattleMockCommandActionDto action,
        List<BattleMockActorCommandSequenceDto> actorSequences,
        Dictionary<string, BattleMockActorCommandSequenceDto> sequenceByActor,
        int maxActionsPerActor,
        out string error
    )
    {
        error = string.Empty;

        if (!sequenceByActor.TryGetValue(actorId, out BattleMockActorCommandSequenceDto sequence))
        {
            sequence = new BattleMockActorCommandSequenceDto
            {
                unitId = actorId,
                sequence = new List<BattleMockCommandActionDto>(),
            };

            sequenceByActor.Add(actorId, sequence);
            actorSequences.Add(sequence);
        }

        if (sequence.sequence.Count >= maxActionsPerActor)
        {
            error = $"actor '{actorId}' exceeds max action count {maxActionsPerActor}.";
            return false;
        }

        sequence.sequence.Add(action);
        return true;
    }

    private static BattleMockCommandDialogDto[] BuildDialogDtos(List<BattleMockActorCommandSequenceDto> actorSequences)
    {
        List<BattleMockCommandDialogDto> dialogs = new List<BattleMockCommandDialogDto>();

        for (int i = 0; i < actorSequences.Count; i++)
        {
            BattleMockActorCommandSequenceDto sequence = actorSequences[i];
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.unitId))
                continue;

            dialogs.Add(new BattleMockCommandDialogDto { unitId = sequence.unitId, text = "명령을 확인했다." });
        }

        return dialogs.ToArray();
    }

    private static string[] BuildDebugLines(List<BattleMockCommandDebugEntry> entries)
    {
        string[] lines = new string[entries.Count];

        for (int i = 0; i < entries.Count; i++)
            lines[i] = entries[i].Text;

        return lines;
    }

    private static string BuildDebugLog(BattleMockCommandParseResult result)
    {
        StringBuilder sb = new StringBuilder(256);

        sb.AppendLine("valid mock parser input");

        if (result != null && result.debugLines != null)
        {
            for (int i = 0; i < result.debugLines.Length; i++)
                sb.AppendLine(result.debugLines[i]);
        }

        return sb.ToString().TrimEnd();
    }

    private static string[] SplitTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool HasSkill(SotAllyUnitDto actor)
    {
        return actor != null && !string.IsNullOrWhiteSpace(actor.skillDescription);
    }

    private readonly struct BattleMockCommandSpec
    {
        public readonly BattleMockCommandKind Kind;
        public readonly string MoveSubtype;
        public readonly string MovementType;

        private BattleMockCommandSpec(BattleMockCommandKind kind, string moveSubtype, string movementType)
        {
            Kind = kind;
            MoveSubtype = moveSubtype;
            MovementType = movementType;
        }

        public static BattleMockCommandSpec Simple(BattleMockCommandKind kind)
        {
            return new BattleMockCommandSpec(kind, null, null);
        }

        public static BattleMockCommandSpec Move(string moveSubtype, string movementType)
        {
            return new BattleMockCommandSpec(BattleMockCommandKind.Move, moveSubtype, movementType);
        }
    }

    private readonly struct BattleMockCommandDebugEntry
    {
        public readonly string Text;

        public BattleMockCommandDebugEntry(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    private enum BattleMockCommandKind
    {
        Attack,
        Skill,
        Wait,
        SkillControlDefer,
        SkillControlForbid,
        Move,
    }

    private sealed class BattleMockCommandParserContext
    {
        private readonly Dictionary<string, SotAllyUnitDto> _allies = new Dictionary<string, SotAllyUnitDto>(
            StringComparer.Ordinal
        );

        private readonly Dictionary<string, SotEnemyUnitDto> _enemies = new Dictionary<string, SotEnemyUnitDto>(
            StringComparer.Ordinal
        );

        private readonly HashSet<string> _allowedActors = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> _allowedAttackTargets = new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> _deadAllies = new HashSet<string>(StringComparer.Ordinal);

        public int MaxActionsPerActor { get; }

        public BattleMockCommandParserContext(SotParserRequestDto request)
        {
            SotAreaSituationDto area = request != null && request.input != null ? request.input.area_situation : null;

            if (area != null && area.allies != null)
            {
                for (int i = 0; i < area.allies.Length; i++)
                {
                    SotAllyUnitDto ally = area.allies[i];
                    if (ally == null || string.IsNullOrWhiteSpace(ally.unitId))
                        continue;

                    string unitId = ally.unitId.ToUpperInvariant();
                    _allies[unitId] = ally;
                }
            }

            if (area != null && area.enemies != null)
            {
                for (int i = 0; i < area.enemies.Length; i++)
                {
                    SotEnemyUnitDto enemy = area.enemies[i];
                    if (enemy == null || string.IsNullOrWhiteSpace(enemy.unitId))
                        continue;

                    string unitId = enemy.unitId.ToUpperInvariant();
                    _enemies[unitId] = enemy;
                }
            }

            SotCommandAnalysisDto analysis = request != null ? request.commandAnalysis : null;

            AddAll(_allowedActors, analysis != null ? analysis.allowedActors : null);
            AddAll(_allowedAttackTargets, analysis != null ? analysis.allowedAttackTargets : null);
            AddAll(_deadAllies, analysis != null ? analysis.deadAllies : null);

            MaxActionsPerActor =
                analysis != null && analysis.actionPolicy != null && analysis.actionPolicy.maxActionsPerActor > 0
                    ? analysis.actionPolicy.maxActionsPerActor
                    : 3;
        }

        public bool TryGetAlly(string unitId, out SotAllyUnitDto ally)
        {
            return _allies.TryGetValue(unitId, out ally);
        }

        public bool IsAllowedActor(string unitId)
        {
            if (!_allies.TryGetValue(unitId, out SotAllyUnitDto ally))
                return false;

            if (!ally.isAlive)
                return false;

            return _allowedActors.Count == 0 || _allowedActors.Contains(unitId);
        }

        public bool IsAllowedAttackTarget(string unitId)
        {
            if (!_enemies.TryGetValue(unitId, out SotEnemyUnitDto enemy))
                return false;

            if (!enemy.isAlive || !enemy.canBeTargeted)
                return false;

            return _allowedAttackTargets.Count == 0 || _allowedAttackTargets.Contains(unitId);
        }

        public bool IsLivingAlly(string unitId)
        {
            return _allies.TryGetValue(unitId, out SotAllyUnitDto ally) && ally.isAlive;
        }

        public bool ActorHasSkill(string unitId)
        {
            return _allies.TryGetValue(unitId, out SotAllyUnitDto ally) && HasSkill(ally);
        }

        public bool IsValidSkillEnemyTarget(string unitId)
        {
            if (!_enemies.TryGetValue(unitId, out SotEnemyUnitDto enemy))
                return false;

            return enemy.isAlive && enemy.canBeTargeted;
        }

        public bool IsValidSkillAllyTarget(string actorId, string targetId, bool canSkillTargetDead)
        {
            if (actorId == targetId)
                return false;

            if (!_allies.TryGetValue(targetId, out SotAllyUnitDto ally))
                return false;

            if (ally.isAlive)
                return ally.canBeTargeted;

            return canSkillTargetDead && _deadAllies.Contains(targetId);
        }

        private static void AddAll(HashSet<string> target, string[] source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                string value = source[i];
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                target.Add(value.ToUpperInvariant());
            }
        }
    }
}

public sealed class BattleMockCommandParseResult
{
    public BattleMockParserOutputDto parserOutput;
    public string[] debugLines;
}

public sealed class BattleMockParserOutputDto
{
    public string thinking;
    public BattleMockCommandDialogDto[] dialog;
    public BattleMockActorCommandSequenceDto[] action;
}

public sealed class BattleMockCommandDialogDto
{
    public string unitId;
    public string text;
}

public sealed class BattleMockActorCommandSequenceDto
{
    public string unitId;
    public List<BattleMockCommandActionDto> sequence;
}

public sealed class BattleMockCommandActionDto
{
    public string type;
    public string subtype;
    public string movementType;
    public string to;
    public string target;
    public string description;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? durationSec;

    public string mode;
}

public sealed class BattleMockDialogLayerOutputDto
{
    public BattleMockDialogLayerLineDto[] dialog;
}

public sealed class BattleMockDialogLayerLineDto
{
    public string unitId;
    public string text;
}
