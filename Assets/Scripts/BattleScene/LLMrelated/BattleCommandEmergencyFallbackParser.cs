// 원본 한글 명령에서 최후방 단일 행동 폴백을 만든다.
// keyword와 unitId는 fuzzy 보정 없이 exact match만 사용한다.
// skill target이 생략되면 스킬 메타데이터와 전장 상태로 기본 target을 고른다.
// 일부 상태성 실패는 행동 없는 유닛 대사로 변환한다.
// 이 파일은 전장 상태를 읽기만 하며 실행 명령을 직접 발행하지 않는다.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class BattleCommandEmergencyFallbackDialogKind
{
    public const string SkillNone = "skill_none";
    public const string SkillDisabled = "skill_disabled";
    public const string SkillCooldown = "skill_cooldown";
    public const string AttackNoTargetableEnemy = "attack_no_targetable_enemy";
    public const string EscapeNoAnchor = "escape_no_anchor";
    public const string EnemySkillNoTarget = "enemy_skill_no_target";
    public const string AllySkillNoTarget = "ally_skill_no_target";
    public const string ReviveNoDeadAlly = "revive_no_dead_ally";
    public const string SkillControlNoSkill = "skill_control_no_skill";
}

public static class BattleCommandEmergencyFallbackParser
{
    public const string AdvisorLine = "잘 못들었나본데요?";

    private static readonly Regex UnitIdRegex = new Regex("[AaEe]_[0-9]{2}", RegexOptions.Compiled);
    private static readonly Regex IntRegex = new Regex(
        "(?<![0-9A-Za-z_])-?[0-9]+(?![0-9A-Za-z_])",
        RegexOptions.Compiled
    );
    private static readonly Regex StandaloneBingRegex = new Regex(
        "(?<![0-9A-Za-z가-힣_])빙(?![0-9A-Za-z가-힣_])",
        RegexOptions.Compiled
    );

    public static bool TryParse(
        string rawCommand,
        BattleOrderRuntimeContext context,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (context == null)
        {
            debugLog = "emergency fallback failed: context is null.";
            return false;
        }

        string command = rawCommand ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            debugLog = "emergency fallback failed: command is empty.";
            return false;
        }

        EmergencyCommandKind commandKind = DetermineCommandKind(command);
        if (commandKind == EmergencyCommandKind.Unknown)
        {
            debugLog = "emergency fallback failed: no supported Korean action keyword.";
            return false;
        }

        List<string> unitIds = ExtractUnitIds(command);
        List<int> integers = ExtractIntegers(command);
        EmergencyUnitContext unitContext = new EmergencyUnitContext(context, unitIds);

        if (unitContext.HasUnknownUnit)
        {
            debugLog = "emergency fallback failed: unknown unit id exists.";
            return false;
        }

        bool parsed;
        switch (commandKind)
        {
            case EmergencyCommandKind.OrbitMove:
                parsed = TryParseOrbitMove(command, context, unitContext, out result, out debugLog);
                break;

            case EmergencyCommandKind.Skill:
                parsed = TryParseSkill(context, unitContext, out result, out debugLog);
                break;

            case EmergencyCommandKind.Attack:
                parsed = TryParseAttack(context, unitContext, out result, out debugLog);
                break;

            case EmergencyCommandKind.Escape:
                parsed = TryParseEscape(context, unitContext, out result, out debugLog);
                break;

            case EmergencyCommandKind.GenericMove:
                parsed = TryParseGenericMove(context, unitContext, out result, out debugLog);
                break;

            case EmergencyCommandKind.Wait:
                parsed = TryParseWait(unitContext, integers, out result, out debugLog);
                break;

            case EmergencyCommandKind.SkillControl:
                parsed = TryParseSkillControl(command, context, unitContext, integers, out result, out debugLog);
                break;

            default:
                debugLog = "emergency fallback failed: unsupported command kind.";
                parsed = false;
                break;
        }

        if (!parsed)
            return false;

        if (result == null || string.IsNullOrWhiteSpace(result.actorUnitId) || result.finalActionSequence == null)
        {
            debugLog = "emergency fallback failed: parse result is invalid.";
            result = null;
            return false;
        }

        return true;
    }

    private static EmergencyCommandKind DetermineCommandKind(string command)
    {
        if (ContainsOrbitKeyword(command))
            return EmergencyCommandKind.OrbitMove;

        if (
            ContainsAny(command, "스킬", "기술", "필살기", "스킬 써", "기술 써", "능력")
            && !ContainsSkillControlKeyword(command)
        )
        {
            return EmergencyCommandKind.Skill;
        }

        if (
            ContainsAny(
                command,
                "공격",
                "공격하",
                "때려",
                "때리",
                "쳐",
                "죽여",
                "잡아",
                "처리",
                "물어",
                "처치",
                "제압",
                "쓰러뜨려",
                "조져",
                "패라",
                "끝내",
                "갈겨"
            )
        )
        {
            return EmergencyCommandKind.Attack;
        }

        if (
            ContainsAny(
                command,
                "도망",
                "도망쳐",
                "빠져",
                "후퇴",
                "퇴각",
                "빼",
                "물러나",
                "물러서",
                "피해",
                "벗어나",
                "거리 벌려",
                "안전"
            )
        )
        {
            return EmergencyCommandKind.Escape;
        }

        if (
            ContainsAny(
                command,
                "이동",
                "이동해",
                "걸어가",
                "쪽으로",
                "붙어",
                "접근",
                "도와",
                "도와줘",
                "지원",
                "커버",
                "엄호",
                "합류",
                "다가가",
                "쫓아가"
            )
        )
        {
            return EmergencyCommandKind.GenericMove;
        }

        if (ContainsAny(command, "대기", "대기해", "기다", "멈춰", "잠깐", "정지", "가만히"))
            return EmergencyCommandKind.Wait;

        if (ContainsSkillControlKeyword(command))
            return EmergencyCommandKind.SkillControl;

        return EmergencyCommandKind.Unknown;
    }

    private static bool ContainsOrbitKeyword(string command)
    {
        return ContainsAny(command, "돌아", "우회", "측면", "뒤에서", "빙 돌아")
            || StandaloneBingRegex.IsMatch(command);
    }

    private static bool ContainsSkillControlKeyword(string command)
    {
        return ContainsExact(command, "스킬 아껴")
            || ContainsExact(command, "기술 아껴")
            || ContainsExact(command, "스킬 쓰지")
            || ContainsExact(command, "스킬 쓰지마")
            || ContainsExact(command, "기술 쓰지마")
            || ContainsExact(command, "금지")
            || ContainsExact(command, "미뤄")
            || ContainsExact(command, "아껴둬")
            || ContainsExact(command, "보류")
            || ContainsExact(command, "나중에");
    }

    private static bool ContainsExact(string command, string keyword)
    {
        return !string.IsNullOrEmpty(command)
            && !string.IsNullOrEmpty(keyword)
            && command.IndexOf(keyword, StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsAny(string command, params string[] keywords)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (ContainsExact(command, keywords[i]))
                return true;
        }

        return false;
    }

    private static List<string> ExtractUnitIds(string command)
    {
        List<string> result = new List<string>();
        MatchCollection matches = UnitIdRegex.Matches(command ?? string.Empty);

        for (int i = 0; i < matches.Count; i++)
        {
            string unitId = matches[i].Value.ToUpperInvariant();
            result.Add(unitId);
        }

        return result;
    }

    private static List<int> ExtractIntegers(string command)
    {
        List<int> result = new List<int>();
        MatchCollection matches = IntRegex.Matches(command ?? string.Empty);

        for (int i = 0; i < matches.Count; i++)
        {
            if (int.TryParse(matches[i].Value, out int value))
                result.Add(value);
        }

        return result;
    }

    private static bool BuildDialogOnlyResult(
        BattleRuntimeUnit actor,
        BattleOrderRuntimeContext context,
        string fallbackDialogKind,
        string debugMessage,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        string actorId = GetUnitId(actor, context);

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "dialog",
            sourceDialog = string.Empty,
            finalActionSequence = Array.Empty<SotFinalActionDto>(),
            dialogOnly = true,
            fallbackDialogKind = fallbackDialogKind ?? string.Empty,
        };

        debugLog = debugMessage ?? string.Empty;
        return true;
    }

    private static bool TryParseAttack(
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.AllyCount != 1)
        {
            debugLog = "emergency fallback attack failed: ally actor count must be exactly 1.";
            return false;
        }

        if (unitContext.EnemyCount > 1)
        {
            debugLog = "emergency fallback attack failed: enemy target count must be 0 or 1.";
            return false;
        }

        BattleRuntimeUnit actor = unitContext.GetAllyAt(0);
        if (!IsValidActor(actor))
        {
            debugLog = "emergency fallback attack failed: actor is not valid.";
            return false;
        }

        BattleRuntimeUnit target = unitContext.EnemyCount == 1 ? unitContext.GetEnemyAt(0) : null;
        if (!BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, context.SimulationManager))
            target = SelectAttackTarget(actor, context, target);

        if (target == null)
        {
            return BuildDialogOnlyResult(
                actor,
                context,
                BattleCommandEmergencyFallbackDialogKind.AttackNoTargetableEnemy,
                "emergency fallback attack dialog: no targetable enemy.",
                out result,
                out debugLog
            );
        }

        string actorId = GetUnitId(actor, context);
        string targetId = GetUnitId(target, context);

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "attack",
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(new SotFinalActionDto { type = "attack", target = targetId }),
        };

        debugLog = $"emergency fallback attack: {actorId} attacks {targetId}.";
        return true;
    }

    private static bool TryParseSkill(
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.AllyCount < 1 || unitContext.AllyCount > 2)
        {
            debugLog = "emergency fallback skill failed: ally count must be 1 or 2.";
            return false;
        }

        if (unitContext.EnemyCount > 1)
        {
            debugLog = "emergency fallback skill failed: enemy target count must be 0 or 1.";
            return false;
        }

        if (unitContext.UnitCount > 2)
        {
            debugLog = "emergency fallback skill failed: unit id count must be 1 or 2.";
            return false;
        }

        BattleRuntimeUnit actor = unitContext.GetAllyAt(0);
        if (!IsValidActor(actor))
        {
            debugLog = "emergency fallback skill failed: actor is not valid.";
            return false;
        }

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (TryGetSkillUseBlockDialogKind(actor, metadata, out string blockDialogKind))
        {
            return BuildDialogOnlyResult(
                actor,
                context,
                blockDialogKind,
                "emergency fallback skill dialog: actor cannot use skill.",
                out result,
                out debugLog
            );
        }

        BattleRuntimeUnit target;
        if (metadata.isSkillOnSelf)
        {
            if (unitContext.UnitCount != 1)
            {
                debugLog = "emergency fallback skill failed: self skill cannot consume an extra target.";
                return false;
            }

            target = actor;
        }
        else if (metadata.isSkillOnOtherAlly)
        {
            if (unitContext.EnemyCount != 0)
            {
                debugLog = "emergency fallback skill failed: ally skill cannot consume an enemy target.";
                return false;
            }

            if (unitContext.UnitCount == 2)
            {
                target = unitContext.GetAllyAt(1);
                if (!IsValidOtherAllySkillTarget(actor, target, metadata))
                {
                    debugLog = "emergency fallback skill failed: ally skill target is invalid.";
                    return false;
                }
            }
            else
            {
                target = SelectAllySkillTarget(actor, context, metadata);
                if (target == null)
                {
                    string dialogKind = metadata.canSkillTargetDead
                        ? BattleCommandEmergencyFallbackDialogKind.ReviveNoDeadAlly
                        : BattleCommandEmergencyFallbackDialogKind.AllySkillNoTarget;

                    return BuildDialogOnlyResult(
                        actor,
                        context,
                        dialogKind,
                        "emergency fallback skill dialog: no valid ally skill target.",
                        out result,
                        out debugLog
                    );
                }
            }
        }
        else
        {
            if (unitContext.AllyCount != 1)
            {
                debugLog = "emergency fallback skill failed: enemy skill cannot consume an ally target.";
                return false;
            }

            if (unitContext.UnitCount == 2)
            {
                if (unitContext.EnemyCount != 1)
                {
                    debugLog = "emergency fallback skill failed: explicit enemy skill target is invalid.";
                    return false;
                }

                target = unitContext.GetEnemyAt(0);
                if (
                    !BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(
                        target,
                        context.SimulationManager
                    )
                )
                {
                    debugLog = "emergency fallback skill failed: enemy skill target is not targetable.";
                    return false;
                }
            }
            else
            {
                target = SelectEnemySkillTarget(actor, context, metadata);
                if (target == null)
                {
                    return BuildDialogOnlyResult(
                        actor,
                        context,
                        BattleCommandEmergencyFallbackDialogKind.EnemySkillNoTarget,
                        "emergency fallback skill dialog: no targetable enemy for skill.",
                        out result,
                        out debugLog
                    );
                }
            }
        }

        string actorId = GetUnitId(actor, context);
        string targetId = GetUnitId(target, context);

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "skill",
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(
                new SotFinalActionDto
                {
                    type = "skill",
                    description = metadata.skillDescription ?? string.Empty,
                    target = targetId,
                }
            ),
        };

        debugLog = $"emergency fallback skill: {actorId} uses skill on {targetId}.";
        return true;
    }

    private static bool TryParseEscape(
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.EnemyCount > 0)
        {
            debugLog = "emergency fallback escape failed: enemy target is not accepted.";
            return false;
        }

        if (unitContext.AllyCount != 1 && unitContext.AllyCount != 2)
        {
            debugLog = "emergency fallback escape failed: ally count must be 1 or 2.";
            return false;
        }

        BattleRuntimeUnit actor = unitContext.GetAllyAt(0);
        if (!IsValidActor(actor))
        {
            debugLog = "emergency fallback escape failed: actor is not valid.";
            return false;
        }

        BattleRuntimeUnit target =
            unitContext.AllyCount == 2 ? unitContext.GetAllyAt(1) : SelectEscapeTarget(actor, context);
        if (target == null)
        {
            return BuildDialogOnlyResult(
                actor,
                context,
                BattleCommandEmergencyFallbackDialogKind.EscapeNoAnchor,
                "emergency fallback escape dialog: no retreat anchor.",
                out result,
                out debugLog
            );
        }

        if (!BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback escape failed: anchor ally is invalid.";
            return false;
        }

        string actorId = GetUnitId(actor, context);
        string targetId = GetUnitId(target, context);

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "escape",
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(
                new SotFinalActionDto
                {
                    type = "move",
                    subtype = "escape",
                    movementType = "direct",
                    to = targetId,
                }
            ),
        };

        debugLog = $"emergency fallback escape: {actorId} escapes toward {targetId}.";
        return true;
    }

    private static bool TryParseOrbitMove(
        string command,
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.UnitCount != 2)
        {
            debugLog = "emergency fallback orbit failed: unit id count must be exactly 2.";
            return false;
        }

        BattleRuntimeUnit actor = null;
        BattleRuntimeUnit target = null;
        bool targetIsAlly = false;
        bool targetIsEnemy = false;

        if (unitContext.AllyCount == 1 && unitContext.EnemyCount == 1)
        {
            actor = unitContext.GetAllyAt(0);
            target = unitContext.GetEnemyAt(0);
            targetIsEnemy = true;
        }
        else if (unitContext.AllyCount == 2 && unitContext.EnemyCount == 0)
        {
            actor = unitContext.GetAllyAt(0);
            target = unitContext.GetAllyAt(1);
            targetIsAlly = true;
        }

        if (actor == null || !IsValidActor(actor))
        {
            debugLog = "emergency fallback orbit failed: no valid ally actor found.";
            return false;
        }

        if (target == null || (!targetIsAlly && !targetIsEnemy))
        {
            debugLog = "emergency fallback orbit failed: no valid target found.";
            return false;
        }

        if (targetIsAlly && !BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback orbit failed: ally target is invalid.";
            return false;
        }

        if (
            targetIsEnemy
            && !BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(
                target,
                context.SimulationManager
            )
        )
        {
            debugLog = "emergency fallback orbit failed: enemy target is not targetable.";
            return false;
        }

        string actorId = GetUnitId(actor, context);
        string targetId = GetUnitId(target, context);
        string subtype = targetIsEnemy ? "approachOpponent" : "help";

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = subtype,
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(
                new SotFinalActionDto
                {
                    type = "move",
                    subtype = subtype,
                    movementType = "flank",
                    to = targetId,
                }
            ),
        };

        debugLog = $"emergency fallback orbit: {actorId} moves flank toward {targetId}.";
        return true;
    }

    private static bool TryParseGenericMove(
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.UnitCount != 2)
        {
            debugLog = "emergency fallback move failed: unit id count must be exactly 2.";
            return false;
        }

        BattleRuntimeUnit actor = null;
        BattleRuntimeUnit target = null;
        bool targetIsAlly = false;
        bool targetIsEnemy = false;

        if (unitContext.AllyCount == 1 && unitContext.EnemyCount == 1)
        {
            actor = unitContext.GetAllyAt(0);
            target = unitContext.GetEnemyAt(0);
            targetIsEnemy = true;
        }
        else if (unitContext.AllyCount == 2 && unitContext.EnemyCount == 0)
        {
            actor = unitContext.GetAllyAt(0);
            target = unitContext.GetAllyAt(1);
            targetIsAlly = true;
        }

        if (actor == null || !IsValidActor(actor))
        {
            debugLog = "emergency fallback move failed: no valid ally actor found.";
            return false;
        }

        if (target == null || (!targetIsAlly && !targetIsEnemy))
        {
            debugLog = "emergency fallback move failed: no valid target found.";
            return false;
        }

        if (targetIsAlly && !BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback move failed: ally target is invalid.";
            return false;
        }

        if (
            targetIsEnemy
            && !BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(
                target,
                context.SimulationManager
            )
        )
        {
            debugLog = "emergency fallback move failed: enemy target is not targetable.";
            return false;
        }

        string actorId = GetUnitId(actor, context);
        string targetId = GetUnitId(target, context);
        string subtype = targetIsEnemy ? "approachOpponent" : "help";

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = subtype,
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(
                new SotFinalActionDto
                {
                    type = "move",
                    subtype = subtype,
                    movementType = "direct",
                    to = targetId,
                }
            ),
        };

        debugLog = $"emergency fallback move: {actorId} moves direct toward {targetId}.";
        return true;
    }

    private static bool TryParseWait(
        EmergencyUnitContext unitContext,
        List<int> integers,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.AllyCount != 1 || unitContext.EnemyCount != 0)
        {
            debugLog = "emergency fallback wait failed: exactly one ally actor and no enemy target are required.";
            return false;
        }

        BattleRuntimeUnit actor = unitContext.GetAllyAt(0);
        if (!IsValidActor(actor))
        {
            debugLog = "emergency fallback wait failed: actor is not valid.";
            return false;
        }

        float durationSec = ClampDuration(integers != null && integers.Count > 0 ? integers[0] : 1);
        string actorId = unitContext.GetAllyIdAt(0);

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "wait",
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(new SotFinalActionDto { type = "wait", durationSec = durationSec }),
        };

        debugLog = $"emergency fallback wait: {actorId} waits {durationSec:0.##} seconds.";
        return true;
    }

    private static bool TryParseSkillControl(
        string command,
        BattleOrderRuntimeContext context,
        EmergencyUnitContext unitContext,
        List<int> integers,
        out BattleCommandEmergencyFallbackParseResult result,
        out string debugLog
    )
    {
        result = null;
        debugLog = string.Empty;

        if (unitContext.AllyCount != 1 || unitContext.EnemyCount != 0)
        {
            debugLog =
                "emergency fallback skillControl failed: exactly one ally actor and no enemy target are required.";
            return false;
        }

        BattleRuntimeUnit actor = unitContext.GetAllyAt(0);
        if (!IsValidActor(actor))
        {
            debugLog = "emergency fallback skillControl failed: actor is not valid.";
            return false;
        }

        BattleSkillRuntimeMetadata metadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(actor);
        if (metadata.skillId == WeaponSkillId.None)
        {
            return BuildDialogOnlyResult(
                actor,
                context,
                BattleCommandEmergencyFallbackDialogKind.SkillControlNoSkill,
                "emergency fallback skillControl dialog: actor has no skill.",
                out result,
                out debugLog
            );
        }

        bool defer = ContainsExact(command, "미뤄");
        string actorId = GetUnitId(actor, context);
        SotFinalActionDto action = defer
            ? new SotFinalActionDto
            {
                type = "skillControl",
                mode = "defer",
                durationSec = ClampDuration(integers != null && integers.Count > 0 ? integers[0] : 1),
            }
            : new SotFinalActionDto { type = "skillControl", mode = "forbid" };

        result = new BattleCommandEmergencyFallbackParseResult
        {
            actorUnitId = actorId,
            mainActionCategory = "skillControl",
            sourceDialog = "명령을 확인했다.",
            finalActionSequence = One(action),
        };

        debugLog = defer
            ? $"emergency fallback skillControl: {actorId} defers skill."
            : $"emergency fallback skillControl: {actorId} forbids skill.";
        return true;
    }

    private static BattleRuntimeUnit SelectAttackTarget(
        BattleRuntimeUnit actor,
        BattleOrderRuntimeContext context,
        BattleRuntimeUnit originalTarget
    )
    {
        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindClosestTargetableEnemy(
            actor,
            context.Enemies,
            context.SimulationManager
        );

        if (target != null)
            return target;

        if (originalTarget != null)
        {
            target = BattleCommandPostprocessRuntimeQueries.FindTargetableEnemyClosestToPosition(
                originalTarget.Position,
                context.Enemies,
                context.SimulationManager
            );

            if (target != null)
                return target;
        }

        target = BattleCommandPostprocessRuntimeQueries.FindLowestHpTargetableEnemy(
            context.Enemies,
            context.SimulationManager
        );

        if (target != null)
            return target;

        return BattleCommandPostprocessRuntimeQueries.FindEnemyAlreadyEngagedWithActor(
            actor,
            context.Enemies,
            context.SimulationManager
        );
    }

    private static BattleRuntimeUnit SelectAllySkillTarget(
        BattleRuntimeUnit actor,
        BattleOrderRuntimeContext context,
        BattleSkillRuntimeMetadata metadata
    )
    {
        if (metadata.canSkillTargetDead)
            return BattleCommandPostprocessRuntimeQueries.FindDeadAllyTarget(actor, context.Allies);

        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindLowestHpLivingAlly(actor, context.Allies);
        if (target != null)
            return target;

        return BattleCommandPostprocessRuntimeQueries.FindMostPressuredAlly(actor, context.Allies, context.Enemies);
    }

    private static BattleRuntimeUnit SelectEnemySkillTarget(
        BattleRuntimeUnit actor,
        BattleOrderRuntimeContext context,
        BattleSkillRuntimeMetadata metadata
    )
    {
        if (metadata.isSkillAoe)
        {
            BattleRuntimeUnit aoeTarget = BattleCommandPostprocessRuntimeQueries.FindBestAoeCenterEnemy(
                actor,
                context.Enemies,
                context.SimulationManager
            );

            if (aoeTarget != null)
                return aoeTarget;
        }

        return SelectAttackTarget(actor, context, null);
    }

    private static BattleRuntimeUnit SelectEscapeTarget(BattleRuntimeUnit actor, BattleOrderRuntimeContext context)
    {
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap =
            BattleOrderRuntimeQueries.BuildFormationInfoMap(context.Allies, context.Enemies);

        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindEligibleBacklineAlly(
            actor,
            context.Allies,
            formationMap
        );

        if (target != null)
            return target;

        return BattleCommandPostprocessRuntimeQueries.FindFarthestLivingAlly(actor, context.Allies);
    }

    private static bool TryGetSkillUseBlockDialogKind(
        BattleRuntimeUnit actor,
        BattleSkillRuntimeMetadata metadata,
        out string dialogKind
    )
    {
        dialogKind = null;

        if (actor == null || actor.State == null)
            return false;

        if (metadata.skillId == WeaponSkillId.None)
        {
            dialogKind = BattleCommandEmergencyFallbackDialogKind.SkillNone;
            return true;
        }

        if (actor.State.IsSkillDisabled)
        {
            dialogKind = BattleCommandEmergencyFallbackDialogKind.SkillDisabled;
            return true;
        }

        if (actor.State.SkillCooldownRemaining > 0f)
        {
            dialogKind = BattleCommandEmergencyFallbackDialogKind.SkillCooldown;
            return true;
        }

        return false;
    }

    private static bool IsValidOtherAllySkillTarget(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit target,
        BattleSkillRuntimeMetadata metadata
    )
    {
        if (BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
            return true;

        return metadata.canSkillTargetDead
            && BattleCommandPostprocessRuntimeQueries.IsValidDeadAllyTarget(actor, target);
    }

    private static bool IsValidActor(BattleRuntimeUnit actor)
    {
        return actor != null && actor.State != null && !actor.IsCombatDisabled && !actor.State.IsStunned;
    }

    private static string GetUnitId(BattleRuntimeUnit unit, BattleOrderRuntimeContext context)
    {
        return BattleOrderRuntimeQueries.GetUnitId(unit, context != null ? context.RosterProjection : null);
    }

    private static float ClampDuration(int value)
    {
        return Mathf.Clamp(value, 1, 10);
    }

    private static SotFinalActionDto[] One(SotFinalActionDto action)
    {
        return action == null ? Array.Empty<SotFinalActionDto>() : new[] { action };
    }

    private enum EmergencyCommandKind
    {
        Unknown = 0,
        OrbitMove = 1,
        Skill = 2,
        Attack = 3,
        Escape = 4,
        GenericMove = 5,
        Wait = 6,
        SkillControl = 7,
    }

    private sealed class EmergencyUnitContext
    {
        private readonly List<string> _originalUnitIds = new List<string>();
        private readonly List<BattleRuntimeUnit> _originalUnits = new List<BattleRuntimeUnit>();
        private readonly List<bool> _originalIsAlly = new List<bool>();
        private readonly List<bool> _originalIsEnemy = new List<bool>();
        private readonly List<string> _allyIds = new List<string>();
        private readonly List<BattleRuntimeUnit> _allies = new List<BattleRuntimeUnit>();
        private readonly List<string> _enemyIds = new List<string>();
        private readonly List<BattleRuntimeUnit> _enemies = new List<BattleRuntimeUnit>();

        public int UnitCount => _originalUnitIds.Count;
        public int AllyCount => _allies.Count;
        public int EnemyCount => _enemies.Count;
        public bool HasUnknownUnit { get; private set; }

        public EmergencyUnitContext(BattleOrderRuntimeContext context, List<string> unitIds)
        {
            if (unitIds == null)
                return;

            for (int i = 0; i < unitIds.Count; i++)
            {
                string unitId = unitIds[i];
                BattleRuntimeUnit ally = BattleOrderRuntimeQueries.FindUnitById(
                    context.Allies,
                    context.RosterProjection,
                    unitId
                );
                BattleRuntimeUnit enemy = BattleOrderRuntimeQueries.FindUnitById(
                    context.Enemies,
                    context.RosterProjection,
                    unitId
                );
                bool isAlly = ally != null;
                bool isEnemy = enemy != null;
                BattleRuntimeUnit unit = isAlly ? ally : enemy;

                _originalUnitIds.Add(unitId);
                _originalUnits.Add(unit);
                _originalIsAlly.Add(isAlly);
                _originalIsEnemy.Add(isEnemy);

                if (isAlly)
                {
                    _allyIds.Add(unitId);
                    _allies.Add(ally);
                }
                else if (isEnemy)
                {
                    _enemyIds.Add(unitId);
                    _enemies.Add(enemy);
                }
                else
                {
                    HasUnknownUnit = true;
                }
            }
        }

        public BattleRuntimeUnit GetOriginalUnitAt(int index)
        {
            return index >= 0 && index < _originalUnits.Count ? _originalUnits[index] : null;
        }

        public bool IsOriginalUnitAllyAt(int index)
        {
            return index >= 0 && index < _originalIsAlly.Count && _originalIsAlly[index];
        }

        public bool IsOriginalUnitEnemyAt(int index)
        {
            return index >= 0 && index < _originalIsEnemy.Count && _originalIsEnemy[index];
        }

        public BattleRuntimeUnit GetAllyAt(int index)
        {
            return index >= 0 && index < _allies.Count ? _allies[index] : null;
        }

        public string GetAllyIdAt(int index)
        {
            return index >= 0 && index < _allyIds.Count ? _allyIds[index] : string.Empty;
        }

        public BattleRuntimeUnit GetEnemyAt(int index)
        {
            return index >= 0 && index < _enemies.Count ? _enemies[index] : null;
        }
    }
}

public sealed class BattleCommandEmergencyFallbackParseResult
{
    public string actorUnitId;
    public string mainActionCategory;
    public string sourceDialog;
    public SotFinalActionDto[] finalActionSequence;
    public bool dialogOnly;
    public string fallbackDialogKind;
}
