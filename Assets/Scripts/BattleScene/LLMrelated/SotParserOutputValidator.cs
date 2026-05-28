// SOT parser output을 mock/server 공통으로 최소 검증한다.
// BattleMockCommandParser는 mock 명령어 파싱만 담당한다.
// 이 validator는 parser output schema, actor/target 범위, skill/move 규칙을 검사한다.
// duration clamp, 순응/거부, runtime 보정은 기존 후처리 단계에서 수행한다.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class SotParserOutputValidator
{
    private static readonly Regex UnitIdRegex = new Regex("^[AaEe]_[0-9]{2}$", RegexOptions.Compiled);

    public static bool TryValidate(SotParserOutputDto output, SotParserRequestDto request, out string error)
    {
        error = string.Empty;

        if (request == null || request.input == null)
        {
            error = "parser request is null.";
            return false;
        }

        if (output == null)
        {
            error = "parser output is null.";
            return false;
        }

        if (output.thinking == null)
        {
            error = "thinking is null.";
            return false;
        }

        SotDialogLineDto[] dialog = output.dialog ?? Array.Empty<SotDialogLineDto>();
        SotActorActionDto[] action = output.action ?? Array.Empty<SotActorActionDto>();

        if (action.Length == 0)
        {
            if (dialog.Length != 0)
            {
                error = "dialog must be empty when action is empty.";
                return false;
            }

            return true;
        }

        ParserOutputValidationContext context = new ParserOutputValidationContext(request);
        HashSet<string> actionActors = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < action.Length; i++)
        {
            SotActorActionDto actorAction = action[i];
            if (actorAction == null)
            {
                error = $"action[{i}] is null.";
                return false;
            }

            if (!TryNormalizeUnitId(actorAction.unitId, out string actorId))
            {
                error = $"action[{i}].unitId is invalid.";
                return false;
            }

            if (!actionActors.Add(actorId))
            {
                error = $"duplicated action actor '{actorId}'.";
                return false;
            }

            if (!context.IsAllowedActor(actorId))
            {
                error = $"action actor '{actorId}' is not an allowed actor.";
                return false;
            }

            SotFinalActionDto[] sequence = actorAction.sequence ?? Array.Empty<SotFinalActionDto>();
            if (sequence.Length == 0)
            {
                error = $"action actor '{actorId}' has empty sequence.";
                return false;
            }

            if (sequence.Length > context.MaxActionsPerActor)
            {
                error = $"actor '{actorId}' exceeds max action count {context.MaxActionsPerActor}.";
                return false;
            }

            for (int sequenceIndex = 0; sequenceIndex < sequence.Length; sequenceIndex++)
            {
                if (!TryValidateSequenceItem(actorId, sequence[sequenceIndex], sequenceIndex, context, out error))
                    return false;
            }
        }

        if (!TryValidateDialog(dialog, actionActors, out error))
            return false;

        return true;
    }

    private static bool TryValidateDialog(SotDialogLineDto[] dialog, HashSet<string> actionActors, out string error)
    {
        error = string.Empty;

        if (dialog == null)
            dialog = Array.Empty<SotDialogLineDto>();

        if (dialog.Length != actionActors.Count)
        {
            error = "dialog count must match action actor count.";
            return false;
        }

        HashSet<string> dialogActors = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < dialog.Length; i++)
        {
            SotDialogLineDto line = dialog[i];
            if (line == null)
            {
                error = $"dialog[{i}] is null.";
                return false;
            }

            if (!TryNormalizeUnitId(line.unitId, out string unitId))
            {
                error = $"dialog[{i}].unitId is invalid.";
                return false;
            }

            if (!actionActors.Contains(unitId))
            {
                error = $"dialog actor '{unitId}' does not exist in action actors.";
                return false;
            }

            if (!dialogActors.Add(unitId))
            {
                error = $"duplicated dialog actor '{unitId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(line.text))
            {
                error = $"dialog[{i}].text is empty.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateSequenceItem(
        string actorId,
        SotFinalActionDto action,
        int sequenceIndex,
        ParserOutputValidationContext context,
        out string error
    )
    {
        error = string.Empty;

        if (action == null)
        {
            error = $"sequence[{sequenceIndex}] is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(action.type))
        {
            error = $"sequence[{sequenceIndex}].type is empty.";
            return false;
        }

        switch (action.type)
        {
            case "attack":
                return TryValidateAttack(actorId, action, sequenceIndex, context, out error);

            case "skill":
                return TryValidateSkill(actorId, action, sequenceIndex, context, out error);

            case "wait":
                return TryValidateWait(action, sequenceIndex, out error);

            case "skillControl":
                return TryValidateSkillControl(actorId, action, sequenceIndex, context, out error);

            case "move":
                return TryValidateMove(actorId, action, sequenceIndex, context, out error);

            default:
                error = $"sequence[{sequenceIndex}] has unsupported action type '{action.type}'.";
                return false;
        }
    }

    private static bool TryValidateAttack(
        string actorId,
        SotFinalActionDto action,
        int sequenceIndex,
        ParserOutputValidationContext context,
        out string error
    )
    {
        error = string.Empty;

        if (!TryNormalizeUnitId(action.target, out string targetId))
        {
            error = $"attack sequence[{sequenceIndex}] target is invalid.";
            return false;
        }

        if (!context.IsAllowedAttackTarget(targetId))
        {
            error = $"attack target '{targetId}' is not an allowed attack target.";
            return false;
        }

        return true;
    }

    private static bool TryValidateSkill(
        string actorId,
        SotFinalActionDto action,
        int sequenceIndex,
        ParserOutputValidationContext context,
        out string error
    )
    {
        error = string.Empty;

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

        if (
            !string.Equals(
                action.description ?? string.Empty,
                actor.skillDescription ?? string.Empty,
                StringComparison.Ordinal
            )
        )
        {
            error = $"skill description for actor '{actorId}' does not match actor skillDescription.";
            return false;
        }

        if (!TryNormalizeUnitId(action.target, out string targetId))
        {
            error = $"skill sequence[{sequenceIndex}] target is invalid.";
            return false;
        }

        if (actor.IsSkillOnSelf)
        {
            if (!string.Equals(targetId, actorId, StringComparison.Ordinal))
            {
                error = $"self skill target must be actor '{actorId}'.";
                return false;
            }

            return true;
        }

        if (actor.IsSkillOnOtherAlly)
        {
            if (!context.IsValidSkillAllyTarget(actorId, targetId, actor.canSkillTargetDead))
            {
                error = $"skill target '{targetId}' is not a valid ally skill target for '{actorId}'.";
                return false;
            }

            return true;
        }

        if (!context.IsValidSkillEnemyTarget(targetId))
        {
            error = $"skill target '{targetId}' is not a valid enemy skill target.";
            return false;
        }

        return true;
    }

    private static bool TryValidateWait(SotFinalActionDto action, int sequenceIndex, out string error)
    {
        error = string.Empty;

        if (!action.durationSec.HasValue)
        {
            error = $"wait sequence[{sequenceIndex}] durationSec is missing.";
            return false;
        }

        return true;
    }

    private static bool TryValidateSkillControl(
        string actorId,
        SotFinalActionDto action,
        int sequenceIndex,
        ParserOutputValidationContext context,
        out string error
    )
    {
        error = string.Empty;

        if (!context.ActorHasSkill(actorId))
        {
            error = $"skillControl actor '{actorId}' has no skill metadata.";
            return false;
        }

        if (action.mode == "defer")
        {
            if (!action.durationSec.HasValue)
            {
                error = $"skillControl defer sequence[{sequenceIndex}] durationSec is missing.";
                return false;
            }

            return true;
        }

        if (action.mode == "forbid")
            return true;

        error = $"skillControl sequence[{sequenceIndex}] mode is invalid.";
        return false;
    }

    private static bool TryValidateMove(
        string actorId,
        SotFinalActionDto action,
        int sequenceIndex,
        ParserOutputValidationContext context,
        out string error
    )
    {
        error = string.Empty;

        if (
            action.subtype != "approachOpponent"
            && action.subtype != "escape"
            && action.subtype != "help"
            && action.subtype != "holdFront"
        )
        {
            error = $"move sequence[{sequenceIndex}] subtype is invalid.";
            return false;
        }

        if (action.movementType != "direct" && action.movementType != "flank")
        {
            error = $"move sequence[{sequenceIndex}] movementType is invalid.";
            return false;
        }

        if (!TryNormalizeUnitId(action.to, out string targetId))
        {
            error = $"move sequence[{sequenceIndex}] to is invalid.";
            return false;
        }

        return IsValidMoveTarget(actorId, targetId, action.subtype, context, out error);
    }

    private static bool IsValidMoveTarget(
        string actorId,
        string targetId,
        string subtype,
        ParserOutputValidationContext context,
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
                if (actorId == targetId)
                    return true;

                if (!context.IsLivingAlly(targetId))
                {
                    error = $"escape target '{targetId}' is not a living ally.";
                    return false;
                }

                return true;
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

    private static bool TryNormalizeUnitId(string rawUnitId, out string unitId)
    {
        unitId = null;

        if (string.IsNullOrWhiteSpace(rawUnitId))
            return false;

        string normalized = rawUnitId.Trim().ToUpperInvariant();
        if (!UnitIdRegex.IsMatch(normalized))
            return false;

        unitId = normalized;
        return true;
    }

    private static bool HasSkill(SotAllyUnitDto actor)
    {
        return actor != null && !string.IsNullOrWhiteSpace(actor.skillDescription);
    }

    private sealed class ParserOutputValidationContext
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

        public ParserOutputValidationContext(SotParserRequestDto request)
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
