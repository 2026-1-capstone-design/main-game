// 최종 action sequence와 상태성 실패에서 하드코딩된 유닛 대사를 만든다.
// 대사 SLM/LLM 실패 또는 최후방 폴백 성공 시 사용한다.
// speechStyle 0, 1, 2만 반영하고 성격 설명은 사용하지 않는다.
// unitId는 현재 전투의 DisplayName으로 바꾸고 조사를 자동 선택한다.
// 사극풍에서는 기술이라는 표현을 사용한다.

using System;
using System.Collections.Generic;

public static class BattleCommandFallbackDialogBuilder
{
    public static SotDialogLayerResponseDto BuildFromPostprocessResult(
        BattleCommandPostprocessResult postprocessResult,
        BattleOrderRuntimeContext context
    )
    {
        if (postprocessResult == null || postprocessResult.actors == null || postprocessResult.actors.Length == 0)
            return new SotDialogLayerResponseDto { dialog = Array.Empty<SotDialogLineDto>() };

        List<SotDialogLineDto> lines = new List<SotDialogLineDto>(postprocessResult.actors.Length);

        for (int i = 0; i < postprocessResult.actors.Length; i++)
        {
            BattleCommandFinalActorDto actor = postprocessResult.actors[i];
            if (actor == null || string.IsNullOrWhiteSpace(actor.unitId))
                continue;

            string text = BuildLine(actor, context);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            lines.Add(new SotDialogLineDto { unitId = actor.unitId, text = text });
        }

        return new SotDialogLayerResponseDto { dialog = lines.ToArray() };
    }

    public static SotDialogLayerResponseDto BuildEmergencyFailureDialog(
        string actorUnitId,
        string dialogKind,
        BattleOrderRuntimeContext context
    )
    {
        if (string.IsNullOrWhiteSpace(actorUnitId))
            return new SotDialogLayerResponseDto { dialog = Array.Empty<SotDialogLineDto>() };

        int speechStyle = ResolveSpeechStyle(actorUnitId, context);
        string text = BuildEmergencyFailureLine(dialogKind, speechStyle);

        if (string.IsNullOrWhiteSpace(text))
            return new SotDialogLayerResponseDto { dialog = Array.Empty<SotDialogLineDto>() };

        return new SotDialogLayerResponseDto
        {
            dialog = new[]
            {
                new SotDialogLineDto { unitId = actorUnitId, text = text },
            },
        };
    }

    private static string BuildLine(BattleCommandFinalActorDto actor, BattleOrderRuntimeContext context)
    {
        SotFinalActionDto action = FirstAction(actor.finalActionSequence);
        if (action == null || string.IsNullOrWhiteSpace(action.type))
            return string.Empty;

        int speechStyle = ResolveSpeechStyle(actor.unitId, context);
        string type = Normalize(action.type);

        switch (type)
        {
            case "attack":
                return BuildAttackLine(action.target, speechStyle, context);

            case "skill":
                return BuildSkillLine(action.target, actor.unitId, speechStyle, context);

            case "wait":
                return BuildWaitLine(action.durationSec.HasValue ? action.durationSec.Value : 1f, speechStyle);

            case "skillcontrol":
                return BuildSkillControlLine(action.mode, speechStyle);

            case "move":
                return BuildMoveLine(action, speechStyle, context);

            default:
                return string.Empty;
        }
    }

    private static string BuildAttackLine(string target, int speechStyle, BattleOrderRuntimeContext context)
    {
        string targetText = ResolveUnitText(target, "적", context);

        switch (speechStyle)
        {
            case 1:
                return WithObjectJosa(targetText) + " 공격하겠습니다.";
            case 2:
                return WithObjectJosa(targetText) + " 치겠소.";
            default:
                return WithObjectJosa(targetText) + " 공격하겠다.";
        }
    }

    private static string BuildSkillLine(
        string target,
        string actorUnitId,
        int speechStyle,
        BattleOrderRuntimeContext context
    )
    {
        bool isSelf =
            !string.IsNullOrWhiteSpace(target)
            && !string.IsNullOrWhiteSpace(actorUnitId)
            && string.Equals(target.Trim(), actorUnitId.Trim(), StringComparison.OrdinalIgnoreCase);

        if (isSelf || string.IsNullOrWhiteSpace(target))
        {
            switch (speechStyle)
            {
                case 1:
                    return "스킬을 사용하겠습니다.";
                case 2:
                    return "기술을 쓰겠소.";
                default:
                    return "스킬을 사용한다.";
            }
        }

        string targetText = ResolveUnitText(target, "상대", context);
        switch (speechStyle)
        {
            case 1:
                return targetText + "에게 스킬을 사용하겠습니다.";
            case 2:
                return targetText + "에게 기술을 쓰겠소.";
            default:
                return targetText + "에게 스킬을 사용한다.";
        }
    }

    private static string BuildWaitLine(float durationSec, int speechStyle)
    {
        string durationText = durationSec.ToString("0.##") + "초";

        switch (speechStyle)
        {
            case 1:
                return durationText + " 대기하겠습니다.";
            case 2:
                return durationText + " 기다리겠소.";
            default:
                return durationText + " 대기한다.";
        }
    }

    private static string BuildSkillControlLine(string mode, int speechStyle)
    {
        bool defer = string.Equals(mode, "defer", StringComparison.OrdinalIgnoreCase);

        if (defer)
        {
            switch (speechStyle)
            {
                case 1:
                    return "스킬 사용을 잠시 미루겠습니다.";
                case 2:
                    return "기술은 잠시 아끼겠소.";
                default:
                    return "스킬 사용을 잠시 미룬다.";
            }
        }

        switch (speechStyle)
        {
            case 1:
                return "스킬은 사용하지 않겠습니다.";
            case 2:
                return "기술은 쓰지 않겠소.";
            default:
                return "스킬은 쓰지 않는다.";
        }
    }

    private static string BuildMoveLine(SotFinalActionDto action, int speechStyle, BattleOrderRuntimeContext context)
    {
        string subtype = Normalize(action.subtype);
        string movementType = Normalize(action.movementType);
        string targetText = ResolveUnitText(action.to, string.Empty, context);

        switch (subtype)
        {
            case "approachopponent":
                if (movementType == "flank")
                {
                    switch (speechStyle)
                    {
                        case 1:
                            return targetText + "에게 우회 접근하겠습니다.";
                        case 2:
                            return targetText + "에게 돌아가겠소.";
                        default:
                            return targetText + "에게 우회 접근한다.";
                    }
                }

                switch (speechStyle)
                {
                    case 1:
                        return targetText + "에게 접근하겠습니다.";
                    case 2:
                        return targetText + "에게 다가가겠소.";
                    default:
                        return targetText + "에게 접근한다.";
                }

            case "escape":
                if (string.IsNullOrWhiteSpace(action.to))
                {
                    switch (speechStyle)
                    {
                        case 1:
                            return "위험에서 벗어나겠습니다.";
                        case 2:
                            return "위험에서 벗어나겠소.";
                        default:
                            return "위험에서 벗어난다.";
                    }
                }

                switch (speechStyle)
                {
                    case 1:
                        return targetText + " 쪽으로 물러나겠습니다.";
                    case 2:
                        return targetText + " 쪽으로 물러나겠소.";
                    default:
                        return targetText + " 쪽으로 빠진다.";
                }

            case "help":
                if (movementType == "flank")
                {
                    switch (speechStyle)
                    {
                        case 1:
                            return targetText + " 쪽으로 돌아 붙겠습니다.";
                        case 2:
                            return targetText + " 쪽으로 돌아 붙겠소.";
                        default:
                            return targetText + " 쪽으로 돌아 붙는다.";
                    }
                }

                switch (speechStyle)
                {
                    case 1:
                        return targetText + "에게 붙겠습니다.";
                    case 2:
                        return targetText + "에게 붙겠소.";
                    default:
                        return targetText + "에게 붙는다.";
                }

            case "holdfront":
                switch (speechStyle)
                {
                    case 1:
                        return "전열을 지키겠습니다.";
                    case 2:
                        return "전열을 지키겠소.";
                    default:
                        return "전열을 지킨다.";
                }

            default:
                return string.Empty;
        }
    }

    private static string BuildEmergencyFailureLine(string dialogKind, int speechStyle)
    {
        switch (dialogKind)
        {
            case BattleCommandEmergencyFallbackDialogKind.SkillNone:
                return SelectBySpeechStyle(
                    speechStyle,
                    "나는 사용할 스킬이 없어.",
                    "저는 사용할 스킬이 없습니다.",
                    "내게는 쓸 기술이 없소."
                );

            case BattleCommandEmergencyFallbackDialogKind.SkillDisabled:
                return SelectBySpeechStyle(
                    speechStyle,
                    "지금은 스킬을 쓸 수 없어.",
                    "지금은 스킬을 사용할 수 없습니다.",
                    "지금은 기술을 쓸 수 없소."
                );

            case BattleCommandEmergencyFallbackDialogKind.SkillCooldown:
                return SelectBySpeechStyle(
                    speechStyle,
                    "스킬이 아직 준비되지 않았어.",
                    "스킬이 아직 준비되지 않았습니다.",
                    "기술이 아직 준비되지 않았소."
                );

            case BattleCommandEmergencyFallbackDialogKind.AttackNoTargetableEnemy:
                return SelectBySpeechStyle(
                    speechStyle,
                    "공격할 만한 적을 찾지 못했어.",
                    "공격할 만한 적을 찾지 못했습니다.",
                    "칠 만한 적을 찾지 못했소."
                );

            case BattleCommandEmergencyFallbackDialogKind.EscapeNoAnchor:
                return SelectBySpeechStyle(
                    speechStyle,
                    "물러날 곳을 찾지 못했어.",
                    "물러날 곳을 찾지 못했습니다.",
                    "물러날 곳을 찾지 못했소."
                );

            case BattleCommandEmergencyFallbackDialogKind.EnemySkillNoTarget:
                return SelectBySpeechStyle(
                    speechStyle,
                    "스킬을 쓸 만한 적을 찾지 못했어.",
                    "스킬을 사용할 만한 적을 찾지 못했습니다.",
                    "기술을 쓸 만한 적을 찾지 못했소."
                );

            case BattleCommandEmergencyFallbackDialogKind.AllySkillNoTarget:
                return SelectBySpeechStyle(
                    speechStyle,
                    "스킬을 쓸 만한 아군을 찾지 못했어.",
                    "스킬을 사용할 만한 아군을 찾지 못했습니다.",
                    "기술을 쓸 만한 아군을 찾지 못했소."
                );

            case BattleCommandEmergencyFallbackDialogKind.ReviveNoDeadAlly:
                return SelectBySpeechStyle(
                    speechStyle,
                    "되살릴 아군이 없어.",
                    "되살릴 아군이 없습니다.",
                    "되살릴 아군이 없소."
                );

            case BattleCommandEmergencyFallbackDialogKind.SkillControlNoSkill:
                return SelectBySpeechStyle(
                    speechStyle,
                    "조절할 스킬이 없어.",
                    "조절할 스킬이 없습니다.",
                    "조절할 기술이 없소."
                );

            default:
                return string.Empty;
        }
    }

    private static string SelectBySpeechStyle(int speechStyle, string casual, string polite, string archaic)
    {
        switch (speechStyle)
        {
            case 1:
                return polite;
            case 2:
                return archaic;
            default:
                return casual;
        }
    }

    private static int ResolveSpeechStyle(string unitId, BattleOrderRuntimeContext context)
    {
        BattleRuntimeUnit actor = BattleOrderRuntimeQueries.FindUnitById(
            context != null ? context.Allies : null,
            context != null ? context.RosterProjection : null,
            unitId
        );

        if (actor == null || actor.Snapshot == null || actor.Snapshot.Personality == null)
            return 0;

        return actor.Snapshot.Personality.speechStyle;
    }

    private static string ResolveUnitText(string unitId, string fallback, BattleOrderRuntimeContext context)
    {
        if (string.IsNullOrWhiteSpace(unitId))
            return fallback ?? string.Empty;

        string trimmedUnitId = unitId.Trim();

        BattleRuntimeUnit unit = BattleOrderRuntimeQueries.FindUnitById(
            context != null ? context.Allies : null,
            context != null ? context.RosterProjection : null,
            trimmedUnitId
        );

        if (unit == null)
        {
            unit = BattleOrderRuntimeQueries.FindUnitById(
                context != null ? context.Enemies : null,
                context != null ? context.RosterProjection : null,
                trimmedUnitId
            );
        }

        if (unit != null && !string.IsNullOrWhiteSpace(unit.DisplayName))
            return unit.DisplayName.Trim();

        return trimmedUnitId;
    }

    private static SotFinalActionDto FirstAction(SotFinalActionDto[] sequence)
    {
        if (sequence == null)
            return null;

        for (int i = 0; i < sequence.Length; i++)
        {
            if (sequence[i] != null)
                return sequence[i];
        }

        return null;
    }

    private static string WithObjectJosa(string text)
    {
        return AppendJosa(text, "을", "를");
    }

    private static string AppendJosa(string text, string withBatchim, string withoutBatchim)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return text + (HasFinalConsonant(text) ? withBatchim : withoutBatchim);
    }

    private static bool HasFinalConsonant(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        for (int i = text.Length - 1; i >= 0; i--)
        {
            char character = text[i];
            if (char.IsWhiteSpace(character))
                continue;

            const int hangulBase = 0xAC00;
            const int hangulEnd = 0xD7A3;

            if (character < hangulBase || character > hangulEnd)
                return false;

            int code = character - hangulBase;
            return code % 28 != 0;
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
