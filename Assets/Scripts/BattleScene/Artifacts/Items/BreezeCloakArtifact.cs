using System.Collections.Generic;
using UnityEngine;

// 산들바람 망토: 범위 3 내에(다른 장신구들보다 상대적으로 작은 범위) 아군이 없으면 이속, 공속 % 증가
// 수치 설정: 범위는 좁은 반경 3.0f(거리 제곱 9f)로 고정하였으며, 상태이상 버프의 Level 값을 장신구 레벨의 2배수 비율로 부여하도록 설정했습니다 (1Lv: 버프 레벨 2, 2Lv: 버프 레벨 4, 3Lv: 버프 레벨 6).
public sealed class BreezeCloakArtifact : IBattleStartArtifactEffect
{
    public ArtifactId ArtifactId => ArtifactId.BreezeCloak;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        ScheduleCheck(owner, context, effects);
    }

    private void ScheduleCheck(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.ScheduleEffect(
            0.5f,
            context.Actor,
            context.Actor,
            context,
            (ctx, sink) =>
            {
                if (owner.IsCombatDisabled)
                    return;

                bool hasAlly = false;
                float radiusSqr = 9f;

                for (int i = 0; i < ctx.Units.Count; i++)
                {
                    BattleRuntimeUnit unit = ctx.Units[i];
                    if (unit == null || unit.State == null || unit.IsCombatDisabled || unit.State == owner)
                        continue;

                    if (unit.TeamId == owner.TeamId && (unit.Position - owner.Position).sqrMagnitude <= radiusSqr)
                    {
                        hasAlly = true;
                        break;
                    }
                }

                if (!hasAlly)
                {
                    sink.ApplyStatus(
                        new BattleStatusRequest
                        {
                            Source = owner,
                            Target = owner,
                            Type = BattleStatusType.MoveSpeed,
                            Level = _level * 2,
                            Duration = 0.6f,
                            IsDebuff = false,
                            IsDispelAllowed = true,
                        }
                    );

                    sink.ApplyStatus(
                        new BattleStatusRequest
                        {
                            Source = owner,
                            Target = owner,
                            Type = BattleStatusType.AttackSpeed,
                            Level = _level * 2,
                            Duration = 0.6f,
                            IsDebuff = false,
                            IsDispelAllowed = true,
                        }
                    );
                }

                ScheduleCheck(owner, ctx, sink);
            }
        );
    }
}
