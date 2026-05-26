// 후처리 결과를 대사 레이어 입력용 자연어 설명 문자열로 만든다.
// advisorLine, obeyedActionAdjustment, refusalSummary, debug summary 문장을 생성한다.
// 대사 SLM이 내부 용어를 복사하지 않도록 실행 의미 중심 문장을 만든다.
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

public enum BattleInputDialogTargetIssue
{
    Unknown = 0,
    TargetDead = 1,
    TargetNotFoundOrUntargetable = 2,
    NoValidTarget = 3,
    SkillTargetMismatch = 4,
}

public static class BattleCommandInputDialogBuilder
{
    public const string EmptySourceDialog = "명령을 확인했다.";

    public static string BuildAdvisorLine(BattleInputDialogAdvisorReason reason)
    {
        switch (reason)
        {
            case BattleInputDialogAdvisorReason.ParserOutputNull:
                return "우리 애들이 방금 명령을 못 들은 모양입니다!";

            case BattleInputDialogAdvisorReason.ParserActionEmpty:
                return "단장님! 지금은 그걸 할 수 없잖아요!";

            case BattleInputDialogAdvisorReason.ContextInvalid:
                return "전장 상황을 확인하지 못했습니다.";

            case BattleInputDialogAdvisorReason.AllActorsDropped:
                return "지금 명령을 수행할 수 있는 아군이 없습니다.";

            case BattleInputDialogAdvisorReason.InvalidInput:
                return "명령을 제대로 알아듣지 못했습니다.";

            case BattleInputDialogAdvisorReason.FallbackToDefaultMlAi:
                return "지금은 각자 판단에 맡기겠습니다.";

            default:
                return "지금은 명령을 처리하기 어렵습니다.";
        }
    }

    public static string BuildSourceDialogFallback(string unitId)
    {
        return EmptySourceDialog;
    }

    public static string BuildActorDropSummary(string unitId, BattleInputDialogActorDropReason reason)
    {
        string actorText = NormalizeUnitIdForText(unitId);

        switch (reason)
        {
            case BattleInputDialogActorDropReason.MissingUnitId:
                return "명령을 받을 유닛을 확인하지 못했다.";

            case BattleInputDialogActorDropReason.ActorNotFound:
                return actorText + "를 찾지 못했다.";

            case BattleInputDialogActorDropReason.ActorDead:
                return actorText + "는 이미 전투 불가 상태다.";

            case BattleInputDialogActorDropReason.ActorStunned:
                return actorText + "는 지금 움직일 수 없다.";

            case BattleInputDialogActorDropReason.ActorNotPlayerAlly:
                return actorText + "는 명령 가능한 아군이 아니다.";

            case BattleInputDialogActorDropReason.SequenceEmpty:
                return actorText + "에게 실행할 행동이 없다.";

            case BattleInputDialogActorDropReason.SequenceInvalid:
                return actorText + "의 행동을 실행하기 어렵다.";

            case BattleInputDialogActorDropReason.FinalSequenceEmpty:
                return actorText + "가 수행할 수 있는 행동이 없다.";

            case BattleInputDialogActorDropReason.ConversionFailed:
                return actorText + "의 행동을 실행 명령으로 바꾸지 못했다.";

            default:
                return actorText + "의 명령을 처리하지 못했다.";
        }
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        string actionCategory,
        string originalUnitId,
        string replacementUnitId
    )
    {
        return BuildObeyedActionAdjustment(
            reason,
            actionCategory,
            originalUnitId,
            replacementUnitId,
            BattleInputDialogTargetIssue.Unknown,
            string.Empty
        );
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        string actionCategory,
        string originalUnitId,
        string replacementUnitId,
        BattleInputDialogTargetIssue targetIssue
    )
    {
        return BuildObeyedActionAdjustment(
            reason,
            actionCategory,
            originalUnitId,
            replacementUnitId,
            targetIssue,
            string.Empty
        );
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        string actionCategory,
        string originalUnitId,
        string replacementUnitId,
        BattleInputDialogTargetIssue targetIssue,
        string actorUnitId
    )
    {
        string categoryText = NormalizeCategoryForText(actionCategory);
        string originalText = NormalizeUnitIdForText(originalUnitId);
        string replacementText = NormalizeUnitIdForText(replacementUnitId);
        string finalActionText = BuildFinalActionText(categoryText, replacementUnitId, actorUnitId);

        switch (reason)
        {
            case BattleInputDialogAdjustmentReason.None:
                return string.Empty;

            case BattleInputDialogAdjustmentReason.AttackTargetReplaced:
            case BattleInputDialogAdjustmentReason.MoveTargetReplaced:
            case BattleInputDialogAdjustmentReason.SkillTargetReplaced:
                return BuildTargetReplacementText(originalText, finalActionText, targetIssue);

            case BattleInputDialogAdjustmentReason.MovementTypeDefaulted:
                return "이동 방식이 맞지 않아 직접 이동한다.";

            case BattleInputDialogAdjustmentReason.SkillDescriptionCorrected:
                return "스킬 내용이 실제 보유 스킬과 달라 보유한 스킬을 사용한다.";

            case BattleInputDialogAdjustmentReason.WaitDurationClamped:
                return "대기 시간이 허용 범위를 벗어나 조정된 시간만 대기한다.";

            case BattleInputDialogAdjustmentReason.SkillControlDurationClamped:
                return "스킬을 미루는 시간이 허용 범위를 벗어나 조정된 시간만 미룬다.";

            case BattleInputDialogAdjustmentReason.SkillControlModeCorrected:
                return "스킬 통제 방식이 맞지 않아 스킬을 쓰지 않는다.";

            case BattleInputDialogAdjustmentReason.InvalidActionDropped:
                return BuildActionNoun(categoryText) + " 행동은 지금 실행하기 어려워 제외한다.";

            case BattleInputDialogAdjustmentReason.FallbackWaitApplied:
                return "실행할 행동을 찾지 못해 잠시 대기한다.";

            case BattleInputDialogAdjustmentReason.SequenceTrimmed:
                return "행동이 너무 많아 앞의 가능한 행동만 수행한다.";

            case BattleInputDialogAdjustmentReason.Generic:
                return string.IsNullOrWhiteSpace(finalActionText)
                    ? "명령을 현재 전장 상황에 맞게 조정한다."
                    : "현재 전장 상황에 맞춰 " + finalActionText;

            default:
                return string.IsNullOrWhiteSpace(finalActionText)
                    ? "명령을 현재 전장 상황에 맞게 조정한다."
                    : "현재 전장 상황에 맞춰 " + finalActionText;
        }
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        SotFinalActionDto originalAction,
        SotFinalActionDto correctedAction
    )
    {
        return BuildObeyedActionAdjustment(
            reason,
            originalAction,
            correctedAction,
            BattleInputDialogTargetIssue.Unknown,
            string.Empty
        );
    }

    public static string BuildObeyedActionAdjustment(
        BattleInputDialogAdjustmentReason reason,
        SotFinalActionDto originalAction,
        SotFinalActionDto correctedAction,
        BattleInputDialogTargetIssue targetIssue,
        string actorUnitId
    )
    {
        if (reason == BattleInputDialogAdjustmentReason.None)
            return string.Empty;

        string category = ResolveActionCategory(correctedAction ?? originalAction);
        string originalTarget = ResolveActionPrimaryUnitId(originalAction);
        string correctedTarget = ResolveActionPrimaryUnitId(correctedAction);

        return BuildObeyedActionAdjustment(reason, category, originalTarget, correctedTarget, targetIssue, actorUnitId);
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
        return BuildRefusalSummary(reason, originalCategory, fallbackCategory, null, string.Empty);
    }

    public static string BuildRefusalSummary(
        BattleInputDialogRefusalReason reason,
        string originalCategory,
        string fallbackCategory,
        SotFinalActionDto[] finalActionSequence,
        string actorUnitId
    )
    {
        string originalText = NormalizeCategoryForText(originalCategory);
        string fallbackText = NormalizeCategoryForText(fallbackCategory);

        switch (reason)
        {
            case BattleInputDialogRefusalReason.FallbackWaitApplied:
                return BuildRefusalPrefix(originalText) + "잠시 대기한다.";

            case BattleInputDialogRefusalReason.NoFallbackAvailable:
                return BuildRefusalPrefix(originalText) + "지금은 다른 행동도 고르지 못한다.";

            case BattleInputDialogRefusalReason.FallbackActionBuilt:
            case BattleInputDialogRefusalReason.ObedienceRollFailed:
            default:
                string finalText = BuildFinalSequenceText(finalActionSequence, fallbackText, actorUnitId);
                return BuildRefusalPrefix(originalText) + finalText;
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
        string finalText = BuildFinalActionText(fallbackText, fallbackTargetUnitId, string.Empty);

        switch (reason)
        {
            case BattleInputDialogFallbackReason.RefusedOriginalAction:
                return BuildRefusalPrefix(originalText) + finalText;

            case BattleInputDialogFallbackReason.AttackTargetInvalid:
            case BattleInputDialogFallbackReason.MoveTargetInvalid:
            case BattleInputDialogFallbackReason.SkillTargetInvalid:
                return BuildTargetReplacementText(
                    targetText,
                    finalText,
                    BattleInputDialogTargetIssue.TargetNotFoundOrUntargetable
                );

            case BattleInputDialogFallbackReason.SkillUnavailable:
                return "지금은 스킬을 쓸 수 없어 " + finalText;

            case BattleInputDialogFallbackReason.NoValidPrimaryAction:
                return "원래 행동을 실행하기 어려워 " + finalText;

            case BattleInputDialogFallbackReason.NoValidFallbackAction:
                return "실행할 행동을 찾지 못해 잠시 대기한다.";

            default:
                return finalText;
        }
    }

    public static string BuildTargetInvalidSummary(string actionCategory, string targetUnitId)
    {
        return NormalizeUnitIdForText(targetUnitId)
            + "를 찾지 못해 "
            + BuildActionNoun(actionCategory)
            + " 행동을 조정한다.";
    }

    public static string BuildTargetDeadSummary(string targetUnitId)
    {
        return NormalizeUnitIdForText(targetUnitId) + "는 이미 전투 불가 상태다.";
    }

    public static string BuildNoValidTargetSummary(string actionCategory)
    {
        return BuildActionNoun(actionCategory) + "할 대상을 찾지 못했다.";
    }

    public static string BuildDurationClampSummary(string actionCategory, float before, float after)
    {
        string category = NormalizeCategoryForText(actionCategory);
        string afterText = FormatSeconds(after);

        if (category == "wait")
            return "대기 시간이 허용 범위를 벗어나 " + afterText + "만 대기한다.";

        if (category == "skillControl")
            return "스킬을 미루는 시간이 허용 범위를 벗어나 " + afterText + "만 미룬다.";

        return "시간이 허용 범위를 벗어나 " + afterText + "로 조정한다.";
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

    private static string BuildTargetReplacementText(
        string originalText,
        string finalActionText,
        BattleInputDialogTargetIssue targetIssue
    )
    {
        if (string.IsNullOrWhiteSpace(finalActionText))
            finalActionText = "다른 행동을 수행한다.";

        switch (targetIssue)
        {
            case BattleInputDialogTargetIssue.TargetDead:
                return originalText + "는 이미 전투 불가 상태라 " + finalActionText;

            case BattleInputDialogTargetIssue.SkillTargetMismatch:
                return "스킬 대상이 맞지 않아 " + finalActionText;

            case BattleInputDialogTargetIssue.NoValidTarget:
                return "맞는 대상을 찾지 못해 " + finalActionText;

            case BattleInputDialogTargetIssue.TargetNotFoundOrUntargetable:
            case BattleInputDialogTargetIssue.Unknown:
            default:
                if (originalText == "unknown")
                    return "대상을 찾지 못해 " + finalActionText;

                return originalText + "를 찾지 못해 " + finalActionText;
        }
    }

    private static string BuildRefusalPrefix(string originalCategory)
    {
        switch (NormalizeCategoryForText(originalCategory))
        {
            case "attack":
                return "원래 공격 명령이었지만, 지금은 공격보다는 ";

            case "wait":
                return "원래 대기 명령이었지만, 기다리지 않고 ";

            case "escape":
                return "원래 후퇴 명령이었지만, 물러서지 않고 ";

            case "approachOpponent":
                return "원래 접근 명령이었지만, 무리해서 붙기보다 ";

            case "help":
                return "원래 지원 명령이었지만, 지금은 지원보다는 ";

            case "holdFront":
                return "원래 전열 유지 명령이었지만, 버티기보다 ";

            case "skill":
                return "원래 스킬 명령이었지만, 스킬보다는 ";

            case "skillControl":
                return "원래 스킬을 아끼라는 명령이었지만, 지금은 ";

            default:
                return "원래 명령과 달리 ";
        }
    }

    private static string BuildFinalSequenceText(
        SotFinalActionDto[] finalActionSequence,
        string fallbackCategory,
        string actorUnitId
    )
    {
        if (finalActionSequence != null)
        {
            for (int i = 0; i < finalActionSequence.Length; i++)
            {
                SotFinalActionDto action = finalActionSequence[i];
                if (action == null)
                    continue;

                string actionText = BuildFinalActionText(action, actorUnitId);
                if (!string.IsNullOrWhiteSpace(actionText))
                    return actionText;
            }
        }

        return BuildFinalActionText(fallbackCategory, string.Empty, actorUnitId);
    }

    private static string BuildFinalActionText(SotFinalActionDto action, string actorUnitId)
    {
        if (action == null)
            return string.Empty;

        string type = NormalizeToken(action.type);

        switch (type)
        {
            case "attack":
                return BuildFinalActionText("attack", action.target, actorUnitId);

            case "skill":
                return BuildFinalActionText("skill", action.target, actorUnitId);

            case "wait":
                return action.durationSec.HasValue
                    ? FormatSeconds(action.durationSec.Value) + " 대기한다."
                    : "잠시 대기한다.";

            case "skillcontrol":
                if (NormalizeToken(action.mode) == "defer")
                {
                    return action.durationSec.HasValue
                        ? "스킬 사용을 " + FormatSeconds(action.durationSec.Value) + " 미룬다."
                        : "스킬 사용을 잠시 미룬다.";
                }

                return "스킬을 쓰지 않는다.";

            case "move":
                return BuildFinalActionText(ResolveActionCategory(action), action.to, actorUnitId);

            default:
                return string.Empty;
        }
    }

    private static string BuildFinalActionText(string category, string targetUnitId, string actorUnitId)
    {
        string normalizedCategory = NormalizeCategoryForText(category);
        string targetText = NormalizeUnitIdForText(targetUnitId);
        bool hasTarget = !string.IsNullOrWhiteSpace(targetUnitId);
        bool isSelfTarget =
            hasTarget
            && !string.IsNullOrWhiteSpace(actorUnitId)
            && string.Equals(targetUnitId.Trim(), actorUnitId.Trim(), StringComparison.OrdinalIgnoreCase);

        switch (normalizedCategory)
        {
            case "attack":
                return hasTarget ? targetText + "을 공격한다." : "가까운 적을 공격한다.";

            case "approachOpponent":
                return hasTarget ? targetText + "에게 접근한다." : "가까운 적에게 접근한다.";

            case "escape":
                return hasTarget ? targetText + " 쪽으로 물러난다." : "위험에서 벗어난다.";

            case "help":
                return hasTarget
                    ? "도움이 필요한 " + targetText + "을 도와 주변 적을 공격한다."
                    : "도움이 필요한 아군을 도와 주변 적을 공격한다.";

            case "holdFront":
                return isSelfTarget ? "내 자리를 지킨다." : "전열을 지킨다.";

            case "skill":
                if (isSelfTarget)
                    return "자신에게 스킬을 쓴다.";

                return hasTarget ? targetText + "에게 스킬을 쓴다." : "스킬을 쓴다.";

            case "wait":
                return "잠시 대기한다.";

            case "skillControl":
            case "noSkill":
                return "스킬을 쓰지 않는다.";

            case "deferSkill":
                return "스킬 사용을 잠시 미룬다.";

            default:
                return "다른 행동을 수행한다.";
        }
    }

    private static string BuildActionNoun(string category)
    {
        switch (NormalizeCategoryForText(category))
        {
            case "attack":
                return "공격";
            case "approachOpponent":
                return "접근";
            case "escape":
                return "후퇴";
            case "help":
                return "지원";
            case "holdFront":
                return "전열 유지";
            case "skill":
                return "스킬 사용";
            case "wait":
                return "대기";
            case "skillControl":
            case "deferSkill":
            case "noSkill":
                return "스킬 통제";
            default:
                return "명령";
        }
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.##") + "초";
    }

    private static string NormalizeToken(string token)
    {
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
    }
}
