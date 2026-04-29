using System;
using System.Collections.Generic;
using Unity.MLAgents.Sensors;
using UnityEngine;

// 관측 컨텍스트.
//
// LastIntent/LastMove/LastRotate는 GladiatorAgent가 직전 OnActionReceived에서 캐시한 값.
// 정책이 자기 직전 행동을 알 수 있어야 지그재그가 줄어든다 (POMDP 대응).
//
// TeamSizeNormalized는 커리큘럼(2v2 → 6v6) 진행 단계를 정책에 노출해서,
// 같은 weight가 다른 팀 크기에서도 적응할 수 있도록 한다.
public readonly struct GladiatorObservationContext
{
    public readonly BattleRuntimeUnit Self;
    public readonly GladiatorRosterView RosterView;
    public readonly Vector3 ArenaCenter;
    public readonly float ArenaRadius;
    public readonly int LastIntent;
    public readonly int LastMove;
    public readonly int LastRotate;
    public readonly float TeamSizeNormalized;

    public GladiatorObservationContext(
        BattleRuntimeUnit self,
        GladiatorRosterView rosterView,
        Vector3 arenaCenter,
        float arenaRadius,
        int lastIntent,
        int lastMove,
        int lastRotate,
        float teamSizeNormalized
    )
    {
        Self = self;
        RosterView = rosterView;
        ArenaCenter = arenaCenter;
        ArenaRadius = arenaRadius;
        LastIntent = lastIntent;
        LastMove = lastMove;
        LastRotate = lastRotate;
        TeamSizeNormalized = teamSizeNormalized;
    }
}

// Observation vector를 채우는 단일 진입점.
//
// schema와 1:1로 매칭되도록 sensor.AddObservation 호출 순서를 schema 정의 순서와 동일하게 유지한다.
// 모든 영역은 Stage 1에서도 차원이 살아 있고, Stage 미활성 영역은 0으로 채운다.
// 정규화는 trainer YAML의 normalize=True가 통계 기반으로 자동 처리한다.
public static class BattleObservationBuilder
{
    private const float RangedAttackRangeThreshold = 3f;

    public static void Write(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleRuntimeUnit self = context.Self;

        // 자신이 사망/비활성이면 전체 0. policy가 죽은 상태를 학습할 필요 없음.
        if (self == null || self.IsCombatDisabled)
        {
            AddZeroes(sensor, GladiatorObservationSchema.TotalSize);
            return;
        }

        // ─── Self segment (43) ────────────────────────────
        WriteSelfSegment(sensor, context);

        // ─── Self situational (8) ─────────────────────────
        WriteSituationalSegment(sensor, context);

        // ─── Teammates 5 × 9 ──────────────────────────────
        IReadOnlyList<BattleRuntimeUnit> teammates = GetTeammatesSorted(context.RosterView, self);
        WriteUnitSlots(sensor, self, teammates, GladiatorObservationSchema.TeammateSlots);

        // ─── Opponents 6 × 9 ──────────────────────────────
        IReadOnlyList<BattleRuntimeUnit> opponents = GetOpponentsSorted(context.RosterView, self);
        WriteUnitSlots(sensor, self, opponents, GladiatorObservationSchema.OpponentSlots);

        // ─── Personality (6) — Stage 2+ ───────────────────
        AddZeroes(sensor, GladiatorObservationSchema.PersonalitySize);

        // ─── Order parameters (11) — Stage 3+ ─────────────
        AddZeroes(sensor, GladiatorObservationSchema.OrderSize);
    }

    private static void WriteSelfSegment(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleRuntimeUnit self = context.Self;

        // Yaw sin/cos (절대 yaw, 학습 시 self-relative frame이라도 절대 방향 정보가 무기/스킬 분기에 유용)
        float yawDeg = self.transform.eulerAngles.y;
        float yawRad = yawDeg * Mathf.Deg2Rad;
        sensor.AddObservation(Mathf.Sin(yawRad));
        sensor.AddObservation(Mathf.Cos(yawRad));

        // Health ratio + 그 보색
        float healthRatio = self.MaxHealth > 0f ? Mathf.Clamp01(self.CurrentHealth / self.MaxHealth) : 1f;
        sensor.AddObservation(healthRatio);
        sensor.AddObservation(1f - healthRatio);

        // Stats raw (normalize=True가 trainer 측에서 처리)
        sensor.AddObservation(self.CurrentHealth);
        sensor.AddObservation(self.MaxHealth);
        sensor.AddObservation(self.Attack);
        sensor.AddObservation(self.AttackRange);
        sensor.AddObservation(self.MoveSpeed);

        // Cooldowns
        sensor.AddObservation(self.AttackCooldownRemaining);
        sensor.AddObservation(self.SkillCooldownRemaining);

        // Skill range. 현재 IBattleSkill에 명시 사거리 노출이 없으므로, 임시로 자기 공격 사거리를 사용.
        // Stage 3에서 스킬 사거리 노출 시 교체.
        sensor.AddObservation(self.AttackRange);

        // Weapon one-hot (12)
        WeaponType weaponType = self.Snapshot != null ? self.Snapshot.WeaponType : WeaponType.None;
        WriteWeaponOneHot(sensor, weaponType);

        // Skill type one-hot (4: attack/tank/support/enhance, None=zeros)
        skillType skill = self.getSkillType();
        WriteSkillTypeOneHot(sensor, skill);

        // Bias
        sensor.AddObservation(1f);

        // Last action encoding (intent 5 + move 6 + rotate 3)
        WriteOneHot(sensor, context.LastIntent, GladiatorObservationSchema.LastIntentOneHot);
        WriteOneHot(sensor, context.LastMove, GladiatorObservationSchema.LastMoveOneHot);
        WriteOneHot(sensor, context.LastRotate, GladiatorObservationSchema.LastRotateOneHot);
    }

    private static void WriteSituationalSegment(VectorSensor sensor, GladiatorObservationContext context)
    {
        BattleRuntimeUnit self = context.Self;

        // 경기장 중심까지의 self-local 좌표
        Vector2 arenaLocal = WorldToLocal(self, context.ArenaCenter - self.Position);
        sensor.AddObservation(arenaLocal.x);
        sensor.AddObservation(arenaLocal.y);

        IReadOnlyList<BattleRuntimeUnit> teammates = GetTeammatesSorted(context.RosterView, self);
        IReadOnlyList<BattleRuntimeUnit> opponents = GetOpponentsSorted(context.RosterView, self);
        BattleRuntimeUnit nearestOpponent = FindNearestLiving(self, opponents, out float nearestOpponentDistance);
        float selfEffectiveRange = GetEffectiveRange(self, nearestOpponent, self.AttackRange);
        float opponentEffectiveRange = GetEffectiveRange(nearestOpponent, self, nearestOpponent?.AttackRange ?? 0f);
        float threatRadius = Mathf.Max(selfEffectiveRange, opponentEffectiveRange) * 1.25f;

        // 최근접 적 거리
        sensor.AddObservation(nearestOpponentDistance < float.MaxValue ? nearestOpponentDistance : 0f);

        // 사거리 안에 있는 적 존재 여부
        bool inRange = nearestOpponent != null && nearestOpponentDistance <= selfEffectiveRange;
        sensor.AddObservation(inRange ? 1f : 0f);

        // 위험 반경 내 적/아군 비율
        sensor.AddObservation(
            CountLivingWithin(self, opponents, threatRadius) / (float)GladiatorObservationSchema.OpponentSlots
        );
        sensor.AddObservation(
            CountLivingWithin(self, teammates, threatRadius)
                / (float)Mathf.Max(1, GladiatorObservationSchema.TeammateSlots)
        );

        // Boundary pressure
        float distanceFromCenter = Vector3.Distance(
            new Vector3(self.Position.x, 0f, self.Position.z),
            new Vector3(context.ArenaCenter.x, 0f, context.ArenaCenter.z)
        );
        float boundaryPressure =
            context.ArenaRadius > 0f && context.ArenaRadius < float.MaxValue
                ? Mathf.Clamp01(distanceFromCenter / context.ArenaRadius)
                : 0f;
        sensor.AddObservation(boundaryPressure);

        // Team size normalized (커리큘럼 단계 노출)
        sensor.AddObservation(context.TeamSizeNormalized);
    }

    private static void WriteUnitSlots(
        VectorSensor sensor,
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> units,
        int slots
    )
    {
        for (int i = 0; i < slots; i++)
        {
            BattleRuntimeUnit unit = i < units.Count ? units[i] : null;
            if (unit == null || unit.IsCombatDisabled)
            {
                AddZeroes(sensor, GladiatorObservationSchema.UnitSlotSize);
                continue;
            }

            Vector2 localPos = WorldToLocal(self, unit.Position - self.Position);
            sensor.AddObservation(localPos.x);
            sensor.AddObservation(localPos.y);

            float healthRatio = unit.MaxHealth > 0f ? Mathf.Clamp01(unit.CurrentHealth / unit.MaxHealth) : 1f;
            sensor.AddObservation(healthRatio);
            sensor.AddObservation(unit.MaxHealth);

            sensor.AddObservation(unit.Attack);
            sensor.AddObservation(unit.AttackRange);
            sensor.AddObservation(unit.MoveSpeed);

            // 근/원거리 binary (사거리만으로 추정. PPT의 무기 type 압축본)
            sensor.AddObservation(unit.AttackRange >= RangedAttackRangeThreshold ? 1f : 0f);

            // Alive flag (이미 IsCombatDisabled를 위에서 분기했으므로 여기까지 오면 살아있음)
            sensor.AddObservation(1f);
        }
    }

    private static void WriteWeaponOneHot(VectorSensor sensor, WeaponType weaponType)
    {
        int index = (int)weaponType;
        for (int i = 0; i < GladiatorObservationSchema.WeaponOneHot; i++)
        {
            sensor.AddObservation(i == index ? 1f : 0f);
        }
    }

    private static void WriteSkillTypeOneHot(VectorSensor sensor, skillType skill)
    {
        // None은 zeros, 나머지 4종은 각각 슬롯 1개에 1.0
        // enum: None=0, attack=1, tank=2, support=3, enhance=4
        int index = (int)skill - 1; // attack=0, tank=1, support=2, enhance=3
        for (int i = 0; i < GladiatorObservationSchema.SkillTypeOneHot; i++)
        {
            sensor.AddObservation(i == index ? 1f : 0f);
        }
    }

    private static void WriteOneHot(VectorSensor sensor, int activeIndex, int width)
    {
        for (int i = 0; i < width; i++)
        {
            sensor.AddObservation(i == activeIndex ? 1f : 0f);
        }
    }

    private static void AddZeroes(VectorSensor sensor, int count)
    {
        for (int i = 0; i < count; i++)
        {
            sensor.AddObservation(0f);
        }
    }

    private static Vector2 WorldToLocal(BattleRuntimeUnit self, Vector3 worldDelta)
    {
        float x = Vector3.Dot(worldDelta, self.transform.right);
        float z = Vector3.Dot(worldDelta, self.transform.forward);
        return new Vector2(x, z);
    }

    private static BattleRuntimeUnit FindNearestLiving(
        BattleRuntimeUnit self,
        IReadOnlyList<BattleRuntimeUnit> units,
        out float nearestDistance
    )
    {
        BattleRuntimeUnit nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = unit.Position - self.Position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = unit;
            }
        }

        nearestDistance = nearest != null ? Mathf.Sqrt(nearestSqrDistance) : float.MaxValue;
        return nearest;
    }

    private static float CountLivingWithin(BattleRuntimeUnit self, IReadOnlyList<BattleRuntimeUnit> units, float radius)
    {
        if (radius <= 0f)
        {
            return 0f;
        }

        float sqrRadius = radius * radius;
        int count = 0;
        for (int i = 0; i < units.Count; i++)
        {
            BattleRuntimeUnit unit = units[i];
            if (unit == null || unit.IsCombatDisabled)
            {
                continue;
            }

            Vector3 delta = unit.Position - self.Position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= sqrRadius)
            {
                count++;
            }
        }

        return count;
    }

    private static float GetEffectiveRange(BattleRuntimeUnit attacker, BattleRuntimeUnit target, float attackRange)
    {
        if (attacker == null || target == null)
        {
            return 0f;
        }

        return attacker.BodyRadius + target.BodyRadius + Mathf.Max(0f, attackRange) + 0.05f;
    }

    private static IReadOnlyList<BattleRuntimeUnit> GetTeammatesSorted(
        GladiatorRosterView rosterView,
        BattleRuntimeUnit self
    ) => rosterView != null ? rosterView.GetSortedTeammates(self) : Array.Empty<BattleRuntimeUnit>();

    private static IReadOnlyList<BattleRuntimeUnit> GetOpponentsSorted(
        GladiatorRosterView rosterView,
        BattleRuntimeUnit self
    ) => rosterView != null ? rosterView.GetSortedHostiles(self) : Array.Empty<BattleRuntimeUnit>();
}
