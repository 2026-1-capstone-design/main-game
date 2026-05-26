using System.Collections.Generic;
using UnityEngine;

// SLM 응답 DTO를 시뮬레이션 IR(SlmUnitCommand)로 변환한다.
// SLM 측 스키마 변경에 노출되는 유일한 어댑터이다.
public static class SlmDtoConverter
{
    public static bool TryConvert(
        BattleLlmResponseDto dto,
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection,
        out List<SlmUnitCommand> commands,
        out string errorReason
    )
    {
        commands = null;
        errorReason = null;

        if (dto == null || dto.output == null)
        {
            errorReason = "Response DTO or output is null.";
            return false;
        }

        if (actor == null)
        {
            errorReason = "Actor unit is null.";
            return false;
        }

        BattleLlmResponseActionDto[] rawActions = dto.output.action;
        if (rawActions == null || rawActions.Length == 0)
        {
            // 빈 action은 거부 대사로 해석되므로 명령 없음을 정상 응답으로 처리한다.
            commands = new List<SlmUnitCommand>(0);
            return true;
        }

        commands = new List<SlmUnitCommand>(rawActions.Length);

        for (int i = 0; i < rawActions.Length; i++)
        {
            BattleLlmResponseActionDto raw = rawActions[i];
            if (raw == null)
                continue;

            if (
                !TryConvertSingle(
                    raw,
                    actor,
                    allyUnits,
                    enemyUnits,
                    rosterProjection,
                    out SlmUnitCommand command,
                    out string singleError
                )
            )
            {
                errorReason = $"Action[{i}] conversion failed. {singleError}";
                return false;
            }

            if (command.Kind == SlmCommandKind.None)
                continue;

            commands.Add(command);
        }

        return true;
    }

    private static bool TryConvertSingle(
        BattleLlmResponseActionDto raw,
        BattleRuntimeUnit actor,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection,
        out SlmUnitCommand command,
        out string errorReason
    )
    {
        command = default;
        errorReason = null;

        SlmCommandKind kind = ParseKind(raw.type);
        if (kind == SlmCommandKind.None)
        {
            errorReason = $"Unknown action type '{raw.type}'.";
            return false;
        }

        if (kind == SlmCommandKind.Attack)
        {
            BattleRuntimeUnit target = FindUnitById(raw.targetUnitId, allyUnits, enemyUnits, rosterProjection);
            command = new SlmUnitCommand(
                actor,
                SlmCommandKind.Attack,
                target,
                SlmMoveSubtype.None,
                SlmMoveStyle.Direct,
                0f
            );
            return true;
        }

        if (kind == SlmCommandKind.Move)
        {
            SlmMoveSubtype subtype = ParseMoveSubtype(raw.subtype);
            // DTO에 movementType 필드가 없어 기본값(Direct)으로 둔다.
            SlmMoveStyle style = SlmMoveStyle.Direct;

            // targetUnitId가 비어있으면 relativeFromUnitId를 fallback으로 사용한다.
            string targetId = string.IsNullOrWhiteSpace(raw.targetUnitId) ? raw.relativeFromUnitId : raw.targetUnitId;
            BattleRuntimeUnit moveTarget = FindUnitById(targetId, allyUnits, enemyUnits, rosterProjection);

            command = new SlmUnitCommand(actor, SlmCommandKind.Move, moveTarget, subtype, style, 0f);
            return true;
        }

        if (kind == SlmCommandKind.Skill)
        {
            BattleRuntimeUnit skillTarget = FindUnitById(raw.targetUnitId, allyUnits, enemyUnits, rosterProjection);
            command = new SlmUnitCommand(
                actor,
                SlmCommandKind.Skill,
                skillTarget,
                SlmMoveSubtype.None,
                SlmMoveStyle.Direct,
                0f
            );
            return true;
        }

        if (kind == SlmCommandKind.Wait || kind == SlmCommandKind.DeferSkill)
        {
            // deferSkill은 명세에서 제외돼 Planner에서 즉시 종료된다.
            // DTO에 sec 필드가 없어 DurationSec=0으로 두고, Planner가 디폴트 값으로 처리한다.
            command = new SlmUnitCommand(actor, kind, null, SlmMoveSubtype.None, SlmMoveStyle.Direct, 0f);
            return true;
        }

        if (kind == SlmCommandKind.NoSkill)
        {
            command = new SlmUnitCommand(
                actor,
                SlmCommandKind.NoSkill,
                null,
                SlmMoveSubtype.None,
                SlmMoveStyle.Direct,
                0f
            );
            return true;
        }

        errorReason = $"Unhandled action kind '{kind}'.";
        return false;
    }

    private static SlmCommandKind ParseKind(string typeToken)
    {
        if (string.IsNullOrWhiteSpace(typeToken))
            return SlmCommandKind.None;

        switch (typeToken.Trim().ToLowerInvariant())
        {
            case "attack":
                return SlmCommandKind.Attack;
            case "move":
                return SlmCommandKind.Move;
            case "skill":
                return SlmCommandKind.Skill;
            case "wait":
                return SlmCommandKind.Wait;
            case "deferskill":
                return SlmCommandKind.DeferSkill;
            case "noskill":
                return SlmCommandKind.NoSkill;
            default:
                return SlmCommandKind.None;
        }
    }

    private static SlmMoveSubtype ParseMoveSubtype(string subtypeToken)
    {
        if (string.IsNullOrWhiteSpace(subtypeToken))
            return SlmMoveSubtype.None;

        switch (subtypeToken.Trim().ToLowerInvariant())
        {
            case "approachopponent":
                return SlmMoveSubtype.ApproachOpponent;
            case "escape":
                return SlmMoveSubtype.Escape;
            case "help":
                return SlmMoveSubtype.Help;
            case "holdfront":
                return SlmMoveSubtype.HoldFront;

            // 구 DTO(absolute/relative) fallback. 갱신된 스키마에서는 도달하지 않는다.
            case "absolute":
            case "relative":
                return SlmMoveSubtype.ApproachOpponent;

            default:
                return SlmMoveSubtype.None;
        }
    }

    private static BattleRuntimeUnit FindUnitById(
        string unitId,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection
    )
    {
        if (string.IsNullOrWhiteSpace(unitId) || rosterProjection == null)
            return null;

        for (int i = 0; i < allyUnits.Count; i++)
        {
            BattleRuntimeUnit unit = allyUnits[i];
            if (
                unit != null
                && string.Equals(rosterProjection.GetDisplayUnitId(unit), unitId, System.StringComparison.Ordinal)
            )
                return unit;
        }

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            BattleRuntimeUnit unit = enemyUnits[i];
            if (
                unit != null
                && string.Equals(rosterProjection.GetDisplayUnitId(unit), unitId, System.StringComparison.Ordinal)
            )
                return unit;
        }

        return null;
    }
}
