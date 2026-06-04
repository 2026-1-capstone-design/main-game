// 원본 한글 명령에서 최후방 단일 행동 폴백을 만든다.
// keyword와 unitId는 fuzzy 보정 없이 exact match만 사용한다.
// 단일 actor와 단일 action만 허용하고, 모호하면 실패한다.
// skill target이 생략되면 스킬 메타데이터와 전장 상태로 기본 target을 고른다.
// 이 파일은 전장 상태를 읽기만 하며 실행 명령을 직접 발행하지 않는다.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class BattleCommandEmergencyFallbackParser
{
    public const string AdvisorLine = "잘 못들었나본데요?";

    private static readonly Regex UnitIdRegex = new Regex("[AaEe]_[0-9]{2}", RegexOptions.Compiled);
    private static readonly Regex IntRegex = new Regex("(?<![0-9A-Za-z_])-?[0-9]+(?![0-9A-Za-z_])", RegexOptions.Compiled);
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

        if (ContainsExact(command, "스킬") && !ContainsSkillControlKeyword(command))
            return EmergencyCommandKind.Skill;

        if (ContainsAny(command, "공격", "때려", "쳐", "죽여", "잡아", "처리", "물어"))
            return EmergencyCommandKind.Attack;

        if (ContainsAny(command, "도망", "빠져", "후퇴", "빼", "물러나", "물러서"))
            return EmergencyCommandKind.Escape;

        if (ContainsAny(command, "이동", "이동해", "걸어가", "쪽으로", "붙어", "접근"))
            return EmergencyCommandKind.GenericMove;

        if (ContainsAny(command, "대기", "기다", "멈춰"))
            return EmergencyCommandKind.Wait;

        if (ContainsSkillControlKeyword(command))
            return EmergencyCommandKind.SkillControl;

        return EmergencyCommandKind.Unknown;
    }

    private static bool ContainsOrbitKeyword(string command)
    {
        return ContainsExact(command, "돌아") || StandaloneBingRegex.IsMatch(command);
    }

    private static bool ContainsSkillControlKeyword(string command)
    {
        return ContainsExact(command, "스킬 아껴")
            || ContainsExact(command, "스킬 쓰지")
            || ContainsExact(command, "금지")
            || ContainsExact(command, "미뤄");
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
            debugLog = "emergency fallback attack failed: no targetable enemy.";
            return false;
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
        if (!CanActorUseSkill(actor, metadata))
        {
            debugLog = "emergency fallback skill failed: actor cannot use skill.";
            return false;
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
                    debugLog = "emergency fallback skill failed: no valid ally skill target.";
                    return false;
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
                if (!BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, context.SimulationManager))
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
                    debugLog = "emergency fallback skill failed: no targetable enemy for skill.";
                    return false;
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

        BattleRuntimeUnit target = unitContext.AllyCount == 2 ? unitContext.GetAllyAt(1) : SelectEscapeTarget(actor, context);
        if (target != null && !BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback escape failed: anchor ally is invalid.";
            return false;
        }

        string actorId = GetUnitId(actor, context);
        string targetId = target != null ? GetUnitId(target, context) : null;

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

        debugLog = string.IsNullOrWhiteSpace(targetId)
            ? $"emergency fallback escape: {actorId} escapes."
            : $"emergency fallback escape: {actorId} escapes toward {targetId}.";
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

        BattleRuntimeUnit actor = unitContext.GetOriginalUnitAt(0);
        BattleRuntimeUnit target = unitContext.GetOriginalUnitAt(1);
        bool targetIsAlly = unitContext.IsOriginalUnitAllyAt(1);
        bool targetIsEnemy = unitContext.IsOriginalUnitEnemyAt(1);

        if (!unitContext.IsOriginalUnitAllyAt(0) || !IsValidActor(actor))
        {
            debugLog = "emergency fallback orbit failed: first unit must be a valid ally actor.";
            return false;
        }

        if (!targetIsAlly && !targetIsEnemy)
        {
            debugLog = "emergency fallback orbit failed: second unit must be ally or enemy.";
            return false;
        }

        if (targetIsAlly && !BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback orbit failed: ally target is invalid.";
            return false;
        }

        if (targetIsEnemy && !BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, context.SimulationManager))
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

        BattleRuntimeUnit actor = unitContext.GetOriginalUnitAt(0);
        BattleRuntimeUnit target = unitContext.GetOriginalUnitAt(1);
        bool targetIsAlly = unitContext.IsOriginalUnitAllyAt(1);
        bool targetIsEnemy = unitContext.IsOriginalUnitEnemyAt(1);

        if (!unitContext.IsOriginalUnitAllyAt(0) || !IsValidActor(actor))
        {
            debugLog = "emergency fallback move failed: first unit must be a valid ally actor.";
            return false;
        }

        if (!targetIsAlly && !targetIsEnemy)
        {
            debugLog = "emergency fallback move failed: second unit must be ally or enemy.";
            return false;
        }

        if (targetIsAlly && !BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
        {
            debugLog = "emergency fallback move failed: ally target is invalid.";
            return false;
        }

        if (targetIsEnemy && !BattleCommandPostprocessRuntimeQueries.IsEnemyTargetableForPostprocess(target, context.SimulationManager))
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
            debugLog = "emergency fallback skillControl failed: exactly one ally actor and no enemy target are required.";
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
            debugLog = "emergency fallback skillControl failed: actor has no skill.";
            return false;
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
        {
            BattleRuntimeUnit deadAlly = BattleCommandPostprocessRuntimeQueries.FindDeadAllyTarget(actor, context.Allies);
            if (deadAlly != null)
                return deadAlly;
        }

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
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap = BattleOrderRuntimeQueries.BuildFormationInfoMap(
            context.Allies,
            context.Enemies
        );

        BattleRuntimeUnit target = BattleCommandPostprocessRuntimeQueries.FindEligibleBacklineAlly(
            actor,
            context.Allies,
            formationMap
        );

        if (target != null)
            return target;

        return BattleCommandPostprocessRuntimeQueries.FindFarthestLivingAlly(actor, context.Allies);
    }

    private static bool CanActorUseSkill(BattleRuntimeUnit actor, BattleSkillRuntimeMetadata metadata)
    {
        return actor != null
            && actor.State != null
            && metadata.skillId != WeaponSkillId.None
            && !actor.State.IsSkillDisabled
            && actor.State.SkillCooldownRemaining <= 0f;
    }

    private static bool IsValidOtherAllySkillTarget(
        BattleRuntimeUnit actor,
        BattleRuntimeUnit target,
        BattleSkillRuntimeMetadata metadata
    )
    {
        if (BattleCommandPostprocessRuntimeQueries.IsValidOtherAllyTarget(actor, target))
            return true;

        return metadata.canSkillTargetDead && BattleCommandPostprocessRuntimeQueries.IsValidDeadAllyTarget(actor, target);
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
                BattleRuntimeUnit ally = BattleOrderRuntimeQueries.FindUnitById(context.Allies, context.RosterProjection, unitId);
                BattleRuntimeUnit enemy = BattleOrderRuntimeQueries.FindUnitById(context.Enemies, context.RosterProjection, unitId);
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
}
