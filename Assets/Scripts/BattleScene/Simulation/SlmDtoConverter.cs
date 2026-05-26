// SOT 후처리 완료 action을 SlmUnitCommand로 변환한다.
// 이 파일은 SOT DTO와 팀원 실행 IR 사이의 유일한 어댑터다.
// DTO 스키마 변경은 이 파일에서 흡수하고, 시뮬레이션은 SlmUnitCommand만 의존한다.
// 실제 행동 로직은 SlmCommandUnitPlanner와 하위 실행계층이 처리한다.
// 여기서는 unitId 해석, enum 매핑, command sequence 생성만 수행한다.

using System;
using System.Collections.Generic;

public static class SlmDtoConverter
{
    // 후처리 결과 actor 1명의 finalActionSequence를 SlmUnitCommand sequence로 변환한다.
    // 호출부는 반환된 actor.State와 commands를 BattleSimulationManager.IssueSlmCommands에 넘긴다.
    public static bool TryConvert(
        BattleCommandFinalActorDto finalActor,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection,
        out BattleRuntimeUnit actor,
        out List<SlmUnitCommand> commands,
        out string errorReason
    )
    {
        actor = null;
        commands = null;
        errorReason = null;

        if (finalActor == null)
        {
            errorReason = "Final actor DTO is null.";
            return false;
        }

        return TryConvert(
            finalActor.unitId,
            finalActor.finalActionSequence,
            allyUnits,
            enemyUnits,
            rosterProjection,
            out actor,
            out commands,
            out errorReason
        );
    }

    // actor unitId와 확정된 SOT action sequence를 실행 IR로 변환한다.
    // actor는 반드시 아군 목록에서만 찾는다.
    public static bool TryConvert(
        string actorUnitId,
        IReadOnlyList<SotFinalActionDto> finalActionSequence,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection,
        out BattleRuntimeUnit actor,
        out List<SlmUnitCommand> commands,
        out string errorReason
    )
    {
        actor = null;
        commands = null;
        errorReason = null;

        if (string.IsNullOrWhiteSpace(actorUnitId))
        {
            errorReason = "Actor unitId is empty.";
            return false;
        }

        actor = BattleOrderRuntimeQueries.FindUnitById(allyUnits, rosterProjection, actorUnitId);
        if (actor == null)
        {
            errorReason = $"Actor unit '{actorUnitId}' was not found in ally roster.";
            return false;
        }

        if (finalActionSequence == null || finalActionSequence.Count == 0)
        {
            errorReason = $"Actor '{actorUnitId}' has no final action sequence.";
            return false;
        }

        commands = new List<SlmUnitCommand>(finalActionSequence.Count);

        for (int i = 0; i < finalActionSequence.Count; i++)
        {
            SotFinalActionDto raw = finalActionSequence[i];
            if (raw == null)
            {
                errorReason = $"Action[{i}] is null.";
                commands = null;
                return false;
            }

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
                commands = null;
                return false;
            }

            if (command.Kind == SlmCommandKind.None)
            {
                errorReason = $"Action[{i}] converted to None.";
                commands = null;
                return false;
            }

            commands.Add(command);
        }

        return true;
    }

    // SOT action 한 칸을 SlmUnitCommand 한 칸으로 변환한다.
    // target side와 runtime validity는 후처리 단계에서 이미 확정된 것으로 본다.
    private static bool TryConvertSingle(
        SotFinalActionDto raw,
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

        string type = NormalizeToken(raw.type);
        if (string.IsNullOrEmpty(type))
        {
            errorReason = "Action type is empty.";
            return false;
        }

        switch (type)
        {
            case "attack":
                return TryConvertAttack(
                    raw,
                    actor,
                    allyUnits,
                    enemyUnits,
                    rosterProjection,
                    out command,
                    out errorReason
                );

            case "move":
                return TryConvertMove(
                    raw,
                    actor,
                    allyUnits,
                    enemyUnits,
                    rosterProjection,
                    out command,
                    out errorReason
                );

            case "skill":
                return TryConvertSkill(
                    raw,
                    actor,
                    allyUnits,
                    enemyUnits,
                    rosterProjection,
                    out command,
                    out errorReason
                );

            case "wait":
                command = new SlmUnitCommand(
                    actor,
                    SlmCommandKind.Wait,
                    null,
                    SlmMoveSubtype.None,
                    SlmMoveStyle.Direct,
                    ReadDurationSec(raw)
                );
                return true;

            case "skillcontrol":
                return TryConvertSkillControl(raw, actor, out command, out errorReason);

            default:
                errorReason = $"Unknown action type '{raw.type}'.";
                return false;
        }
    }

    // attack.target을 SlmCommandKind.Attack의 Target으로 넣는다.
    private static bool TryConvertAttack(
        SotFinalActionDto raw,
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

        BattleRuntimeUnit target = FindAnyUnitById(
            raw.target,
            allyUnits,
            enemyUnits,
            rosterProjection
        );

        if (target == null)
        {
            errorReason = $"Attack target '{raw.target}' was not found.";
            return false;
        }

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

    // move.subtype, move.movementType, move.to를 SlmUnitCommand 이동 필드로 넣는다.
    // escape는 SOT target을 실행 기준점으로 쓰지 않고, 실행층에서 적 위협/적 군집 기준으로 도망친다.
    // help는 후처리/실행층에서 direct 보호 행동으로 해석한다.
    private static bool TryConvertMove(
        SotFinalActionDto raw,
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

        SlmMoveSubtype subtype = ParseMoveSubtype(raw.subtype);
        if (subtype == SlmMoveSubtype.None)
        {
            errorReason = $"Unknown move subtype '{raw.subtype}'.";
            return false;
        }

        SlmMoveStyle style = ParseMoveStyle(raw.movementType, out bool validMoveStyle);
        if (!validMoveStyle)
        {
            errorReason = $"Unknown movementType '{raw.movementType}'.";
            return false;
        }

        if (subtype == SlmMoveSubtype.Escape)
        {
            command = new SlmUnitCommand(
                actor,
                SlmCommandKind.Move,
                null,
                SlmMoveSubtype.Escape,
                SlmMoveStyle.Direct,
                0f
            );
            return true;
        }

        BattleRuntimeUnit target = FindAnyUnitById(
            raw.to,
            allyUnits,
            enemyUnits,
            rosterProjection
        );

        if (target == null)
        {
            errorReason = $"Move target '{raw.to}' was not found.";
            return false;
        }

        if (subtype == SlmMoveSubtype.Help)
            style = SlmMoveStyle.Direct;

        command = new SlmUnitCommand(actor, SlmCommandKind.Move, target, subtype, style, 0f);
        return true;
    }

    // skill.target을 SlmCommandKind.Skill의 Target으로 넣는다.
    // self skill도 target에는 actor 자신이 들어간다.
    private static bool TryConvertSkill(
        SotFinalActionDto raw,
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

        BattleRuntimeUnit target = FindAnyUnitById(
            raw.target,
            allyUnits,
            enemyUnits,
            rosterProjection
        );

        if (target == null)
        {
            errorReason = $"Skill target '{raw.target}' was not found.";
            return false;
        }

        command = new SlmUnitCommand(
            actor,
            SlmCommandKind.Skill,
            target,
            SlmMoveSubtype.None,
            SlmMoveStyle.Direct,
            0f
        );
        return true;
    }

    // skillControl.mode를 실행 IR의 분리된 command kind로 변환한다.
    // defer는 DeferSkill, forbid는 NoSkill로 매핑한다.
    private static bool TryConvertSkillControl(
        SotFinalActionDto raw,
        BattleRuntimeUnit actor,
        out SlmUnitCommand command,
        out string errorReason
    )
    {
        command = default;
        errorReason = null;

        switch (NormalizeToken(raw.mode))
        {
            case "defer":
                command = new SlmUnitCommand(
                    actor,
                    SlmCommandKind.DeferSkill,
                    null,
                    SlmMoveSubtype.None,
                    SlmMoveStyle.Direct,
                    ReadDurationSec(raw)
                );
                return true;

            case "forbid":
                command = new SlmUnitCommand(
                    actor,
                    SlmCommandKind.NoSkill,
                    null,
                    SlmMoveSubtype.None,
                    SlmMoveStyle.Direct,
                    ReadDurationSec(raw)
                );
                return true;

            default:
                errorReason = $"Unknown skillControl mode '{raw.mode}'.";
                return false;
        }
    }

    // SOT move subtype 문자열을 실행 IR enum으로 변환한다.
    private static SlmMoveSubtype ParseMoveSubtype(string subtypeToken)
    {
        switch (NormalizeToken(subtypeToken))
        {
            case "approachopponent":
                return SlmMoveSubtype.ApproachOpponent;
            case "escape":
                return SlmMoveSubtype.Escape;
            case "help":
                return SlmMoveSubtype.Help;
            case "holdfront":
                return SlmMoveSubtype.HoldFront;
            default:
                return SlmMoveSubtype.None;
        }
    }

    // movementType 누락은 direct로 본다.
    // 알 수 없는 값은 실패시킨다.
    private static SlmMoveStyle ParseMoveStyle(string movementTypeToken, out bool valid)
    {
        string token = NormalizeToken(movementTypeToken);
        if (string.IsNullOrEmpty(token) || token == "direct")
        {
            valid = true;
            return SlmMoveStyle.Direct;
        }

        if (token == "flank")
        {
            valid = true;
            return SlmMoveStyle.Flank;
        }

        valid = false;
        return SlmMoveStyle.Direct;
    }

    // durationSec가 없으면 0으로 둔다.
    // wait/noSkill/deferSkill의 최종 clamp는 planner가 한 번 더 처리한다.
    private static float ReadDurationSec(SotFinalActionDto raw)
    {
        if (raw == null || !raw.durationSec.HasValue)
            return 0f;

        return raw.durationSec.Value;
    }

    // SOT unitId를 아군/적 전체에서 BattleRuntimeUnit으로 되돌린다.
    // actor는 아군만 허용하지만 action target은 아군/적 양쪽 모두 가능하다.
    private static BattleRuntimeUnit FindAnyUnitById(
        string unitId,
        IReadOnlyList<BattleRuntimeUnit> allyUnits,
        IReadOnlyList<BattleRuntimeUnit> enemyUnits,
        IBattleRosterProjection rosterProjection
    )
    {
        BattleRuntimeUnit unit = BattleOrderRuntimeQueries.FindUnitById(
            allyUnits,
            rosterProjection,
            unitId
        );

        if (unit != null)
            return unit;

        return BattleOrderRuntimeQueries.FindUnitById(enemyUnits, rosterProjection, unitId);
    }

    // 비교용 토큰 정규화. null, 앞 뒤 공백, 대소문자 차이, switch case 중복 방지
    private static string NormalizeToken(string token)
    {
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().ToLowerInvariant();
    }
}
