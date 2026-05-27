// SOT parser layer에 넘길 input과 commandAnalysis를 조립한다.
// 전장 상태를 읽기만 하며 시뮬레이션 명령을 발행하지 않는다.
// skill 메타데이터는 WeaponSkillId 기준 registry에서 조회한다.
// 후처리 단계에서 actor별 reason map과 보정 요약을 추가한다.

using System.Collections.Generic;

public sealed class BattleParserInputBuilder
{
    public SotParserRequestDto Build(string rawCommand, BattleOrderRuntimeContext context)
    {
        BattleRuntimeUnit[] allies = context != null ? context.Allies : null;
        BattleRuntimeUnit[] enemies = context != null ? context.Enemies : null;
        IBattleRosterProjection roster = context != null ? context.RosterProjection : null;
        BattleSimulationManager simulation = context != null ? context.SimulationManager : null;

        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap =
            BattleOrderRuntimeQueries.BuildFormationInfoMap(allies, enemies);

        float allyAverageAttack = BattleOrderRuntimeQueries.ComputeLivingAverageAttack(allies);
        float enemyAverageAttack = BattleOrderRuntimeQueries.ComputeLivingAverageAttack(enemies);

        SotAllyUnitDto[] allyDtos = BuildAllyDtos(allies, enemies, roster, simulation, formationMap, allyAverageAttack);

        SotEnemyUnitDto[] enemyDtos = BuildEnemyDtos(
            enemies,
            allies,
            roster,
            simulation,
            formationMap,
            enemyAverageAttack
        );

        return new SotParserRequestDto
        {
            input = new SotParserInputDto
            {
                command = rawCommand ?? string.Empty,
                area_situation = new SotAreaSituationDto { allies = allyDtos, enemies = enemyDtos },
            },
            commandAnalysis = BattleOrderRuntimeQueries.BuildCommandAnalysis(allies, enemies, roster, simulation),
        };
    }

    private static SotAllyUnitDto[] BuildAllyDtos(
        BattleRuntimeUnit[] allies,
        BattleRuntimeUnit[] enemies,
        IBattleRosterProjection roster,
        BattleSimulationManager simulation,
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap,
        float livingAverageAttack
    )
    {
        List<SotAllyUnitDto> result = new List<SotAllyUnitDto>();

        if (allies == null)
            return result.ToArray();

        for (int i = 0; i < allies.Length; i++)
        {
            BattleRuntimeUnit unit = allies[i];
            if (unit == null)
                continue;

            BattleOrderFormationInfo formation = ResolveFormation(unit, formationMap);
            BattleOrderUnitSignals signals = BattleOrderRuntimeQueries.BuildSignals(
                unit,
                allies,
                enemies,
                roster,
                simulation,
                livingAverageAttack
            );

            BattleSkillRuntimeMetadata skillMetadata = BattleOrderRuntimeQueries.ResolveSkillMetadata(unit);

            result.Add(
                new SotAllyUnitDto
                {
                    unitId = BattleOrderRuntimeQueries.GetUnitId(unit, roster),
                    isAlive = BattleOrderRuntimeQueries.IsAlive(unit),
                    canBeTargeted = BattleOrderRuntimeQueries.IsTargetableForParser(unit, simulation),
                    isRanged = unit.Snapshot != null && unit.Snapshot.IsRanged,
                    hpRatio = signals.HpRatio,
                    attackRatioToAvg = signals.AttackRatioToAvg,
                    engagedByOpponentCount = signals.EngagedByOpponentCount,
                    teamFormationRole = formation.Role,
                    skillDescription = skillMetadata.skillDescription ?? string.Empty,
                    IsSkillOnSelf = skillMetadata.isSkillOnSelf,
                    IsSkillOnOtherAlly = skillMetadata.isSkillOnOtherAlly,
                    isSkillAoe = skillMetadata.isSkillAoe,
                    canSkillTargetDead = skillMetadata.canSkillTargetDead,
                    closestTargetableOpponent = signals.ClosestTargetableOpponent,
                    farthestTargetableOpponent = signals.FarthestTargetableOpponent,
                    closestAliveAlly = signals.ClosestAliveAlly,
                    farthestAliveAlly = signals.FarthestAliveAlly,
                }
            );
        }

        return result.ToArray();
    }

    private static SotEnemyUnitDto[] BuildEnemyDtos(
        BattleRuntimeUnit[] enemies,
        BattleRuntimeUnit[] allies,
        IBattleRosterProjection roster,
        BattleSimulationManager simulation,
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap,
        float livingAverageAttack
    )
    {
        List<SotEnemyUnitDto> result = new List<SotEnemyUnitDto>();

        if (enemies == null)
            return result.ToArray();

        for (int i = 0; i < enemies.Length; i++)
        {
            BattleRuntimeUnit unit = enemies[i];
            if (unit == null)
                continue;

            BattleOrderFormationInfo formation = ResolveFormation(unit, formationMap);
            BattleOrderUnitSignals signals = BattleOrderRuntimeQueries.BuildSignals(
                unit,
                enemies,
                allies,
                roster,
                simulation,
                livingAverageAttack
            );

            result.Add(
                new SotEnemyUnitDto
                {
                    unitId = BattleOrderRuntimeQueries.GetUnitId(unit, roster),
                    isAlive = BattleOrderRuntimeQueries.IsAlive(unit),
                    canBeTargeted = BattleOrderRuntimeQueries.IsTargetableForParser(unit, simulation),
                    isRanged = unit.Snapshot != null && unit.Snapshot.IsRanged,
                    hpRatio = signals.HpRatio,
                    attackRatioToAvg = signals.AttackRatioToAvg,
                    engagedByOpponentCount = signals.EngagedByOpponentCount,
                    teamFormationRole = formation.Role,
                }
            );
        }

        return result.ToArray();
    }

    private static BattleOrderFormationInfo ResolveFormation(
        BattleRuntimeUnit unit,
        Dictionary<BattleRuntimeUnit, BattleOrderFormationInfo> formationMap
    )
    {
        if (unit != null && formationMap != null && formationMap.TryGetValue(unit, out BattleOrderFormationInfo info))
            return info;

        return new BattleOrderFormationInfo("midline", false, true);
    }
}
