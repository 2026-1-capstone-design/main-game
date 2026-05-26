// Battle order 입력 생성에 필요한 런타임 계산과 질의를 모은다.
// formation role, targetability, 거리 신호, commandAnalysis 후보를 산출한다.
// 실행 plan을 만들지 않고 SOT 입력 필드만 계산한다.
// 후처리 단계에서 InvalidReason과 보정 사유를 이 파일에 확장한다.

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BattleOrderRuntimeContext
{
    public readonly BattleRuntimeUnit[] Allies;
    public readonly BattleRuntimeUnit[] Enemies;
    public readonly IBattleRosterProjection RosterProjection;
    public readonly BattleSimulationManager SimulationManager;

    public BattleOrderRuntimeContext(
        BattleRuntimeUnit[] allies,
        BattleRuntimeUnit[] enemies,
        IBattleRosterProjection rosterProjection,
        BattleSimulationManager simulationManager
    )
    {
        Allies = allies;
        Enemies = enemies;
        RosterProjection = rosterProjection;
        SimulationManager = simulationManager;
    }
}

public readonly struct BattleOrderFormationInfo
{
    public readonly string Role;
    public readonly bool CrossedOpponentCenter;
    public readonly bool HoldFrontAnchorEligible;

    public BattleOrderFormationInfo(string role, bool crossedOpponentCenter, bool holdFrontAnchorEligible)
    {
        Role = role;
        CrossedOpponentCenter = crossedOpponentCenter;
        HoldFrontAnchorEligible = holdFrontAnchorEligible;
    }
}

public sealed class BattleOrderUnitSignals
{
    public float HpRatio;
    public float AttackRatioToAvg;
    public int EngagedByOpponentCount;
    public string ClosestTargetableOpponent;
    public string FarthestTargetableOpponent;
    public string ClosestAliveAlly;
    public string FarthestAliveAlly;
}

public static class BattleOrderRuntimeQueries
{
    private const float OpponentPressureRangeMultiplier = 1.15f;

    // 후처리에서 invalidUnits의 내부 reason map을 만들 때 확장할 예정
    // parser input에는 unitId list만 넣고, 대사용 자연어 요약은 후처리에서 만든다.
    public enum InvalidReason
    {
        None = 0,
        MissingUnit = 1,
        Dead = 2,
        NotPlayerAlly = 3,
        Stunned = 4,
        Untargetable = 5,
        InvalidSide = 6,
    }

    public static string GetUnitId(BattleRuntimeUnit unit, IBattleRosterProjection rosterProjection)
    {
        if (unit == null)
            return "UNKNOWN";

        if (rosterProjection != null)
            return rosterProjection.GetDisplayUnitId(unit);

        return $"U_{Mathf.Clamp(unit.UnitNumber, 0, 99):00}";
    }

    public static BattleRuntimeUnit FindUnitById(
        IReadOnlyList<BattleRuntimeUnit> units,
        IBattleRosterProjection rosterProjection,
        string unitId
    )
    {
        if (units == null || string.IsNullOrWhiteSpace(unitId))
            return null;

        string normalizedUnitId = unitId.Trim();

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null)
                continue;

            string currentUnitId = GetUnitId(unit, rosterProjection);
            if (string.Equals(currentUnitId, normalizedUnitId, StringComparison.OrdinalIgnoreCase))
                return unit;
        }

        return null;
    }

    public static bool IsAlive(BattleRuntimeUnit unit)
    {
        return unit != null && unit.State != null && !unit.IsCombatDisabled;
    }

    public static float GetHpRatio(BattleRuntimeUnit unit)
    {
        if (unit == null || unit.MaxHealth <= 0f)
            return 0f;

        return Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth);
    }

    public static float ComputeLivingAverageAttack(IReadOnlyList<BattleRuntimeUnit> units)
    {
        if (units == null)
            return 1f;

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (!IsAlive(unit))
                continue;

            sum += Mathf.Max(0.0001f, unit.Attack);
            count++;
        }

        return count > 0 ? sum / count : 1f;
    }

    public static float ComputeAttackRatioToAvg(BattleRuntimeUnit unit, float livingTeamAverageAttack)
    {
        if (unit == null)
            return 1f;

        float denominator = Mathf.Max(0.0001f, livingTeamAverageAttack);
        return Mathf.Max(0.0001f, unit.Attack / denominator);
    }

    public static bool HasTargetingBlock(BattleRuntimeUnit candidate, BattleSimulationManager simulationManager)
    {
        if (candidate == null || candidate.IsCombatDisabled)
            return false;

        BattleArtifactSystem artifacts = simulationManager != null ? simulationManager.ArtifactSystem : null;
        return artifacts != null && artifacts.HasTargetingBlock(candidate, BattleTargetingReason.Planner);
    }

    public static bool IsTargetableForParser(BattleRuntimeUnit candidate, BattleSimulationManager simulationManager)
    {
        if (candidate == null || candidate.IsCombatDisabled)
            return false;

        return !HasTargetingBlock(candidate, simulationManager);
    }

    public static int CountEngagedByOpponents(BattleRuntimeUnit unit, IReadOnlyList<BattleRuntimeUnit> opponents)
    {
        if (unit == null || unit.State == null || opponents == null)
            return 0;

        int count = 0;

        for (int i = 0; i < opponents.Count; i++)
        {
            BattleRuntimeUnit opponent = opponents[i];
            if (opponent == null || opponent.State == null || opponent.IsCombatDisabled)
                continue;

            bool directlyTargeting =
                opponent.State.CurrentTarget == unit.State || opponent.State.PlannedTargetEnemy == unit.State;

            bool closeEnough = false;
            float effectiveRange = BattleFieldSnapshot.GetEffectiveAttackDistance(opponent.State, unit.State);
            float pressureRange = effectiveRange * OpponentPressureRangeMultiplier;
            Vector3 diff = opponent.Position - unit.Position;
            diff.y = 0f;
            closeEnough = diff.sqrMagnitude <= pressureRange * pressureRange;

            if (directlyTargeting || closeEnough)
                count++;
        }

        return count;
    }

    public static BattleOrderUnitSignals BuildSignals(
        BattleRuntimeUnit unit,
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        IBattleRosterProjection rosterProjection,
        BattleSimulationManager simulationManager,
        float livingTeamAverageAttack
    )
    {
        BattleOrderUnitSignals signals = new BattleOrderUnitSignals();

        signals.HpRatio = GetHpRatio(unit);
        signals.AttackRatioToAvg = ComputeAttackRatioToAvg(unit, livingTeamAverageAttack);
        signals.EngagedByOpponentCount = CountEngagedByOpponents(unit, enemies);

        ResolveClosestAndFarthestTargetableOpponent(
            unit,
            enemies,
            rosterProjection,
            simulationManager,
            out signals.ClosestTargetableOpponent,
            out signals.FarthestTargetableOpponent
        );

        ResolveClosestAndFarthestAliveAlly(
            unit,
            allies,
            rosterProjection,
            out signals.ClosestAliveAlly,
            out signals.FarthestAliveAlly
        );

        return signals;
    }

    private static void ResolveClosestAndFarthestTargetableOpponent(
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        IBattleRosterProjection rosterProjection,
        BattleSimulationManager simulationManager,
        out string closest,
        out string farthest
    )
    {
        closest = null;
        farthest = null;

        if (self == null || enemies == null)
            return;

        BattleRuntimeUnit closestUnit = null;
        BattleRuntimeUnit farthestUnit = null;
        float closestDistSqr = float.MaxValue;
        float farthestDistSqr = float.MinValue;

        for (int i = 0; i < enemies.Count; i++)
        {
            BattleRuntimeUnit enemy = enemies[i];
            if (!IsTargetableForParser(enemy, simulationManager))
                continue;

            Vector3 diff = enemy.Position - self.Position;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestUnit = enemy;
            }

            if (distSqr > farthestDistSqr)
            {
                farthestDistSqr = distSqr;
                farthestUnit = enemy;
            }
        }

        if (closestUnit != null)
            closest = GetUnitId(closestUnit, rosterProjection);

        if (farthestUnit != null)
            farthest = GetUnitId(farthestUnit, rosterProjection);
    }

    private static void ResolveClosestAndFarthestAliveAlly(
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> allies,
        IBattleRosterProjection rosterProjection,
        out string closest,
        out string farthest
    )
    {
        closest = null;
        farthest = null;

        if (self == null || allies == null)
            return;

        BattleRuntimeUnit closestUnit = null;
        BattleRuntimeUnit farthestUnit = null;
        float closestDistSqr = float.MaxValue;
        float farthestDistSqr = float.MinValue;

        for (int i = 0; i < allies.Count; i++)
        {
            BattleRuntimeUnit ally = allies[i];
            if (ally == null || ally == self || !IsAlive(ally))
                continue;

            Vector3 diff = ally.Position - self.Position;
            diff.y = 0f;
            float distSqr = diff.sqrMagnitude;

            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestUnit = ally;
            }

            if (distSqr > farthestDistSqr)
            {
                farthestDistSqr = distSqr;
                farthestUnit = ally;
            }
        }

        if (closestUnit != null)
            closest = GetUnitId(closestUnit, rosterProjection);

        if (farthestUnit != null)
            farthest = GetUnitId(farthestUnit, rosterProjection);
    }

    public static Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> BuildFormationInfoMap(
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyList<BattleRuntimeUnit> enemies
    )
    {
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> result =
            new Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo>();

        List<BattleRuntimeUnit> livingAllies = CollectLivingUnits(allies);
        List<BattleRuntimeUnit> livingEnemies = CollectLivingUnits(enemies);

        if (livingAllies.Count == 0 && livingEnemies.Count == 0)
            return result;

        Vector3 allyCenter = ComputeCenter(livingAllies);
        Vector3 enemyCenter = ComputeCenter(livingEnemies);

        Vector3 axis = enemyCenter - allyCenter;
        axis.y = 0f;

        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.forward;
        else
            axis.Normalize();

        FillTeamFormationInfo(result, livingAllies, axis, allyCenter, enemyCenter, isPlayerSide: true);

        FillTeamFormationInfo(result, livingEnemies, axis, allyCenter, enemyCenter, isPlayerSide: false);

        return result;
    }

    private static List<BattleRuntimeUnit> CollectLivingUnits(IReadOnlyList<BattleRuntimeUnit> units)
    {
        List<BattleRuntimeUnit> result = new List<BattleRuntimeUnit>();

        if (units == null)
            return result;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (IsAlive(unit))
                result.Add(unit);
        }

        return result;
    }

    private static Vector3 ComputeCenter(IReadOnlyList<BattleRuntimeUnit> units)
    {
        if (units == null || units.Count == 0)
            return Vector3.zero;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null)
                continue;

            sum += unit.Position;
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    private static void FillTeamFormationInfo(
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> result,
        IReadOnlyList<BattleRuntimeUnit> teamUnits,
        Vector3 axis,
        Vector3 allyCenter,
        Vector3 enemyCenter,
        bool isPlayerSide
    )
    {
        if (teamUnits == null || teamUnits.Count == 0)
            return;

        float minS = float.MaxValue;
        float maxS = float.MinValue;

        for (int i = 0; i < teamUnits.Count; i++)
        {
            float s = Project(teamUnits[i].Position, axis);
            minS = Mathf.Min(minS, s);
            maxS = Mathf.Max(maxS, s);
        }

        float range = Mathf.Max(0.0001f, maxS - minS);
        float t1 = minS + range / 3f;
        float t2 = minS + range * 2f / 3f;

        float allyCenterS = Project(allyCenter, axis);
        float enemyCenterS = Project(enemyCenter, axis);

        for (int i = 0; i < teamUnits.Count; i++)
        {
            BattleRuntimeUnit unit = teamUnits[i];
            float s = Project(unit.Position, axis);
            string role;
            bool crossedOpponentCenter;

            if (isPlayerSide)
            {
                crossedOpponentCenter = s >= enemyCenterS;
                if (crossedOpponentCenter)
                    role = "frontline";
                else if (s < t1)
                    role = "backline";
                else if (s < t2)
                    role = "midline";
                else
                    role = "frontline";
            }
            else
            {
                crossedOpponentCenter = s <= allyCenterS;
                if (crossedOpponentCenter)
                    role = "frontline";
                else if (s < t1)
                    role = "frontline";
                else if (s < t2)
                    role = "midline";
                else
                    role = "backline";
            }

            bool holdFrontAnchorEligible = !crossedOpponentCenter;

            result[unit] = new BattleOrderFormationInfo(role, crossedOpponentCenter, holdFrontAnchorEligible);
        }
    }

    private static float Project(Vector3 position, Vector3 axis)
    {
        Vector3 p = position;
        p.y = 0f;
        return Vector3.Dot(p, axis);
    }

    public static SotCommandAnalysisDto BuildCommandAnalysis(
        IReadOnlyList<BattleRuntimeUnit> allies,
        IReadOnlyList<BattleRuntimeUnit> enemies,
        IBattleRosterProjection rosterProjection,
        BattleSimulationManager simulationManager
    )
    {
        List<string> allowedActors = new List<string>();
        List<string> allowedAttackTargets = new List<string>();
        List<string> validMoveToUnits = new List<string>();
        List<string> deadAllies = new List<string>();
        List<string> invalidUnits = new List<string>();

        if (allies != null)
        {
            for (int i = 0; i < allies.Count; i++)
            {
                BattleRuntimeUnit ally = allies[i];
                if (ally == null)
                    continue;

                string unitId = GetUnitId(ally, rosterProjection);

                if (ally.IsCombatDisabled)
                {
                    deadAllies.Add(unitId);
                    invalidUnits.Add(unitId);
                    continue;
                }

                if (ally.State != null && !ally.State.IsStunned)
                    allowedActors.Add(unitId);

                validMoveToUnits.Add(unitId);
            }
        }

        if (enemies != null)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                BattleRuntimeUnit enemy = enemies[i];
                if (enemy == null)
                    continue;

                string unitId = GetUnitId(enemy, rosterProjection);

                if (IsTargetableForParser(enemy, simulationManager))
                {
                    allowedAttackTargets.Add(unitId);
                    validMoveToUnits.Add(unitId);
                }
                else
                {
                    invalidUnits.Add(unitId);
                }
            }
        }

        return new SotCommandAnalysisDto
        {
            analysisMode = "runtime_constraint_summary",
            allowedActors = allowedActors.ToArray(),
            allowedAttackTargets = allowedAttackTargets.ToArray(),
            validMoveToUnits = validMoveToUnits.ToArray(),
            deadAllies = deadAllies.ToArray(),
            invalidUnits = invalidUnits.ToArray(),
            actionPolicy = BuildDefaultActionPolicy(),
        };
    }

    private static SotActionPolicyDto BuildDefaultActionPolicy()
    {
        return new SotActionPolicyDto
        {
            maxActionsPerActor = 3,
            allowedActionTypes = new[] { "move", "attack", "skill", "wait", "skillControl" },
            allowedMoveSubtypes = new[] { "approachOpponent", "escape", "help", "holdFront" },
            allowedMovementTypes = new[] { "direct", "flank" },
            waitDurationSecMin = 1f,
            waitDurationSecMax = 10f,
            skillControlDeferSecMin = 1f,
            skillControlDeferSecMax = 10f,
        };
    }

    public static BattleSkillRuntimeMetadata ResolveSkillMetadata(BattleRuntimeUnit unit)
    {
        WeaponSkillId skillId =
            unit != null && unit.Snapshot != null ? unit.Snapshot.WeaponSkillId : WeaponSkillId.None;

        return ResolveSkillMetadata(skillId);
    }

    public static BattleSkillRuntimeMetadata ResolveSkillMetadata(WeaponSkillId skillId)
    {
        BattleSkillRuntimeMetadata empty = new BattleSkillRuntimeMetadata
        {
            skillId = skillId,
            skillDescription = string.Empty,
            isSkillOnSelf = false,
            isSkillOnOtherAlly = false,
            isSkillAoe = false,
            canSkillTargetDead = false,
        };

        if (skillId == WeaponSkillId.None)
            return empty;

        ContentDatabaseProvider provider = ContentDatabaseProvider.Instance;
        if (provider == null)
            return empty;

        IReadOnlyList<WeaponSkillSO> weaponSkills = provider.WeaponSkills;
        if (weaponSkills == null)
            return empty;

        for (int i = 0; i < weaponSkills.Count; i++)
        {
            WeaponSkillSO skill = weaponSkills[i];
            if (skill == null || skill.skillId != skillId)
                continue;

            return new BattleSkillRuntimeMetadata
            {
                skillId = skill.skillId,
                skillDescription = string.IsNullOrWhiteSpace(skill.description)
                    ? string.Empty
                    : skill.description.Trim(),
                isSkillOnSelf = skill.isSkillOnSelf,
                isSkillOnOtherAlly = skill.isSkillOnOtherAlly,
                isSkillAoe = skill.isSkillAoe,
                canSkillTargetDead = skill.canSkillTargetDead,
            };
        }

        return empty;
    }
}
