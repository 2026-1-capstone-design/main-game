using UnityEngine;

// 전술 자석: 사거리 내부의 적이 착용자의 근처로 강제적으로 유도된다. (1, 2, 3레벨)
public sealed class TacticalMagnetArtifact : IBattleStartArtifactEffect
{
    public ArtifactId ArtifactId => ArtifactId.TacticalMagnet;

    private int _level;

    public void Initialize(BattleUnitCombatState owner, int level, in BattleEffectContext context)
    {
        _level = level;
    }

    public void OnBattleStart(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        SchedulePull(owner, context, effects);
    }

    private void SchedulePull(BattleUnitCombatState owner, in BattleEffectContext context, IBattleEffectSink effects)
    {
        effects.ScheduleEffect(
            1f,
            context.Actor,
            context.Actor,
            context,
            (ctx, sink) =>
            {
                if (owner.IsCombatDisabled)
                    return;

                float pullRange = owner.AttackRange + (_level * 2f);

                foreach (var unit in ctx.Units)
                {
                    if (unit.State == null || unit.State.IsCombatDisabled || unit.TeamId == owner.TeamId)
                        continue;

                    if (Vector3.Distance(owner.Position, unit.Position) <= pullRange)
                    {
                        sink.PullTo(owner, unit.State, 1.5f);
                    }
                }

                SchedulePull(owner, ctx, sink);
            }
        );
    }
}
