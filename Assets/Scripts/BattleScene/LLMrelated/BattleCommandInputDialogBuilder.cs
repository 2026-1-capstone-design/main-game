// 후처리 결과를 대사 레이어 입력용 짧은 설명 문자열로 만든다.
// advisorLine, obeyedActionAdjustment, refusalSummary, debug summary 문장을 생성한다.
// 자연어 품질이 아니라 실행/검증용 진단 문장 생성을 담당한다.
// finalActionSequence와 SlmUnitCommand는 수정하지 않는다.

using System;
using System.Collections.Generic;
using System.Text;

public enum BattleInputDialogAdvisorReason
{
    Unknown = 0,
    ParserOutputNull = 1,
    ParserActionEmpty = 2,
    ContextInvalid = 3,
    AllActorsDropped = 4,
    InvalidInput = 5,
    FallbackToDefaultMlAi = 6,
}

public enum BattleInputDialogActorDropReason
{
    Unknown = 0,
    MissingUnitId = 1,
    ActorNotFound = 2,
    ActorDead = 3,
    ActorStunned = 4,
    ActorNotPlayerAlly = 5,
    SequenceEmpty = 6,
    SequenceInvalid = 7,
    FinalSequenceEmpty = 8,
    ConversionFailed = 9,
}

public enum BattleInputDialogAdjustmentReason
{
    None = 0,
    AttackTargetReplaced = 1,
    MoveTargetReplaced = 2,
    SkillTargetReplaced = 3,
    MovementTypeDefaulted = 4,
    SkillDescriptionCorrected = 5,
    WaitDurationClamped = 6,
    SkillControlDurationClamped = 7,
    SkillControlModeCorrected = 8,
    InvalidActionDropped = 9,
    FallbackWaitApplied = 10,
    SequenceTrimmed = 11,
    Generic = 99,
}

public enum BattleInputDialogRefusalReason
{
    Unknown = 0,
    ObedienceRollFailed = 1,
    FallbackActionBuilt = 2,
    FallbackWaitApplied = 3,
    NoFallbackAvailable = 4,
}

public enum BattleInputDialogFallbackReason
{
    Unknown = 0,
    RefusedOriginalAction = 1,
    AttackTargetInvalid = 2,
    MoveTargetInvalid = 3,
    SkillTargetInvalid = 4,
    SkillUnavailable = 5,
    NoValidPrimaryAction = 6,
    NoValidFallbackAction = 7,
}

public static class BattleCommandInputDialogBuilder
{
    public const string EmptySourceDialog = "명령을 확인했다.";

    public static string BuildAdvisorLine(BattleInputDialogAdvisorReason reason)
    {
        switch (reason)
        {
            case BattleInputDialogAdvisorReason.ParserOutputNull:
                return "parser output이 없음.";

            case BattleInputDialogAdvisorReason.ParserActionEmpty:
                return "parser action이 비어 있음.";

            case BattleInputDialogAdvisorReason.ContextInvalid:
                return "전장 context가 유효하지 않음.";

            case BattleInputDialogAdvisorReason.AllActorsDropped:
                return "최종 실행 가능한 actor가 없음.";

            case BattleInputDialogAdvisorReason.InvalidInput:
                return "입력이 유효하지 않음.";

            case BattleInputDialogAdvisorReason.FallbackToDefaultMlAi:
                return "기본 ML 행동으로 fallback.";

            default:
                return "참모 대사를 여기에.";
        }
    }

    public static string BuildSourceDialogFallback(string unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
            return EmptySourceDialog;

        return EmptySourceDialog;
    }

    public static string BuildActorDropSummary(
        string unitId,
        BattleInputDialogActorDropReason reason
    )
    {
        string actorText = NormalizeUnitIdForText(unitId);

        switch (reason)
        {
            case BattleInputDialogActorDropReason.MissingUnitId:
                return "actor unitId가 없음.";

            case BattleInputDialogActorDropReason.ActorNotFound:
                return actorText + " actor를 찾을 수 없음.";

            case BattleInputDialogActorDropReason.ActorDead:
                return actorText + " actor가 죽음.";

            case BattleInputDialogActorDropReason.ActorStunned:
                return actorText + " actor가 기절 상태.";

            case BattleInputDialogActorDropReason.ActorNotPlayerAlly:
                return actorText + " actor가 플레이어 아군이 아님.";

            case BattleInputDialogActorDropReason.SequenceEmpty:
                return actorText + " action sequence가 비어 있음.";

            case BattleInputDialogActorDropReason.SequenceInvalid:
                return actorText + " action sequence가 유효하지 않음.";

            case BattleInputDialogActorDropReason.FinalSequenceEmpty:
                return actorText + " finalActionSequence가 비어 있음.";

            case BattleInputDialogActorDropReason.ConversionFailed:
                return actorText + " SlmUnitCommand 변환 실패.";

            default:
                return actorText + " actor drop.";
        }
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        string actionCategory,
        string originalUnitId,
        string replacementUnitId
    )
    {
        string categoryText = NormalizeCategoryForText(actionCategory);
        string originalText = NormalizeUnitIdForText(originalUnitId);
        string replacementText = NormalizeUnitIdForText(replacementUnitId);

        switch (reason)
        {
            case BattleInputDialogAdjustmentReason.None:
                return string.Empty;

            case BattleInputDialogAdjustmentReason.AttackTargetReplaced:
                return "공격 대상 변경: " + originalText + " -> " + replacementText + ".";

            case BattleInputDialogAdjustmentReason.MoveTargetReplaced:
                return "이동 기준점 변경: " + originalText + " -> " + replacementText + ".";

            case BattleInputDialogAdjustmentReason.SkillTargetReplaced:
                return "스킬 대상 변경: " + originalText + " -> " + replacementText + ".";

            case BattleInputDialogAdjustmentReason.MovementTypeDefaulted:
                return "movementType이 유효하지 않아 direct로 보정.";

            case BattleInputDialogAdjustmentReason.SkillDescriptionCorrected:
                return "skill description을 actor skillDescription과 일치시킴.";

            case BattleInputDialogAdjustmentReason.WaitDurationClamped:
                return "wait durationSec를 허용 범위로 보정.";

            case BattleInputDialogAdjustmentReason.SkillControlDurationClamped:
                return "skillControl durationSec를 허용 범위로 보정.";

            case BattleInputDialogAdjustmentReason.SkillControlModeCorrected:
                return "skillControl mode가 유효하지 않아 forbid로 보정.";

            case BattleInputDialogAdjustmentReason.InvalidActionDropped:
                return categoryText + " action이 유효하지 않아 제거.";

            case BattleInputDialogAdjustmentReason.FallbackWaitApplied:
                return "유효한 fallback이 없어 wait로 보정.";

            case BattleInputDialogAdjustmentReason.SequenceTrimmed:
                return "action sequence가 최대 길이를 넘어 잘림.";

            case BattleInputDialogAdjustmentReason.Generic:
                return "순응후 보정 결과 여기에.";

            default:
                return "순응후 보정 결과 여기에.";
        }
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        SotFinalActionDto originalAction,
        SotFinalActionDto correctedAction
    )
    {
        if (reason == BattleInputDialogAdjustmentReason.None)
            return string.Empty;

        string category = ResolveActionCategory(originalAction);
        string originalTarget = ResolveActionPrimaryUnitId(originalAction);
        string correctedTarget = ResolveActionPrimaryUnitId(correctedAction);

        return BuildObeyedActionAdjustment(reason, category, originalTarget, correctedTarget);
    }

    public static string BuildCombinedObeyedActionAdjustment(IReadOnlyList<string> adjustmentLines)
    {
        return JoinNonEmpty(adjustmentLines);
    }

    public static string BuildRefusalSummary(
        BattleInputDialogRefusalReason reason,
        string originalCategory,
        string fallbackCategory
    )
    {
        string originalText = NormalizeCategoryForText(originalCategory);
        string fallbackText = NormalizeCategoryForText(fallbackCategory);

        switch (reason)
        {
            case BattleInputDialogRefusalReason.ObedienceRollFailed:
                return "명령 거부: " + originalText + " 대신 " + fallbackText + " 수행.";

            case BattleInputDialogRefusalReason.FallbackActionBuilt:
                return "유효한 fallback 선택: " + fallbackText + ".";

            case BattleInputDialogRefusalReason.FallbackWaitApplied:
                return "유효한 fallback이 없어 wait 수행.";

            case BattleInputDialogRefusalReason.NoFallbackAvailable:
                return "유효한 fallback이 없음.";

            default:
                return "거부 요약 여기에.";
        }
    }

    public static string BuildFallbackSummary(
        BattleInputDialogFallbackReason reason,
        string originalCategory,
        string fallbackCategory,
        string fallbackTargetUnitId
    )
    {
        string originalText = NormalizeCategoryForText(originalCategory);
        string fallbackText = NormalizeCategoryForText(fallbackCategory);
        string targetText = NormalizeUnitIdForText(fallbackTargetUnitId);

        switch (reason)
        {
            case BattleInputDialogFallbackReason.RefusedOriginalAction:
                return "원래 " + originalText + " 명령을 거부하여 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.AttackTargetInvalid:
                return "공격 대상이 유효하지 않아 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.MoveTargetInvalid:
                return "이동 기준점이 유효하지 않아 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.SkillTargetInvalid:
                return "스킬 대상이 유효하지 않아 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.SkillUnavailable:
                return "스킬을 사용할 수 없어 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.NoValidPrimaryAction:
                return "유효한 주요 action이 없어 fallback은 " + fallbackText + "다.";

            case BattleInputDialogFallbackReason.NoValidFallbackAction:
                return "유효한 fallback action이 없어 wait 수행.";

            default:
                if (!string.IsNullOrWhiteSpace(fallbackTargetUnitId))
                    return "fallback은 " + fallbackText + "이고 대상은 " + targetText + "다.";

                return "fallback은 " + fallbackText + "다.";
        }
    }

    public static string BuildTargetInvalidSummary(string actionCategory, string targetUnitId)
    {
        return NormalizeCategoryForText(actionCategory)
            + " 대상이 유효하지 않음: "
            + NormalizeUnitIdForText(targetUnitId)
            + ".";
    }

    public static string BuildTargetDeadSummary(string targetUnitId)
    {
        return "타겟이 죽음: " + NormalizeUnitIdForText(targetUnitId) + ".";
    }

    public static string BuildNoValidTargetSummary(string actionCategory)
    {
        return "유효한 " + NormalizeCategoryForText(actionCategory) + " 대상이 없음.";
    }

    public static string BuildDurationClampSummary(string actionCategory, float before, float after)
    {
        return NormalizeCategoryForText(actionCategory)
            + " durationSec 보정: "
            + before.ToString("0.##")
            + " -> "
            + after.ToString("0.##")
            + ".";
    }

    public static string ResolveActionCategory(SotFinalActionDto action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.type))
            return "unknown";

        string type = NormalizeToken(action.type);

        if (type == "move")
        {
            string subtype = NormalizeToken(action.subtype);
            return string.IsNullOrWhiteSpace(subtype) ? "move" : subtype;
        }

        if (type == "skillcontrol")
            return "skillControl";

        return action.type;
    }

    public static string ResolveActionPrimaryUnitId(SotFinalActionDto action)
    {
        if (action == null)
            return string.Empty;

        string type = NormalizeToken(action.type);

        if (type == "move")
            return action.to ?? string.Empty;

        if (type == "attack" || type == "skill")
            return action.target ?? string.Empty;

        return string.Empty;
    }

    public static string NormalizeCategoryForText(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "unknown";

        string token = NormalizeToken(category);

        switch (token)
        {
            case "approachopponent":
                return "approachOpponent";
            case "escape":
                return "escape";
            case "help":
                return "help";
            case "holdfront":
                return "holdFront";
            case "attack":
                return "attack";
            case "wait":
                return "wait";
            case "skill":
                return "skill";
            case "skillcontrol":
                return "skillControl";
            case "deferskill":
                return "deferSkill";
            case "noskill":
                return "noSkill";
            default:
                return category.Trim();
        }
    }

    public static string NormalizeUnitIdForText(string unitId)
    {
        return string.IsNullOrWhiteSpace(unitId) ? "unknown" : unitId.Trim();
    }

    public static string JoinNonEmpty(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder(128);

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sb.Length > 0)
                sb.Append(" ");

            sb.Append(line.Trim());
        }

        return sb.ToString();
    }

    private static string NormalizeToken(string token)
    {
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
    }
}
