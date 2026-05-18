using System.Collections.Generic;
using UnityEngine;

// ObservationContext의 전투 상태를 observation/reward가 바로 소비할 수 있는 정규화된 scalar signal로 압축한 묶음이다.
// 원본 상태나 선택 이력은 담지 않고, anchor 관계/위협도/측면 여유/국지 압박처럼 파생된 전투 신호만 보관한다.
public readonly struct GladiatorCombatSignalFeatures
{
    private const float Epsilon = 1e-6f;

    // anchor와의 기본 관계: 거리, 상호 위협도, 즉시 사거리 진입 여부.
    public readonly float AnchorDistanceRatio;
    public readonly float AnchorThreatToSelfRatio;
    public readonly float SelfThreatToAnchorRatio;
    public readonly float AnchorInSelfRange;
    public readonly float SelfInAnchorRange;

    // 좌우 lane 신호는 flank 경로의 상대적 여유를 나타낸다.
    public readonly float LeftLaneFreeRatio;
    public readonly float RightLaneFreeRatio;

    // 전장 전체 압축 신호: 적이 얼마나 뭉쳐 있는지 표현한다.
    public readonly float EnemyClusterPressure;

    // anchor 주변의 국지 형세 신호다. peel, engage, assassinate 보상 규칙이 주로 이 묶음을 본다.
    public readonly float AnchorAllySupportPressure;
    public readonly float AnchorEnemyFocusPressure;
    public readonly float AnchorEnemyIsolation;
    public readonly float AnchorEnemyRetreatSignal;

    public GladiatorCombatSignalFeatures(
        float anchorDistanceRatio,
        float anchorThreatToSelfRatio,
        float selfThreatToAnchorRatio,
        float anchorInSelfRange,
        float selfInAnchorRange,
        float leftLaneFreeRatio,
        float rightLaneFreeRatio,
        float enemyClusterPressure,
        float anchorAllySupportPressure,
        float anchorEnemyFocusPressure,
        float anchorEnemyIsolation,
        float anchorEnemyRetreatSignal
    )
    {
        AnchorDistanceRatio = anchorDistanceRatio;
        AnchorThreatToSelfRatio = anchorThreatToSelfRatio;
        SelfThreatToAnchorRatio = selfThreatToAnchorRatio;
        AnchorInSelfRange = anchorInSelfRange;
        SelfInAnchorRange = selfInAnchorRange;
        LeftLaneFreeRatio = leftLaneFreeRatio;
        RightLaneFreeRatio = rightLaneFreeRatio;
        EnemyClusterPressure = enemyClusterPressure;
        AnchorAllySupportPressure = anchorAllySupportPressure;
        AnchorEnemyFocusPressure = anchorEnemyFocusPressure;
        AnchorEnemyIsolation = anchorEnemyIsolation;
        AnchorEnemyRetreatSignal = anchorEnemyRetreatSignal;
    }

    // Builder는 observation/reward가 공유하는 combat signal 계산 규칙을 타입 옆에 둔다.
    public static class Builder
    {
        public static GladiatorCombatSignalFeatures Build(GladiatorObservationContext context)
        {
            BattleUnitCombatState self = context.Self;
            BattleUnitCombatState anchor = context.CurrentAnchor;
            if (self == null || anchor == null || anchor.IsCombatDisabled)
            {
                return new GladiatorCombatSignalFeatures(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }

            Vector3 anchorPosition = anchor.Position;
            float distance = Distance2D(self.Position, anchorPosition);
            float selfRange = GetEffectiveRange(self, anchor, self.AttackRange);
            float anchorRange = GetEffectiveRange(anchor, self, anchor.AttackRange);
            Vector2 toAnchor = context.WorldToObservationAxes(anchorPosition - self.Position);
            Vector2 forward = toAnchor.sqrMagnitude > Epsilon ? toAnchor.normalized : Vector2.up;

            GladiatorAnchorRelationFeatures relations = GladiatorAnchorRelationFeatureBuilder.Build(
                self,
                anchor,
                context.Teammates,
                context.Opponents,
                context.ArenaRadius
            );

            return new GladiatorCombatSignalFeatures(
                GladiatorObservationNormalization.NormalizeDistanceByArenaRadius(distance, context.ArenaRadius),
                GetDamageToMaxHealthRatio(anchor, self),
                GetDamageToMaxHealthRatio(self, anchor),
                distance <= selfRange ? 1f : 0f,
                distance <= anchorRange ? 1f : 0f,
                ComputeFlankClearanceRatio(context, self, context.Opponents, forward, -1f, context.ArenaRadius),
                ComputeFlankClearanceRatio(context, self, context.Opponents, forward, 1f, context.ArenaRadius),
                ComputeEnemyClusterPressure(self, context.Opponents, context.ArenaRadius),
                relations.AllySupportPressure,
                relations.EnemyFocusPressure,
                relations.EnemyIsolation,
                relations.EnemyRetreatSignal
            );
        }

        // anchor를 바라보는 방향의 좌/우 측면이 얼마나 비어 있는지
        // 1.0: 그쪽 측면에 적이 거의 없음, 0.0: 모든 적이 그쪽 측면에 있음
        private static float ComputeFlankClearanceRatio(
            GladiatorObservationContext context,
            BattleUnitCombatState self,
            IReadOnlyList<BattleUnitCombatState> opponents,
            Vector2 forward,
            float laneSign,
            float arenaRadius
        )
        {
            if (arenaRadius <= Epsilon)
            {
                return 1f;
            }

            Vector2 side = new Vector2(-forward.y, forward.x) * laneSign;
            int blockers = 0;
            int samples = 0;
            for (int i = 0; i < opponents.Count; i++)
            {
                BattleUnitCombatState unit = opponents[i];
                if (unit == null || unit.IsCombatDisabled)
                {
                    continue;
                }

                Vector2 relative = context.WorldToObservationAxes(unit.Position - self.Position);
                if (relative.sqrMagnitude <= Epsilon)
                {
                    continue;
                }

                if (Vector2.Dot(relative.normalized, side) > 0.35f)
                {
                    blockers++;
                }

                samples++;
            }

            return samples > 0 ? Mathf.Clamp01(1f - blockers / (float)samples) : 1f;
        }

        private static float ComputeEnemyClusterPressure(
            BattleUnitCombatState self,
            IReadOnlyList<BattleUnitCombatState> opponents,
            float arenaRadius
        )
        {
            float total = 0f;
            int count = 0;
            for (int i = 0; i < opponents.Count; i++)
            {
                BattleUnitCombatState opponent = opponents[i];
                if (opponent == null || opponent.IsCombatDisabled)
                {
                    continue;
                }

                total +=
                    1f
                    - GladiatorObservationNormalization.NormalizeDistanceByArenaRadius(
                        Distance2D(self.Position, opponent.Position),
                        arenaRadius
                    );
                count++;
            }

            return count > 0 ? Mathf.Clamp01(total / count) : 0f;
        }

        private static float Distance2D(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            delta.y = 0f;
            return delta.magnitude;
        }

        private static float GetEffectiveRange(
            BattleUnitCombatState attacker,
            BattleUnitCombatState target,
            float attackRange
        )
        {
            if (attacker == null || target == null)
            {
                return 0f;
            }

            return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attackRange) + 0.05f;
        }

        private static float GetDamageToMaxHealthRatio(BattleUnitCombatState attacker, BattleUnitCombatState target)
        {
            if (attacker == null || target == null || target.MaxHealth <= Epsilon)
            {
                return 0f;
            }

            return Mathf.Clamp01(Mathf.Max(0f, attacker.Attack) / target.MaxHealth);
        }
    }
}
