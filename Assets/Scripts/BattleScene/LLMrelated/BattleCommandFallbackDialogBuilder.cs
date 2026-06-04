// 최종 action sequence에서 하드코딩된 유닛 대사를 만든다.
// 대사 SLM/LLM 실패 또는 최후방 폴백 성공 시 사용한다.
// speechStyle 0, 1, 2만 반영하고 성격 설명은 사용하지 않는다.
// action은 수정하지 않고 unitId와 text만 만든다.
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
                return BuildAttackLine(action.target, speechStyle);

            case "skill":
                return BuildSkillLine(action.target, actor.unitId, speechStyle);

            case "wait":
                return BuildWaitLine(action.durationSec.HasValue ? action.durationSec.Value : 1f, speechStyle);

            case "skillcontrol":
                return BuildSkillControlLine(action.mode, speechStyle);

            case "move":
                return BuildMoveLine(action, speechStyle);

            default:
                return string.Empty;
        }
    }

    private static string BuildAttackLine(string target, int speechStyle)
    {
        string targetText = NormalizeUnitText(target, "적");

        switch (speechStyle)
        {
            case 1:
                return targetText + "를 공격하겠습니다.";
            case 2:
                return targetText + "를 치겠소.";
            default:
                return targetText + "를 공격하겠다.";
        }
    }

    private static string BuildSkillLine(string target, string actorUnitId, int speechStyle)
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

        string targetText = NormalizeUnitText(target, "상대");
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

    private static string BuildMoveLine(SotFinalActionDto action, int speechStyle)
    {
        string subtype = Normalize(action.subtype);
        string movementType = Normalize(action.movementType);
        string targetText = NormalizeUnitText(action.to, string.Empty);

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

    private static string NormalizeUnitText(string unitId, string fallback)
    {
        return string.IsNullOrWhiteSpace(unitId) ? fallback : unitId.Trim();
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
