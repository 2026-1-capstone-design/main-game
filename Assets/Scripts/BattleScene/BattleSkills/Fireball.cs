using System; // 🌟 Action 사용을 위해 필요
using System.Collections.Generic;
using UnityEngine;

// 파이어볼 (스태프) : 화염구 투사체를 발사하여 강력한 데미지를 줍니다.
public sealed class FireballSkill : IBattleSkill
{
    public WeaponSkillId SkillId => WeaponSkillId.Fireball;
    public skillType SkillCategory => skillType.attack;
    public IReadOnlyList<WeaponType> CompatibleWeaponTypes { get; } = new[] { WeaponType.staff };
    public BattleSkillTargetPolicy TargetPolicy => BattleSkillTargetPolicy.PlannedEnemy;
    public float CastRange => 0f;
    public float AreaRadius => 0f;

    public bool CanActivate(in BattleEffectContext context) =>
        context.Actor != null
        && context.Actor.PlannedTargetEnemy != null
        && BattleFieldSnapshot.IsWithinEffectiveAttackDistance(context.Actor.State, context.Actor.PlannedTargetEnemy);

    public void Activate(in BattleEffectContext context, IBattleEffectSink effects)
    {
        BattleRuntimeUnit casterRuntime = context.Actor;
        BattleUnitCombatState casterState = casterRuntime != null ? casterRuntime.State : null;
        BattleUnitCombatState targetState = context.PrimaryTarget != null ? context.PrimaryTarget.State : null;

        if (casterRuntime == null || targetState == null) return;

        // 발사 위치 및 방향 계산
        Vector3 startPos = casterRuntime.Position + Vector3.up;
        Vector3 direction = targetState.Position - startPos;
        direction.y = 0f;

        // 적중했을 때 실행할 구체적인 효과
        Action<BattleUnitCombatState, Vector3, IBattleEffectSink> onHitEffect = (hitTarget, hitPos, sink) =>
        {
            if (hitTarget == null || hitTarget.IsCombatDisabled) return;

            // 기존 로직과 동일하게 2.5배의 큰 데미지를 줍니다
            sink?.DealDamage(
                new BattleDamageRequest
                {
                    Source = casterState,
                    Target = hitTarget,
                    //Amount = casterState.Attack * 2.5f, // 2.5배
                    Amount = 10000,
                    SourceKind = BattleEffectSourceKind.Skill,
                    DamageKind = BattleDamageKind.Direct,
                    SkillId = SkillId,
                    IsSkill = true,
                }
            );

            VFXManager.Instance.PlayEffect("fire_explosion", hitPos);
        };


        float windUpDelay = casterRuntime.GetSkillAnimationDuration() * 0.5f; // 애니메이션 길이에 비례한 선딜레이 설정

        BattleSimulationManager.Instance.LaunchCustomProjectile(
            new BattleDamageRequest { Target = targetState }, // 타겟 추적용 Request
            startPos,
            direction,
            5f,         // 파이어볼 투사체 속도
            "fireball",  // 🌟 에디터에서 ProjectileManager에 등록해둔 ID
            windUpDelay, // 스킬 선딜레이
            onHitEffect  // 🌟 스킬 효과를 투사체에 쥐여줍니다!
        );
    }
}
