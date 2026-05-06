using System.Collections.Generic;
using UnityEngine;

public sealed class FearSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Fear;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 20f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) => context.Actor != null && context.Actor.PlannedTargetEnemy != null;

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit caster = context.Actor;
        BattleRuntimeUnit target = context.PrimaryTarget;

        if (caster == null || target == null || target.State.IsCombatDisabled) return;

        // 타겟(적)에게 5초짜리 임시 장신구를 강제로 채워버립니다!
        effects.GrantTemporaryArtifact(target, new FearArtifact(caster), 5f, context);

        GameObject activeVfx = VFXManager.Instance.PlayEffect("FearDebuff", target.Position);
        effects.ScheduleEffect(10f, caster, caster, context, (ctx, sink) =>
        {
            if (activeVfx != null) VFXManager.Instance.StopEffect(activeVfx);
        });
    }

    private class FearArtifact : IMovementModifierArtifact
    {
        public ArtifactId ArtifactId => ArtifactId.None;
        private readonly BattleRuntimeUnit _fearSource; // 나를 도망가게 만든 놈

        public FearArtifact(BattleRuntimeUnit fearSource) { _fearSource = fearSource; }

        public void Initialize(BattleUnitCombatState owner, in BattleEffectContext context) { }

        public void ModifyMoveSpeed(BattleUnitCombatState owner, ref BattleMoveRequest request)
        {
            if (_fearSource != null && request.Mover != null)
            {
                Vector3 fearDir = request.Mover.Position - _fearSource.Position;
                fearDir.y = 0f;
                if (fearDir.sqrMagnitude > 0.001f)
                {
                    request.Direction = fearDir.normalized; // 시전자 반대 방향으로 무조건 걷게 만듦
                }
            }
        }

        public bool CanIgnoreForcedMovement(BattleUnitCombatState owner, in BattleForcedMovementRequest request) => false;
    }
}
